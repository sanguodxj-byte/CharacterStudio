using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using CharacterStudio.Design;
using CharacterStudio.Exporter;
using RimWorld;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        // ─────────────────────────────────────────────
        private const string InternalizedXmlFileSuffix = ".internalized";

        private void OnSaveSkin()
        {
            // 图层修改工作流：委托给 RenderFix 保存
            if (layerModificationWorkflowActive)
            {
                OnSaveRenderFixPatch();
                return;
            }

            if (workingSkin == null)
            {
                Log.Error("[CharacterStudio] 保存失败：workingSkin 为空");
                ShowStatus("CS_Studio_Err_SaveFailed".Translate());
                return;
            }

            workingSkin.defName = workingSkin.defName?.Trim() ?? string.Empty;
            workingSkin.label = workingSkin.label?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(workingSkin.defName))
            {
                ShowStatus("CS_Studio_Err_DefNameEmpty".Translate());
                return;
            }

            if (!UIHelper.IsValidDefName(workingSkin.defName))
            {
                ShowStatus("CS_Studio_Err_DefNameInvalid".Translate());
                return;
            }

            if (string.IsNullOrEmpty(workingSkin.label))
            {
                workingSkin.label = workingSkin.defName;
            }

            try
            {
                string savedFilePath;
                PawnSkinDef? registeredSkin;
                if (!PersistCurrentSkinDesign(showUserMessages: true, out savedFilePath, out registeredSkin))
                {
                    return;
                }

                string finalStatus = "CS_Studio_Msg_SaveSuccess".Translate();
                if (targetPawn != null && registeredSkin != null)
                {
                    var applyPlan = BuildApplicationPlan(targetPawn, false, "SaveAndApplyToTargetPawn");
                    applyPlan.runtimeSkin = registeredSkin.Clone();
                    if (CharacterApplicationExecutor.Execute(applyPlan))
                    {
                        finalStatus = applyPlan.statusMessage.Translate(targetPawn.LabelShort);
                        Messages.Message(
                            "CS_Appearance_Applied".Translate(registeredSkin.label ?? registeredSkin.defName, targetPawn.LabelShort),
                            MessageTypeDefOf.PositiveEvent,
                            false
                        );

                        if (applyPlan.warnings != null && applyPlan.warnings.Count > 0)
                        {
                            Messages.Message(applyPlan.warnings[0].Translate(), MessageTypeDefOf.CautionInput, false);
                        }
                    }
                    else
                    {
                        string applyStatusKey = string.IsNullOrWhiteSpace(applyPlan.statusMessage)
                            ? "CS_Studio_Err_ApplyFailedCheckLog"
                            : applyPlan.statusMessage;
                        finalStatus = applyStatusKey.Translate();
                        Log.Warning($"[CharacterStudio] 保存后自动应用失败: source={applyPlan.source}, pawn={targetPawn.LabelShort}, status={applyPlan.statusMessage ?? "<empty>"}");
                    }
                }

                ShowStatus(finalStatus);
                Messages.Message("CS_Studio_Msg_SavePath".Translate(savedFilePath), MessageTypeDefOf.PositiveEvent, false);
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 保存失败: {ex}");

                string statusMessage = SkinSaver.GetSaveFailureMessageKey(ex) == "CS_Studio_Err_SaveFailedDiskFull"
                    ? "保存失败：磁盘空间不足、写入配额已满，或目标配置目录不可再写入"
                    : "CS_Studio_Err_SaveFailed".Translate();
                ShowStatus(statusMessage);
            }
        }

        private bool PersistCurrentSkinDesign(bool showUserMessages, out string savedFilePath, out PawnSkinDef? registeredSkin)
        {
            return PersistCurrentSkinDesign(showUserMessages, null, null, out savedFilePath, out registeredSkin);
        }

        private bool PersistCurrentSkinDesign(bool showUserMessages, PawnSkinDef? skinToSave, out string savedFilePath, out PawnSkinDef? registeredSkin)
        {
            return PersistCurrentSkinDesign(showUserMessages, skinToSave, null, out savedFilePath, out registeredSkin);
        }

        private bool PersistCurrentSkinDesign(bool showUserMessages, PawnSkinDef? skinToSave, string? fileNameSuffix, out string savedFilePath, out PawnSkinDef? registeredSkin)
        {
            savedFilePath = string.Empty;
            registeredSkin = null;

            if (workingSkin == null)
            {
                return false;
            }

            PawnSkinDef saveSourceSkin = skinToSave ?? workingSkin;

            if (string.IsNullOrEmpty(saveSourceSkin.label))
            {
                saveSourceSkin.label = saveSourceSkin.defName;
            }

            string exportDir = Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "Skins");
            string fileSuffix = fileNameSuffix?.Trim() ?? string.Empty;
            string baseFileStem = saveSourceSkin.defName ?? "CS_Skin";
            string fileName = baseFileStem + fileSuffix + ".xml";
            string filePath = Path.Combine(exportDir, fileName);

            Directory.CreateDirectory(exportDir);

            float mannequinHeadZ = GetMannequinHeadOffsetZ();
            if (mannequinHeadZ != 0f)
                saveSourceSkin.previewHeadOffsetZ = mannequinHeadZ;

            PawnSkinDef savePlanSkin;
            if (skinToSave != null)
            {
                savePlanSkin = skinToSave.Clone();
            }
            else
            {
                savePlanSkin = BuildRuntimeSkinForExecution();
                if (savePlanSkin == null)
                {
                    Log.Error("[CharacterStudio] 保存失败：未能构建运行时皮肤");
                    if (showUserMessages)
                    {
                        ShowStatus("CS_Studio_Err_SaveFailed".Translate());
                    }
                    return false;
                }
            }

            savePlanSkin.RemoveApparelHidingData();

            CharacterDefinition characterDefinitionToSave = workingDocument.characterDefinition;
            if (skinToSave != null)
            {
                characterDefinitionToSave = workingDocument.characterDefinition?.Clone() ?? new CharacterDefinition();
            }

            string ownerDefName = savePlanSkin.defName ?? workingSkin.defName ?? "CS_Character";
            string characterFilePath = Path.Combine(exportDir, ownerDefName + fileSuffix + ".character.xml");
            ThingDef resolvedRace = ResolveSpawnRaceForCurrentDesign(savePlanSkin);
            characterDefinitionToSave.EnsureDefaults(ownerDefName, resolvedRace, savePlanSkin.attributes);
            characterDefinitionToSave.defName = ownerDefName;
            if (string.IsNullOrWhiteSpace(characterDefinitionToSave.displayName))
            {
                characterDefinitionToSave.displayName = savePlanSkin.label ?? ownerDefName;
            }

            SkinSaver.SaveSkinDef(savePlanSkin, filePath);
            CharacterDefinitionXmlUtility.Save(characterDefinitionToSave, characterFilePath);
            ModBuilder.ExportScatteredLooseFiles(savePlanSkin, characterDefinitionToSave);
            CharacterRuntimeTriggerConfigUtility.SyncCharacterAssets(savePlanSkin, characterDefinitionToSave);

            var registered = PawnSkinDefRegistry.RegisterOrReplace(savePlanSkin.Clone());
            string? preferredRaceDefName = registered.targetRaces != null && registered.targetRaces.Count > 0
                ? registered.targetRaces[0]
                : null;
            ReplaceWorkingSkin(registered, registered.defName, preferredRaceDefName, syncAbilities: false);
            workingDocument.characterDefinition = characterDefinitionToSave.Clone();
            workingDocument.sourceSkinDefName = registered.defName ?? string.Empty;
            workingDocument.lastSavedFilePath = filePath;
            workingDocument.SyncMetadataFromRuntimeSkin();

            if (registered.applyAsDefaultForTargetRaces)
            {
                PawnSkinBootstrapComponent.ApplyDefaultSkinsToCurrentGame();
            }

            savedFilePath = filePath;
            registeredSkin = registered;
            isDirty = false;
            return true;
        }

        private void OnApplySkinToTargetPawn()

        {
            if (targetPawn == null)
            {
                ShowStatus("CS_Studio_Msg_TargetPawnRequired".Translate());
                return;
            }

            if (workingSkin == null)
            {
                Log.Error("[CharacterStudio] 应用失败：workingSkin 为空");
                ShowStatus("CS_Studio_Err_ApplyFailedCheckLog".Translate());
                return;
            }

            try
            {
                var applyPlan = BuildApplicationPlan(targetPawn, false, "ApplyToTargetPawn");
                var runtimeSkin = applyPlan.runtimeSkin;
                runtimeSkin.RemoveApparelHidingData();
                if (CharacterApplicationExecutor.Execute(applyPlan))
                {
                    RefreshRenderTree();
                    ShowStatus(applyPlan.statusMessage.Translate(targetPawn.LabelShort));
                    Messages.Message(
                        "CS_Appearance_Applied".Translate(runtimeSkin.label ?? runtimeSkin.defName, targetPawn.LabelShort),
                        MessageTypeDefOf.PositiveEvent,
                        false
                    );

                    if (applyPlan.warnings != null && applyPlan.warnings.Count > 0)
                    {
                        Messages.Message(applyPlan.warnings[0].Translate(), MessageTypeDefOf.CautionInput, false);
                    }
                }
                else
                {
                    string statusKey = string.IsNullOrWhiteSpace(applyPlan.statusMessage)
                        ? "CS_Studio_Err_ApplyFailedCheckLog"
                        : applyPlan.statusMessage;
                    ShowStatus(statusKey.Translate());
                    Log.Warning($"[CharacterStudio] 应用到目标角色失败: source={applyPlan.source}, pawn={targetPawn.LabelShort}, status={applyPlan.statusMessage ?? "<empty>"}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用到目标角色失败: {ex}");
                ShowStatus("CS_Studio_Err_ApplyFailedCheckLog".Translate());
            }
        }

        private void OnSpawnNewPawn()
        {
            // 图层修改工作流不支持生成新角色（无完整皮肤可应用）
            if (layerModificationWorkflowActive)
            {
                ShowStatus("图层修改工作流不支持生成新角色，请先导出补丁后在游戏中激活。");
                return;
            }

            if (Find.CurrentMap == null)
            {
                ShowStatus("CS_Studio_Err_NoMap".Translate());
                return;
            }

            if (workingSkin == null)
            {
                ShowStatus("CS_Studio_Err_ApplyFailedCheckLog".Translate());
                return;
            }

            Find.WindowStack.Add(new Dialog_SpawnCharacter(directSpawnSettings, settings =>
            {
                directSpawnSettings = settings?.Clone() ?? new CharacterSpawnSettings();
                directSpawnSettings.sourceMapForConditionCheck = Find.CurrentMap;

                try
                {
                    PawnSkinDef runtimeSkin = BuildRuntimeSkinForExecution();
                    ThingDef spawnRace = ResolveSpawnRaceForCurrentDesign(runtimeSkin);
                    CharacterApplicationPlan plan = BuildApplicationPlan(null, false, "SpawnNewPawnFromEditor");
                    plan.runtimeSkin = runtimeSkin;
                    plan.spawnAsNewPawn = true;
                    plan.spawnRaceDef = spawnRace;
                    plan.spawnPawnKind = CharacterSpawnUtility.ResolvePawnKindForRace(spawnRace);
                    plan.spawnFaction = Faction.OfPlayer;
                    plan.spawnMap = Find.CurrentMap;
                    plan.desiredSpawnCell = CharacterSpawnUtility.ResolveSpawnOrigin(plan.spawnMap, targetPawn);
                    plan.spawnSettings = directSpawnSettings.Clone();
                    plan.spawnSettings.sourceMapForConditionCheck = plan.spawnMap;
                    plan.characterDefinition = workingDocument.characterDefinition?.Clone() ?? new CharacterDefinition();
                    plan.characterDefinition.EnsureDefaults(runtimeSkin.defName ?? workingSkin.defName ?? "CS_Character", spawnRace, runtimeSkin.attributes);

                    if (CharacterApplicationExecutor.Execute(plan) && plan.targetPawn != null)
                    {
                        targetPawn = plan.targetPawn;
                        if (workingDocument != null)
                        {
                            workingDocument.preferredPreviewRaceDefName = targetPawn.def.defName;
                            workingDocument.preferredTargetRaceDefName = targetPawn.def.defName;
                        }

                        RefreshRenderTree();
                        ShowStatus("CS_Studio_SpawnedNewPawn".Translate(targetPawn.LabelShort));
                    }
                    else
                    {
                        string statusKey = string.IsNullOrWhiteSpace(plan.statusMessage)
                            ? "CS_Studio_Err_ApplyFailedCheckLog"
                            : plan.statusMessage;
                        ShowStatus(statusKey.Translate());
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[CharacterStudio] 生成新角色失败: {ex}");
                    ShowStatus("CS_Studio_Err_ApplyFailedCheckLog".Translate());
                }
            }, triggerSettings =>
            {
                directSpawnSettings = triggerSettings?.Clone() ?? new CharacterSpawnSettings();
                directSpawnSettings.sourceMapForConditionCheck = Find.CurrentMap;

                try
                {
                    PawnSkinDef runtimeSkin = BuildRuntimeSkinForExecution();
                    ThingDef spawnRace = ResolveSpawnRaceForCurrentDesign(runtimeSkin);
                    CharacterDefinition characterDefinition = workingDocument.characterDefinition?.Clone() ?? new CharacterDefinition();
                    string ownerCharacterDefName = runtimeSkin.defName ?? workingSkin.defName ?? "CS_Character";
                    characterDefinition.EnsureDefaults(ownerCharacterDefName, spawnRace, runtimeSkin.attributes);

                    CharacterSpawnProfileDef profile = new CharacterSpawnProfileDef
                    {
                        defName = $"CS_EditorPreviewProfile_{CharacterSpawnProfileRegistry.SanitizeDefName(ownerCharacterDefName)}",
                        label = characterDefinition.displayName,
                        ownerCharacterDefName = ownerCharacterDefName,
                        skinDefName = runtimeSkin.defName ?? ownerCharacterDefName,
                        raceDefName = spawnRace.defName,
                        pawnKindDefName = CharacterSpawnUtility.ResolvePawnKindForRace(spawnRace).defName,
                        forcePlayerFaction = true,
                        characterDefinition = characterDefinition.Clone()
                    };

                    CharacterRuntimeTriggerDef trigger = new CharacterRuntimeTriggerDef
                    {
                        defName = $"CS_EditorPreviewTrigger_{CharacterSpawnProfileRegistry.SanitizeDefName(ownerCharacterDefName)}",
                        label = characterDefinition.displayName,
                        ownerCharacterDefName = ownerCharacterDefName,
                        spawnProfileDefName = profile.defName,
                        spawnNearColonist = true,
                        requirePlayerHomeMap = false,
                        evaluationIntervalTicks = 1,
                        cooldownTicks = 0,
                        fireOncePerGame = false,
                        fireOncePerMap = false,
                        conditionLogic = CharacterSpawnConditionLogic.All,
                        requiredConditions = new System.Collections.Generic.List<CharacterRuntimeTriggerCondition>
                        {
                            new CharacterRuntimeTriggerCondition
                            {
                                conditionType = CharacterRuntimeTriggerConditionType.Always
                            }
                        },

                        spawnSettings = directSpawnSettings.Clone()
                    };

                    profile.characterDefinition.EnsureDefaults(profile.skinDefName, spawnRace, runtimeSkin.attributes);
                    CharacterStudioAPI.RegisterOrReplaceSkin(runtimeSkin);
                    CharacterStudioAPI.RegisterRuntimeSpawnProfile(profile);
                    CharacterStudioAPI.RegisterRuntimeTrigger(trigger);

                    Pawn? spawnedPawn = null;
                    Action<CharacterRuntimeTriggerDef, CharacterSpawnProfileDef?, Map, Pawn?> handler =
                        (firedTrigger, firedProfile, firedMap, firedPawn) =>
                        {
                            if (firedTrigger == trigger)
                                spawnedPawn = firedPawn;
                        };
                    CharacterStudioAPI.RuntimeTriggerFiredGlobal += handler;
                    try
                    {
                        bool executed = CharacterRuntimeTriggerExecutor.TryExecute(trigger, Find.CurrentMap);
                        if (!executed)
                        {
                            Log.Warning($"[CharacterStudio] 触发器执行返回 false，尝试直接使用 Profile 生成");
                            spawnedPawn = CharacterRuntimeTriggerExecutor.TrySpawnProfile(
                                profile, Find.CurrentMap, null, directSpawnSettings, "SpawnTriggerFromEditor");
                        }
                    }
                    finally
                    {
                        CharacterStudioAPI.RuntimeTriggerFiredGlobal -= handler;
                    }

                    if (spawnedPawn != null)
                    {
                        targetPawn = spawnedPawn;
                        if (workingDocument != null)
                        {
                            workingDocument.preferredPreviewRaceDefName = targetPawn.def.defName;
                            workingDocument.preferredTargetRaceDefName = targetPawn.def.defName;
                        }

                        RefreshRenderTree();
                        ShowStatus("CS_Studio_SpawnedNewPawn".Translate(targetPawn.LabelShort));
                    }
                    else
                    {
                        ShowStatus("CS_Studio_Err_ApplyFailedCheckLog".Translate());
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[CharacterStudio] 使用触发器生成角色失败: {ex}");
                    ShowStatus("CS_Studio_Err_ApplyFailedCheckLog".Translate());
                }
            }, isBusy => suspendHeavyPreviewWork = isBusy));
        }

        private ThingDef ResolveSpawnRaceForCurrentDesign(PawnSkinDef runtimeSkin)
        {
            if (runtimeSkin?.targetRaces != null)
            {
                foreach (string raceDefName in runtimeSkin.targetRaces)
                {
                    ThingDef? raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(raceDefName);
                    if (raceDef?.race != null)
                    {
                        return raceDef;
                    }
                }
            }

            return targetPawn?.def
                ?? mannequin?.CurrentPawn?.def
                ?? ThingDefOf.Human;
        }

        /// <summary>
        /// 从预览人偶的 RenderTree 中读取 Head 节点的 Z 偏移。
        /// 用于记录皮肤创建时的体型基准，后续渲染时自动补偿不同体型的差异。
        /// </summary>
        private float GetMannequinHeadOffsetZ()
        {
            Pawn? previewPawn = mannequin?.CurrentPawn;
            if (previewPawn == null)
                return 0f;

            try
            {
                // renderTree 是 PawnRenderer 的私有字段，通过反射获取
                var renderer = previewPawn.Drawer?.renderer;
                if (renderer == null) return 0f;
                var renderTreeField = typeof(PawnRenderer).GetField("renderTree",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                var renderTree = renderTreeField?.GetValue(renderer) as PawnRenderTree;
                if (renderTree == null)
                    return 0f;

                // 递归遍历节点树查找 Head 节点并获取其 debugOffset.z
                PawnRenderNode? headNode = FindHeadNode(renderTree.rootNode);
                if (headNode != null)
                {
                    return headNode.debugOffset.z;
                }
            }
            catch { }

            return 0f;
        }

        private static PawnRenderNode? FindHeadNode(PawnRenderNode? node)
        {
            if (node == null) return null;
            if (node.Props?.workerClass?.Name == "PawnRenderNodeWorker_Head")
                return node;
            if (node.children != null)
            {
                foreach (PawnRenderNode child in node.children)
                {
                    var found = FindHeadNode(child);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }
    }
}
