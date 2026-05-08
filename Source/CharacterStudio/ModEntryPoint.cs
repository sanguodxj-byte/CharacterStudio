using System.Linq;
using System.Reflection;
using HarmonyLib;
using CharacterStudio.Core;
using CharacterStudio.Rendering;
using Verse;

namespace CharacterStudio
{
    /// <summary>
    /// 模组入口点
    /// 初始化 Harmony 补丁和核心系统
    /// </summary>
    [StaticConstructorOnStartup]
    public static class ModEntryPoint
    {
        /// <summary>
        /// Harmony 实例 ID
        /// </summary>
        public const string HarmonyId = "CharacterStudio.Main";

        /// <summary>
        /// Harmony 实例
        /// </summary>
        public static Harmony? HarmonyInstance { get; private set; }

        /// <summary>
        /// 模组版本
        /// </summary>
        public static string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

        static ModEntryPoint()
        {
            // 初始化 Harmony
            InitializeHarmony();

            // 先屏蔽普通信息日志，避免初始化过程刷屏
            ApplyLogSilencer();

            // 加载运行时皮肤定义
            PawnSkinDefRegistry.LoadFromConfig();
            CharacterSpawnProfileRegistry.LoadFromConfig();
            CharacterRuntimeTriggerRegistry.LoadFromConfig();

            // 从项目角色文件恢复运行时角色资产注册（spawn profile / runtime trigger），
            // 确保编辑器保存到 Config 的项目角色在重启与读档后继续可用。
            // 通过 ExecuteWhenFinished 延迟到 BakeStaticAtlases 完成后执行，避免启动时崩溃。
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                CharacterProjectRegistry.LoadProjectCharactersFromConfig();
            });

            // 将 DefDatabase 中由 XML Defs 加载的 PawnSkinDef（如导出模组提供的）同步到运行时注册表，
            // 使 GetDefaultSkinForRace 等运行时查询能找到这些 Def，
            // 从而让 PawnSkinBootstrapComponent 在加载存档时自动应用导出模组的皮肤到匹配种族的 Pawn。
            PawnSkinDefRegistry.SyncFromDefDatabase();

            // 预热运行时技能 Def，确保旧存档中的 CS_RT_* AbilityDef 引用在读档期即可被解析
            Abilities.AbilityGrantUtility.WarmupAllRuntimeAbilityDefs();

            // 预热字符串规范化缓存，避免渲染管线首次调用时分配
            Core.PawnFaceConfig.PrewarmOverlayIdCache();
            Core.PawnFaceConfig.PrewarmSemanticKeyCache();

            // 预热 DefModExtension 缓存，将热路径的 GetModExtension O(N) 遍历降为 O(1) 字典查找
            Core.DefModExtensionCache.BuildAll();

            // 验证装备定义 XML 序列化/反序列化往返一致性
            VerifyEquipmentDefRoundtrip();

            // 日志：列出已注册的 CS_PawnKind_* PawnKindDef
            LogRegisteredPawnKinds();

