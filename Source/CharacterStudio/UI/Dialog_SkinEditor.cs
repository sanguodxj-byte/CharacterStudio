using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using CharacterStudio.Design;
using CharacterStudio.Exporter;
using CharacterStudio.Introspection;
using UnityEngine;
using Verse;
using System.IO;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 皮肤编辑器主窗口
    /// 三栏布局：图层树 | 预览 | 属性面板
    /// </summary>
    public partial class Dialog_SkinEditor : Window
    {
        // ─────────────────────────────────────────────
        // 常量
        // ─────────────────────────────────────────────
        private const float LeftPanelWidth = 240f;
        private const float RightPanelWidth = 280f;
        private const float TopMenuHeight = 30f;
        private const float BottomStatusHeight = 25f;
        private const float LayerItemHeight = 28f;
        private const float ButtonHeight = 24f;
        private const float TabButtonAreaHeight = 84f;
        private new const float Margin = 5f;

        // ─────────────────────────────────────────────
        // 状态
        // ─────────────────────────────────────────────
        private CharacterDesignDocument workingDocument;
        private PawnSkinDef workingSkin;
        private List<ModularAbilityDef> workingAbilities = new List<ModularAbilityDef>();
        private List<CharacterEquipmentDef> WorkingEquipments = new List<CharacterEquipmentDef>();
        private CharacterRenderFixPatch? workingRenderFixPatch;
        private bool layerModificationWorkflowActive = false;
        private readonly SkinEditorSession session = new SkinEditorSession();
        private int selectedLayerIndex
        {
            get => session.SelectedLayerIndex;
            set => session.SelectedLayerIndex = value;
        }
        private int selectedEquipmentIndex = -1;
        private HashSet<int> selectedLayerIndices
        {
            get => session.SelectedLayerIndices;
            set => session.SelectedLayerIndices = value ?? new HashSet<int>();
        }

        // ─────────────────────────────────────────────
        // 目录管理辅助方法
        // ─────────────────────────────────────────────

        private static string GetSkinRootDir()
        {
            string path = Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "Skins");
            Directory.CreateDirectory(path);
            return path;
        }

        private static string GetSkinTextureBrowseStartPath(string? currentPath)
        {
            return ResolveBrowseStartPath(currentPath, GetSkinRootDir());
        }

        private static string GetEquipmentRootDir()
        {
            string path = Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "Equipments");
            Directory.CreateDirectory(path);
            return path;
        }

        private static string GetEquipmentTextureBrowseStartPath(string? currentPath)
        {
            return ResolveBrowseStartPath(currentPath, GetEquipmentRootDir());
        }

        private static string ResolveBrowseStartPath(string? currentPath, string fallback)
        {
            if (string.IsNullOrWhiteSpace(currentPath))
            {
                return fallback;
            }

            string trimmed = currentPath!.Trim().Trim('"');
            if (Directory.Exists(trimmed))
            {
                return trimmed;
            }

            if (File.Exists(trimmed) || Path.IsPathRooted(trimmed))
            {
                try
                {
                    string? dir = Path.GetDirectoryName(trimmed);
                    if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                    {
                        return dir;
                    }
                }
                catch { }
            }

            return fallback;
        }

        private readonly EditorHistory editorHistory = new EditorHistory(100);
        private int undoMutationDepth = 0;
        private Vector2 layerScrollPos;
        private Vector2 propsScrollPos;
        private float lastLayersPanelHeight = 1600f;
        private float lastNodesPanelHeight = 820f;
        private Vector2 faceScrollPos;
        private Vector2 baseScrollPos;
        private Vector2 equipmentScrollPos;
        private bool equipmentPivotEditMode = false;
        private bool isDraggingEquipmentPivot = false;
        private bool isDirty
        {
            get => session.IsDirty;
            set => session.IsDirty = value;
        }
        /// <summary>表情贴图路径内联编辑缓冲，每帧实时同步到 faceConfig</summary>
        private readonly Dictionary<ExpressionType, string> exprPathBuffer = new Dictionary<ExpressionType, string>();
        /// <summary>分层模式部件路径缓冲，key = "PartType|Expression"</summary>
        private readonly Dictionary<string, string> layeredPartPathBuffer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private string scannedOverlayCacheSourceRoot = string.Empty;
        private readonly List<ScannedOverlayCandidate> scannedOverlayCandidates = new List<ScannedOverlayCandidate>();
        private string scannedOverlayCacheError = string.Empty;
        private string scannedEyeCacheSourceRoot = string.Empty;
        private readonly List<ScannedEyeCandidate> scannedEyeCandidates = new List<ScannedEyeCandidate>();
        private string scannedEyeCacheError = string.Empty;
        private int semanticMappingCacheVersion;
        private int semanticMappingCacheBuildVersion = -1;
        private readonly Dictionary<string, List<ExpressionType>> eyeMappingCache = new Dictionary<string, List<ExpressionType>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<ExpressionType>> mouthMappingCache = new Dictionary<string, List<ExpressionType>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> overlayIdMappingCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> overlaySemanticKeyCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> overlayFollowTargetCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private WeaponCarryVisualState previewWeaponCarryState = WeaponCarryVisualState.Undrafted;
 
        private enum EditorTab { BaseAppearance, Layers, Face, Attributes, Animation, Items, Equipment }
        private EditorTab currentTab = EditorTab.BaseAppearance;
        private string statusMessage = "";
        private float statusMessageTime = 0f;
        private bool IsEquipmentOrItemsTab => currentTab == EditorTab.Equipment || currentTab == EditorTab.Items;
        private bool suspendHeavyPreviewWork = false;
        private bool pendingFacePreviewRefresh = false;
        private float nextFacePreviewRefreshRealtime = 0f;
        private const float FacePreviewRefreshThrottleSeconds = 0.05f;
        private CharacterSpawnSettings directSpawnSettings = new CharacterSpawnSettings
        {
            sourceMapForConditionCheck = null,
            arrivalDefName = "CS_SpawnArrival_DropPod",
            arrivalMode = Items.SummonArrivalMode.DropPod,
            spawnEventDefName = "CS_SpawnEvent_PositiveLetter",
            spawnEvent = Items.SummonSpawnEventMode.PositiveLetter,
            eventMessageText = string.Empty,
            eventLetterTitle = string.Empty,
            spawnAnimationDefName = "CS_SpawnAnimation_ExplosionEffect",
            spawnAnimation = Items.SummonSpawnAnimationMode.ExplosionEffect,
            spawnAnimationScale = 1f
        };

        private enum NewSkinWorkflow { StandardLayers, CompositeBase, AnimalStandardLayers, MechanoidStandardLayers }

        /// <summary>
        /// 推荐最小集表情：完成这 8 种即可覆盖绝大多数游戏状态。
        /// Sleeping 与 Blink 共用同一张闭眼贴图，Pain、Dead 亦可共用，
        /// 故实际美术文件可进一步减少。
        /// </summary>
        private static readonly HashSet<ExpressionType> MinSetExpressions = new HashSet<ExpressionType>
        {
            ExpressionType.Neutral,
            ExpressionType.Happy,
            ExpressionType.Sad,
            ExpressionType.Angry,
            ExpressionType.Sleeping,
            ExpressionType.Blink,
            ExpressionType.Dead,
            ExpressionType.Pain,
        };

        // 预览管理
        private readonly struct PreviewFlowStep
        {
            public readonly string Label;
            public readonly ExpressionType RuntimeExpression;
            public readonly float DurationSeconds;
            public readonly bool OverrideEyeDirection;
            public readonly EyeDirection EyeDirection;
            public readonly MouthState? MouthState;
            public readonly LidState? LidState;
            public readonly BrowState? BrowState;
            public readonly EmotionOverlayState? EmotionState;

            public PreviewFlowStep(
                string label,
                ExpressionType runtimeExpression,
                float durationSeconds,
                bool overrideEyeDirection = false,
                EyeDirection eyeDirection = EyeDirection.Center,
                MouthState? mouthState = null,
                LidState? lidState = null,
                BrowState? browState = null,
                EmotionOverlayState? emotionState = null)
            {
                Label = label;
                RuntimeExpression = runtimeExpression;
                DurationSeconds = durationSeconds;
                OverrideEyeDirection = overrideEyeDirection;
                EyeDirection = eyeDirection;
                MouthState = mouthState;
                LidState = lidState;
                BrowState = browState;
                EmotionState = emotionState;
            }
        }

        private MannequinManager? mannequin;
        private Rot4 previewRotation = Rot4.South;
        private float previewZoom = 1f;
        private bool previewExpressionOverrideEnabled = false;
        private ExpressionType previewExpression = ExpressionType.Neutral;
        private bool previewRuntimeExpressionOverrideEnabled = false;
        private ExpressionType previewRuntimeExpression = ExpressionType.Neutral;
        private bool previewMouthStateOverrideEnabled = false;
        private MouthState previewMouthState = MouthState.Normal;
        private bool previewLidStateOverrideEnabled = false;
        private LidState previewLidState = LidState.Normal;
        private bool previewBrowStateOverrideEnabled = false;
        private BrowState previewBrowState = BrowState.Normal;
        private bool previewEmotionStateOverrideEnabled = false;
        private EmotionOverlayState previewEmotionState = EmotionOverlayState.None;
        private bool previewEyeDirectionOverrideEnabled = false;
        private EyeDirection previewEyeDirection = EyeDirection.Center;
        private bool previewGazeCursorEnabled = false;
        private Vector2 previewGazeCursorOffset = Vector2.zero;
        private bool previewAutoPlayEnabled = false;
        private float previewAutoPlayIntervalSeconds = 0.75f;
        private float previewAutoPlayNextStepTime = 0f;
        private int previewAutoPlayStepIndex = 0;

        private bool previewFaceAnimationPlaying = false;
        private bool previewFaceAnimationLoop = false;
        private float previewFaceAnimationLastRealtime = -1f;
        private float previewFaceAnimationElapsedTicks = 0f;

        private bool previewEquipmentAnimationPlaying = false;
        private bool previewEquipmentAnimationLoop = false;
        private float previewEquipmentAnimationLastRealtime = -1f;
        private float previewEquipmentAnimationElapsedTicks = 0f;
        private string previewEquipmentAnimationTriggerKey = string.Empty;
        private static readonly PreviewFlowStep[] PreviewAutoPlayFlowSteps =
        {
            new PreviewFlowStep("SleepLoop", ExpressionType.Sleeping, 1.8f, true, EyeDirection.Center, MouthState.Sleep, LidState.Close),
            new PreviewFlowStep("WorkFocusDown", ExpressionType.Working, 1.05f, true, EyeDirection.Down),
            new PreviewFlowStep("WorkFocusUp", ExpressionType.Working, 0.9f, true, EyeDirection.Up),
            new PreviewFlowStep("ReadScan", ExpressionType.Reading, 1.1f, true, EyeDirection.Down),
            new PreviewFlowStep("HappySoft", ExpressionType.Happy, 1.0f, false, EyeDirection.Center, MouthState.Smile, LidState.Happy, BrowState.Happy, EmotionOverlayState.Blush),
            new PreviewFlowStep("HappyPeak", ExpressionType.Happy, 0.85f, true, EyeDirection.Center, MouthState.Smile, LidState.Happy, BrowState.Happy, EmotionOverlayState.Blush),
            new PreviewFlowStep("ScaredWide", ExpressionType.Scared, 0.7f, true, EyeDirection.Left, MouthState.Open, emotionState: EmotionOverlayState.Sweat),
            new PreviewFlowStep("ScaredScan", ExpressionType.Scared, 0.55f, true, EyeDirection.Right, MouthState.Open, emotionState: EmotionOverlayState.Sweat),
            new PreviewFlowStep("NeutralReset", ExpressionType.Neutral, 1.15f)
        };
        private bool editLayerOffsetPerFacing = false;
        private const KeyCode ReferenceGhostHotkey = KeyCode.F;
        private const float ReferenceGhostAlpha = 0.35f;
        /// <summary>
        /// F键虚影状态：在 DoWindowContents 最开始读取，避免被 IMGUI TextField 拦截。
        /// </summary>
        private bool isHoldingReferenceGhost = false;
        private const float NewSkinPromptCooldownSeconds = 0.25f;

        // 纹理替换模式：仅替换纹理不修改其他属性
        private bool textureOnlyReplace = false;
        // 预览加载纹理名显示
        private string previewLoadedTexName = "";
        private float previewLoadedTexNameTime = 0f;
        // 拖拽撤销快照捕获标记
        private bool hasCapturedUndoForCurrentDrag = false;
        // 装备物品折叠初始化标记
        private bool itemsCollapseInitialized = false;

        // 编辑目标 Pawn（可空，空表示仅预览模式）
        private Pawn? targetPawn;

        // ─────────────────────────────────────────────
        // 树状视图状态
        // ─────────────────────────────────────────────
        private HashSet<string> expandedPaths = new HashSet<string>();
        private HashSet<string> collapsedSections = new HashSet<string>(); // 记录折叠的属性区域
        private RenderNodeSnapshot? cachedRootSnapshot;
        private string selectedNodePath = "";
        private BaseAppearanceSlotType? selectedBaseSlotType;
        private float treeViewHeight = 0f;
        private const float TreeNodeHeight = 22f;
        private const float TreeIndentWidth = 16f;

        // ─────────────────────────────────────────────
        // 窗口设置
        // ─────────────────────────────────────────────

        public override Vector2 InitialSize => new Vector2(1680f, 980f);

        public Dialog_SkinEditor()
        {
            this.doCloseX = true;
            this.doCloseButton = false;
            this.draggable = false;
            this.resizeable = false;
            this.closeOnClickedOutside = false;
            this.absorbInputAroundWindow = false;
            this.forcePause = false;

            // 直接以核心工作流状态创建工作皮肤，省去手动新建-核心工作流的步骤
            string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            workingSkin = new PawnSkinDef
            {
                defName = $"CS_Skin_{uniqueId}",
                label = "CS_Studio_DefaultSkinLabel".Translate(),
                description = "",
                author = "",
                version = "1.0.0",
                humanlikeOnly = true,
                hideVanillaBody = true,
                hideVanillaHead = true,
                hideVanillaHair = true
            };
            workingSkin.baseAppearance = new BaseAppearanceConfig();
            workingSkin.targetRaces.Add(ThingDefOf.Human.defName);

            workingDocument = CreateDocumentFromSkin(workingSkin, preferredRaceDefName: ThingDefOf.Human.defName);
        }

        public Dialog_SkinEditor(PawnSkinDef existingSkin) : this()
        {
            if (existingSkin != null)
            {
                ReplaceWorkingSkin(existingSkin.Clone(), existingSkin.defName, null, syncAbilities: false);
            }
            SyncAbilitiesFromSkin();
        }

        public Dialog_SkinEditor(Pawn pawn) : this()
        {
            targetPawn = pawn;

            if (pawn?.def != null)
            {
                workingSkin.targetRaces = new List<string> { pawn.def.defName };
                workingDocument.preferredPreviewRaceDefName = pawn.def.defName;
                workingDocument.preferredTargetRaceDefName = pawn.def.defName;
                workingDocument.characterDefinition.raceDefName = pawn.def.defName;
            }

            var comp = pawn?.GetComp<CompPawnSkin>();
            var activeSkin = comp?.ActiveSkin;
            if (activeSkin != null)
            {
                ReplaceWorkingSkin(activeSkin.Clone(), activeSkin.defName, pawn?.def?.defName, syncAbilities: false);
            }

            SyncAbilitiesFromSkin();
        }

        private void SyncAbilitiesFromSkin()
        {
            workingAbilities.Clear();
            if (workingSkin.abilities == null) return;

            foreach (var ability in workingSkin.abilities)
            {
                if (ability != null)
                {
                    workingAbilities.Add(ability.Clone());
                }
            }
        }

        private void SyncAbilitiesToSkin()
        {
            workingSkin.abilities.Clear();
            if (workingAbilities == null) return;

            foreach (var ability in workingAbilities)
            {
                if (ability != null)
                {
                    workingSkin.abilities.Add(ability.Clone());
                }
            }
        }

        private CharacterDesignDocument CreateDocumentFromSkin(PawnSkinDef? skin, string? sourceSkinDefName = null, string? preferredRaceDefName = null)
        {
            var runtimeSkin = skin?.Clone() ?? new PawnSkinDef();
            var document = new CharacterDesignDocument
            {
                runtimeSkin = runtimeSkin,
                sourceSkinDefName = sourceSkinDefName ?? runtimeSkin.defName ?? string.Empty,
                preferredPreviewRaceDefName = preferredRaceDefName ?? runtimeSkin.targetRaces.FirstOrDefault() ?? string.Empty,
                preferredTargetRaceDefName = preferredRaceDefName ?? runtimeSkin.targetRaces.FirstOrDefault() ?? string.Empty
            };
            document.SyncMetadataFromRuntimeSkin();
            document.characterDefinition.EnsureDefaults(
                runtimeSkin.defName ?? "CS_Character",
                DefDatabase<ThingDef>.GetNamedSilentFail(document.preferredTargetRaceDefName)
                    ?? DefDatabase<ThingDef>.GetNamedSilentFail(document.preferredPreviewRaceDefName)
                    ?? ThingDefOf.Human,
                runtimeSkin.attributes);
            RebuildNodeRulesFromRuntimeSkin(document);
            return document;
        }

        private void ReplaceWorkingSkin(PawnSkinDef? skin, string? sourceSkinDefName = null, string? preferredRaceDefName = null, bool syncAbilities = true)
        {
            workingDocument = CreateDocumentFromSkin(skin, sourceSkinDefName, preferredRaceDefName);
            workingSkin = workingDocument.runtimeSkin;
            editLayerOffsetPerFacing = workingSkin?.editLayerOffsetPerFacing ?? false;
            if (syncAbilities)
            {
                SyncAbilitiesFromSkin();
            }
        }

        private void RebuildNodeRulesFromWorkingSkin()
        {
            if (workingDocument == null)
            {
                return;
            }

            workingDocument.runtimeSkin = workingSkin;
            workingDocument.SyncMetadataFromRuntimeSkin();
            RebuildNodeRulesFromRuntimeSkin(workingDocument);
        }

        private void RebuildNodeRulesFromRuntimeSkin(CharacterDesignDocument document)
        {
            if (document == null)
            {
                return;
            }

            var runtimeSkin = document.runtimeSkin ?? new PawnSkinDef();
            var rebuiltRules = new List<CharacterNodeRule>();
            string sourceRaceDefName = document.preferredPreviewRaceDefName ?? string.Empty;

            foreach (var hiddenPath in runtimeSkin.hiddenPaths ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(hiddenPath))
                {
                    continue;
                }

                rebuiltRules.Add(CharacterNodeRule.CreateHide(
                    CharacterNodeReference.FromRuntimeAnchor(hiddenPath, string.Empty, null, null, -1, sourceRaceDefName, string.Empty)));
            }

            foreach (var layer in runtimeSkin.layers ?? new List<PawnLayerConfig>())
            {
                if (layer == null)
                {
                    continue;
                }

                string anchorPath = layer.anchorPath ?? string.Empty;
                string anchorTag = layer.anchorTag ?? string.Empty;
                if (string.IsNullOrWhiteSpace(anchorPath) && string.IsNullOrWhiteSpace(anchorTag))
                {
                    continue;
                }

                rebuiltRules.Add(CharacterNodeRule.CreateAttach(
                    CharacterNodeReference.FromRuntimeAnchor(anchorPath, anchorTag, layer.texPath, layer.workerClass?.FullName, -1, sourceRaceDefName, string.Empty),
                    layer));
            }

            document.nodeRules = rebuiltRules;
        }

        private void SyncWorkingDocumentFromWorkingSkin(bool syncAbilities = true)
        {
            if (workingDocument == null)
            {
                workingDocument = CreateDocumentFromSkin(workingSkin);
            }

            if (syncAbilities)
            {
                SyncAbilitiesToSkin();
            }

            workingDocument.runtimeSkin = workingSkin;
            workingDocument.SyncMetadataFromRuntimeSkin();
            RebuildNodeRulesFromRuntimeSkin(workingDocument);
        }

        private PawnSkinDef BuildRuntimeSkinForExecution()
        {
            SyncWorkingDocumentFromWorkingSkin();
            return CharacterDesignCompiler.CompileRuntimeSkin(workingDocument);
        }

        private CharacterApplicationPlan BuildApplicationPlan(Pawn? planTargetPawn, bool isPreview, string source)
        {
            SyncWorkingDocumentFromWorkingSkin();
            return CharacterApplicationPlanBuilder.Build(workingDocument, planTargetPawn, isPreview, source);
        }

        private void EnterLayerModificationWorkflow(Pawn pawn)
        {
            layerModificationWorkflowActive = true;
            workingRenderFixPatch = RenderFixPatchRegistry.CreatePatchForRace(
                pawn.def.defName,
                preferredDefName: $"CS_RenderFix_{pawn.def.defName}",
                preferredLabel: $"{pawn.def.LabelCap} Render Fix");
        }

        private bool IsNodeHiddenInCurrentMode(RenderNodeSnapshot node)
        {
            if (layerModificationWorkflowActive)
            {
                return workingRenderFixPatch?.hideNodePaths != null
                    && workingRenderFixPatch.hideNodePaths.Contains(node.uniqueNodePath);
            }

#pragma warning disable CS0618
            return workingSkin.hiddenPaths.Contains(node.uniqueNodePath)
                || workingSkin.hiddenTags.Contains(node.tagDefName);
#pragma warning restore CS0618
        }

        private void ToggleNodeVisibilityInCurrentMode(RenderNodeSnapshot node)
        {
            if (layerModificationWorkflowActive)
            {
                ToggleNodeVisibilityInPatch(node);
                return;
            }

            ToggleNodeVisibilityInSkin(node);
        }

        private void ToggleNodeVisibilityInSkin(RenderNodeSnapshot node)
        {
            MutateWithUndo(() =>
            {
                bool hidden;
                if (workingSkin.hiddenPaths.Contains(node.uniqueNodePath))
                {
                    workingSkin.hiddenPaths.Remove(node.uniqueNodePath);
#pragma warning disable CS0618
                    if (!string.IsNullOrEmpty(node.tagDefName))
                    {
                        workingSkin.hiddenTags.Remove(node.tagDefName);
                    }
#pragma warning restore CS0618
                    hidden = false;
                    ShowStatus("CS_Studio_Msg_Shown".Translate(node.uniqueNodePath));
                }
                else
                {
                    workingSkin.hiddenPaths.Add(node.uniqueNodePath);
#pragma warning disable CS0618
                    if (!string.IsNullOrEmpty(node.tagDefName) && !workingSkin.hiddenTags.Contains(node.tagDefName))
                    {
                        workingSkin.hiddenTags.Add(node.tagDefName);
                    }
#pragma warning restore CS0618
                    hidden = true;
                    ShowStatus("CS_Studio_Msg_Hidden".Translate(node.uniqueNodePath));
                }

                UpsertHideNodeRule(node, hidden);
            }, refreshPreview: true, refreshRenderTree: true);
        }

        private void ToggleNodeVisibilityInPatch(RenderNodeSnapshot node)
        {
            if (workingRenderFixPatch == null)
            {
                if (targetPawn == null)
                {
                    return;
                }

                EnterLayerModificationWorkflow(targetPawn);
            }

            MutateWithUndo(() =>
            {
                workingRenderFixPatch ??= new CharacterRenderFixPatch();
                workingRenderFixPatch.hideNodePaths ??= new List<string>();

                if (workingRenderFixPatch.hideNodePaths.Contains(node.uniqueNodePath))
                {
                    workingRenderFixPatch.hideNodePaths.Remove(node.uniqueNodePath);
                    ShowStatus($"已从图层修改补丁中显示节点：{node.uniqueNodePath}");
                }
                else
                {
                    workingRenderFixPatch.hideNodePaths.Add(node.uniqueNodePath);
                    ShowStatus($"已加入图层修改补丁隐藏节点：{node.uniqueNodePath}");
                }

                workingRenderFixPatch.Normalize();
            }, refreshPreview: true, refreshRenderTree: true);
        }

        private float GetNodePatchDrawOrderOffset(string nodePath)
        {
            if (!layerModificationWorkflowActive || workingRenderFixPatch?.orderOverrides == null)
            {
                return 0f;
            }

            RenderNodeOrderOverride? entry = workingRenderFixPatch.orderOverrides
                .FirstOrDefault(overrideEntry => overrideEntry != null
                    && string.Equals(overrideEntry.targetNodePath, nodePath, StringComparison.OrdinalIgnoreCase));
            return entry?.drawOrderOffset ?? 0f;
        }

        private void SetNodePatchDrawOrderOffset(string nodePath, float offset)
        {
            if (!layerModificationWorkflowActive)
            {
                return;
            }

            if (workingRenderFixPatch == null)
            {
                if (targetPawn == null)
                {
                    return;
                }

                EnterLayerModificationWorkflow(targetPawn);
            }

            workingRenderFixPatch ??= new CharacterRenderFixPatch();
            workingRenderFixPatch.orderOverrides ??= new List<RenderNodeOrderOverride>();
            workingRenderFixPatch.orderOverrides.RemoveAll(overrideEntry => overrideEntry != null
                && string.Equals(overrideEntry.targetNodePath, nodePath, StringComparison.OrdinalIgnoreCase));

            if (Math.Abs(offset) > 0.0001f)
            {
                workingRenderFixPatch.orderOverrides.Add(new RenderNodeOrderOverride
                {
                    targetNodePath = nodePath,
                    drawOrderOffset = offset
                });
            }

            workingRenderFixPatch.Normalize();
        }

        private CharacterRenderFixPatch BuildRenderFixPatchFromCurrentState(Pawn pawn)
        {
            if (layerModificationWorkflowActive && workingRenderFixPatch != null)
            {
                CharacterRenderFixPatch patchClone = workingRenderFixPatch.Clone();
                patchClone.targetRaceDefs = new List<string> { pawn.def.defName };
                patchClone.Normalize();
                return patchClone;
            }

            string raceDefName = pawn.def.defName;
            CharacterRenderFixPatch patch = RenderFixPatchRegistry.CreatePatchForRace(
                raceDefName,
                preferredDefName: $"CS_RenderFix_{raceDefName}",
                preferredLabel: $"{pawn.def.LabelCap} Render Fix");

            patch.hideNodePaths = (workingSkin.hiddenPaths ?? new List<string>())
                .Where(nodePath => !string.IsNullOrWhiteSpace(nodePath))
                .Where(nodePath => nodePath.IndexOf("Apparel", StringComparison.OrdinalIgnoreCase) < 0)
                .Where(nodePath => nodePath.IndexOf("Headgear", StringComparison.OrdinalIgnoreCase) < 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            patch.orderOverrides = new List<RenderNodeOrderOverride>();
            patch.Normalize();
            return patch;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            InitializeMannequin();

            // 默认构造时（无 Pawn/已有皮肤导入），直接以核心工作流状态启动
            if (targetPawn == null && workingDocument?.preferredPreviewRaceDefName != null)
            {
                ThingDef previewRace = DefDatabase<ThingDef>.GetNamedSilentFail(workingDocument.preferredPreviewRaceDefName) ?? ThingDefOf.Human;
                mannequin?.ForceReset(previewRace);
                RefreshRenderTree();
                RefreshPreview();
            }
        }

        public override void PreClose()
        {
            equipmentPivotEditMode = false;
            isDraggingEquipmentPivot = false;
            base.PreClose();
            CleanupMannequin();
        }

 
        // 预览相关逻辑已拆分至 Dialog_SkinEditor.Preview.cs
        // 属性面板与辅助绘制逻辑已拆分至 Dialog_SkinEditor.Properties.cs
        // 事件处理与工作流逻辑已拆分至 Dialog_SkinEditor.Workflows.cs

        // ─────────────────────────────────────────────
        // 主文件保留：字段、构造、生命周期、共享状态
        // 其余职责由各 partial 文件承载
        // ─────────────────────────────────────────────

        private void OpenAbilityEditor()
        {
            SyncAbilitiesToSkin();
            Find.WindowStack.Add(new Dialog_AbilityEditor(workingAbilities));
        }

        // ─────────────────────────────────────────────
        // 导入辅助方法（供 Face.AutoImport 使用）
        // ─────────────────────────────────────────────

        /// <summary>
        /// 将导入源根目录路径解析为物理磁盘路径。
        /// 相对路径从 Mod 的 Textures 目录解析，绝对路径直接使用。
        /// </summary>
        private static string ResolveSourceRootToPhysicalDirectory(string sourceRoot)
        {
            if (string.IsNullOrWhiteSpace(sourceRoot))
                return string.Empty;

            if (System.IO.Path.IsPathRooted(sourceRoot))
                return sourceRoot;

            // 相对路径：从 Mod ContentPack 的 Textures 目录查找
            var mod = LoadedModManager.GetMod<CharacterStudioMod>();
            if (mod?.Content?.RootDir != null)
            {
                string texDir = System.IO.Path.Combine(mod.Content.RootDir, "Textures", sourceRoot);
                if (System.IO.Directory.Exists(texDir))
                    return texDir;
            }

            return sourceRoot;
        }

        /// <summary>
        /// 判断路径是否为外部路径（非 Mod 内部路径）。
        /// </summary>
        private static bool IsExternalPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return System.IO.Path.IsPathRooted(path) || path.StartsWith("/") || path.Contains(":");
        }

        /// <summary>
        /// 将扫描到的文件路径转换为存储路径（相对 Mod Textures 目录的路径）。
        /// </summary>
        private static string ConvertScannedFileToStoragePath(string filePath, string physicalRoot, string logicalRoot)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return string.Empty;

            // 如果已经是相对路径或 ContentFinder 路径，直接返回
            string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
            string relativeDir = logicalRoot;

            return string.IsNullOrEmpty(relativeDir) ? fileName : System.IO.Path.Combine(relativeDir, fileName);
        }
    }
}