            // 应用补丁
            ApplyPatches();
        }

        /// <summary>
        /// 初始化 Harmony
        /// </summary>
        private static void InitializeHarmony()
        {
            try
            {
                HarmonyInstance = new Harmony(HarmonyId);
            }
            catch (System.Exception ex)
            {
                Log.Error($"[CharacterStudio] 初始化 Harmony 失败: {ex}");
            }
        }

        private static void ApplyLogSilencer()
        {
            if (HarmonyInstance == null)
                return;

            try
            {
                Patch_LogSilencer.Apply(HarmonyInstance);
            }
            catch (System.Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用日志屏蔽补丁时出错: {ex}");
            }
        }

        /// <summary>
        /// 单独应用某个补丁，避免单个补丁失败阻断后续补丁注册。
        /// </summary>
        private static void ApplyPatch(string patchName, System.Action applyAction)
        {
            try
            {
                applyAction();
            }
            catch (System.Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用补丁失败 [{patchName}]: {ex}");
            }
        }

        /// <summary>
        /// 应用所有补丁
        /// </summary>
        private static void ApplyPatches()
        {
            var harmony = HarmonyInstance;
            if (harmony == null)
            {
                Log.Error("[CharacterStudio] Harmony 实例为空，无法应用补丁");
                return;
            }

            ApplyPatch("PawnRenderTree", () => Patch_PawnRenderTree.Apply(harmony));
            ApplyPatch("PawnRenderer", () => Rendering.Patch_PawnRenderer.Apply(harmony));
            ApplyPatch("FlightState", () => Patches.Patch_FlightState.Apply(harmony));
            ApplyPatch("GameComponentBootstrap", () => Patches.Patch_GameComponentBootstrap.Apply(harmony));
            ApplyPatch("PawnGizmos", () => UI.Patch_PawnGizmos.Apply(harmony));
            ApplyPatch("AbilityGizmos", () => UI.Patch_AbilityGizmos.Apply(harmony));
            ApplyPatch("PlaySettingsAbilityHotkeys", () => UI.Patch_PlaySettings_AbilityHotkeys.Apply(harmony));
            ApplyPatch("UIRootAbilityHotkeys", () => UI.Patch_UIRoot_AbilityHotkeys.Apply(harmony));
            ApplyPatch("AnimationRender", () => Rendering.Patch_AnimationRender.Apply(harmony));
            ApplyPatch("RaceLabel", () => Patches.Patch_RaceLabel.Apply(harmony));
            ApplyPatch("CharacterAttributeBuffStat", () => Patches.Patch_CharacterAttributeBuffStat.Apply(harmony));
            ApplyPatch("AbilityTimeStop", () => Abilities.Patch_AbilityTimeStop.Apply(harmony));
            ApplyPatch("AbilityRuntimeTick", () => Abilities.Patch_AbilityRuntimeTick.Apply(harmony));
            ApplyPatch("ProjectileBezierWallIntercept", () => Patches.Patch_ProjectileBezierWallIntercept.Apply(harmony));
            ApplyPatch("MannequinApparel", () => Patches.Patch_MannequinApparel.Apply(harmony));

            Log.Message("[CharacterStudio] 补丁应用流程完成");
        }

        /// <summary>
        /// 记录已注册的 CS_PawnKind_* PawnKindDef，便于排查导出模组是否正确加载
        /// </summary>
        private static void LogRegisteredPawnKinds()
        {
            try
            {
                var csPawnKinds = Verse.DefDatabase<Verse.PawnKindDef>.AllDefsListForReading
                    .Where(def => def.defName != null && def.defName.StartsWith("CS_PawnKind_"))
                    .ToList();

                if (csPawnKinds.Count == 0)
                {
                    Log.Message("[CharacterStudio] 未发现已注册的 CS_PawnKind_* PawnKindDef");
                    return;
                }

                Log.Message($"[CharacterStudio] 已注册 {csPawnKinds.Count} 个 CS_PawnKind_* PawnKindDef：\n" +
                    string.Join("\n", csPawnKinds.Select(def =>
                    {
                        string race = def.race?.defName ?? "null";
                        string label = def.label ?? def.defName;
                        return $"  - {def.defName} (race={race}, label={label})";
                    })));
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[CharacterStudio] 枚举 PawnKindDef 时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 验证装备定义的 XML 序列化/反序列化往返一致性。
        /// 创建一个包含典型数据的 CharacterEquipmentDef，通过 XML 往返后验证所有字段正确。
        /// </summary>
        private static void VerifyEquipmentDefRoundtrip()
        {
            try
            {
                // 1. 构造一个包含所有典型字段的装备定义
                var original = new Core.CharacterEquipmentDef
                {
                    defName = "CS_Test_Equipment",
                    label = "Test Equipment",
                    description = "A test equipment for verifying XML roundtrip",
                    enabled = true,
                    slotTag = "Apparel",
                    thingDefName = "CS_Test_Equipment_Thing",
                    parentThingDefName = "ApparelMakeableBase",
                    shaderDefName = "CutoutComplex",
                    allowCrafting = true,
                    allowTrading = true,
                    marketValue = 500f,
                    wornTexPath = "Things/TestEquipment/Worn",
                    worldTexPath = "Things/TestEquipment/World",
                    maskTexPath = "Things/TestEquipment/Mask",
                    renderData = new Core.CharacterEquipmentRenderData
                    {
                        layerName = "TestLayer",
                        texPath = "Things/TestEquipment/Texture",
                        anchorTag = "Head",
                        anchorPath = "Body/Head",
                        shaderDefName = "CutoutComplex",
                        offset = new UnityEngine.Vector3(0.1f, 0f, 0.2f),
                        offsetEast = new UnityEngine.Vector3(0.05f, 0f, 0.1f),
                        offsetNorth = new UnityEngine.Vector3(-0.1f, 0f, 0.05f),
                        drawOrder = 55f,
                        scale = new UnityEngine.Vector2(1.1f, 1.1f),
                        colorSource = Core.LayerColorSource.PawnHair,
                        visible = true,
                        flipHorizontal = false
                    }
                };
                original.tags.Add("TestTag");
                original.apparelLayers.Add("OnSkin");
                original.bodyPartGroups.Add("Torso");
                original.apparelTags.Add("TestApparel");

                // 2. 通过 EnsureDefaults 确保数据合法
                original.EnsureDefaults();

                // 3. 序列化为 XML（通过 SkinSaver 使用的公共序列化路径）
                var definition = new Core.CharacterDefinition
                {
                    defName = "CS_Test_CharacterDef",
                    equipments = new System.Collections.Generic.List<Core.CharacterEquipmentDef> { original }
                };

                // 4. 使用 CharacterDefinitionXmlUtility 做完整往返
                var xmlElement = Core.CharacterDefinitionXmlUtility.ToXElement("CharacterDefinition", definition, false);

                // 保存到临时文件再加载回来（验证完整的文件 I/O 路径）
                string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CS_Test_EquipmentRoundtrip.xml");
                Core.CharacterDefinitionXmlUtility.Save(definition, tempFile);

                // 5. 反序列化回来
                var loaded = Core.CharacterDefinitionXmlUtility.Load(tempFile);
                if (loaded == null)
                {
                    Log.Error("[CharacterStudio] 装备验证失败：ParseFromXElement 返回 null");
                    return;
                }

                if (loaded.equipments == null || loaded.equipments.Count == 0)
                {
                    Log.Error("[CharacterStudio] 装备验证失败：equipments 为空");
                    return;
                }

                var loadedEquip = loaded.equipments[0];

                // 6. 逐字段验证
                int errors = 0;
                void Check(string fieldName, string expected, string actual)
                {
                    if (expected != actual)
                    {
                        Log.Warning($"[CharacterStudio] 装备验证字段不匹配 {fieldName}: expected='{expected}', actual='{actual}'");
                        errors++;
                    }
                }

                Check("defName", original.defName, loadedEquip.defName);
                Check("label", original.label, loadedEquip.label);
                Check("description", original.description, loadedEquip.description);
                Check("thingDefName", original.thingDefName, loadedEquip.thingDefName);
                Check("slotTag", original.slotTag, loadedEquip.slotTag);
                Check("shaderDefName", original.shaderDefName, loadedEquip.shaderDefName);
                Check("wornTexPath", original.wornTexPath, loadedEquip.wornTexPath);
                Check("worldTexPath", original.worldTexPath, loadedEquip.worldTexPath);
                Check("maskTexPath", original.maskTexPath, loadedEquip.maskTexPath);

                if (original.enabled != loadedEquip.enabled) { Log.Warning("[CharacterStudio] 装备验证字段不匹配: enabled"); errors++; }
                if (original.allowCrafting != loadedEquip.allowCrafting) { Log.Warning("[CharacterStudio] 装备验证字段不匹配: allowCrafting"); errors++; }
                if (System.Math.Abs(original.marketValue - loadedEquip.marketValue) > 0.01f) { Log.Warning($"[CharacterStudio] 装备验证字段不匹配: marketValue ({original.marketValue} vs {loadedEquip.marketValue})"); errors++; }

                // renderData 验证
                if (loadedEquip.renderData == null)
                {
                    Log.Error("[CharacterStudio] 装备验证失败：renderData 为 null");
                    errors++;
                }
                else
                {
                    Check("renderData.layerName", original.renderData.layerName, loadedEquip.renderData.layerName);
                    Check("renderData.texPath", original.renderData.texPath, loadedEquip.renderData.texPath);
                    Check("renderData.anchorTag", original.renderData.anchorTag, loadedEquip.renderData.anchorTag);
                    Check("renderData.anchorPath", original.renderData.anchorPath, loadedEquip.renderData.anchorPath);
                    Check("renderData.shaderDefName", original.renderData.shaderDefName, loadedEquip.renderData.shaderDefName);
                    if (System.Math.Abs(original.renderData.drawOrder - loadedEquip.renderData.drawOrder) > 0.01f)
                    {
                        Log.Warning($"[CharacterStudio] 装备验证字段不匹配: renderData.drawOrder ({original.renderData.drawOrder} vs {loadedEquip.renderData.drawOrder})");
                        errors++;
                    }
                    if (original.renderData.colorSource != loadedEquip.renderData.colorSource)
                    {
                        Log.Warning($"[CharacterStudio] 装备验证字段不匹配: renderData.colorSource ({original.renderData.colorSource} vs {loadedEquip.renderData.colorSource})");
                        errors++;
                    }
                }

                if (errors == 0)
                {
                    Log.Message("[CharacterStudio] ✅ 装备定义 XML 往返验证通过：所有字段正确序列化/反序列化");
                }
                else
                {
                    Log.Warning($"[CharacterStudio] ⚠️ 装备定义 XML 往返验证完成，发现 {errors} 个字段不匹配");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[CharacterStudio] 装备验证异常：{ex}");
            }
            finally
            {
                try
                {
                    string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CS_Test_EquipmentRoundtrip.xml");
                    if (System.IO.File.Exists(tempFile))
                        System.IO.File.Delete(tempFile);
                }
                catch { }
            }
        }
    }
}
