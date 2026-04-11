using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using CharacterStudio.Attributes;
using Verse;
using CharacterStudio.Rendering;
using CharacterStudio.Abilities;
using UnityEngine;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 皮肤运行时工具
    /// 提供统一的应用/清除入口（必要时会为 Pawn 动态补齐 CompPawnSkin）
    /// </summary>
    public static class PawnSkinRuntimeUtility
    {
        public static bool ApplySkinToPawn(
            Pawn? pawn,
            PawnSkinDef? skin,
            bool fromDefaultRaceBinding = false,
            bool previewMode = false,
            string applicationSource = "")
        {
            if (pawn == null) return false;

            var comp = pawn.GetComp<CompPawnSkin>() ?? TryAddSkinComp(pawn);
            if (comp == null)
            {
                Log.Warning($"[CharacterStudio] Pawn 缺少 CompPawnSkin: {pawn.LabelShort}");
                return false;
            }

            var preparedSkin = PawnSkinRuntimeValidator.PrepareForRuntime(skin);

            // 在真正清缓存并重建渲染树之前，先同步预热本次皮肤会用到的外部纹理与材质。
            // 这样可以显著降低 Apply -> RefreshHiddenNodes 期间命中 Graphic_Runtime 透明回退材质的概率。
            PrewarmExternalSkinAssets(preparedSkin);

            // 使用静默赋值避免 setter 在此处触发 RequestRenderRefresh，
            // 后续 RefreshHiddenNodes + ForceRebuildRenderTree 负责完整刷新，
            // 防止同一帧对同一 Pawn 产生三次冗余重绘。
            var writeResult = comp.SetActiveSkinWithSource(
                preparedSkin,
                fromDefaultRaceBinding,
                previewMode,
                applicationSource);
            if (!writeResult.skinChanged && !writeResult.sourceChanged)
                return true;

            // 皮肤切换时清除表情图形缓存，避免新皮肤使用旧贴图
            CharacterStudio.Rendering.PawnRenderNodeWorker_FaceComponent.ClearCache();
            CharacterStudio.Rendering.PawnRenderNodeWorker_EyeDirection.ClearCache();
            CharacterStudio.Rendering.PawnRenderNodeWorker_CustomLayer.ClearCache();

            try
            {
                // RefreshHiddenNodes 已包含完整的注入流程（移除旧节点 → 注入新节点 → SetDirty）
                // P5: 不再调用 ForceRebuildRenderTree，它会重置 setupComplete=false 导致
                // TrySetupGraphIfNeeded 再次触发完整注入，造成双重注入性能浪费
                Patch_PawnRenderTree.RefreshHiddenNodes(pawn);
                pawn.Drawer.renderer.SetAllGraphicsDirty();
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[CharacterStudio] RefreshHiddenNodes 失败，回退到 RequestRenderRefresh: {ex.Message}");
                comp.RequestRenderRefresh();
            }

            // 同步当前生效的技能装配；显式 loadout 优先，其次回退到皮肤模板。
            AbilityLoadoutRuntimeUtility.GrantEffectiveLoadoutToPawn(pawn);
            CharacterAttributeBuffService.SyncAttributeBuff(pawn);

            return true;
        }

        private static void PrewarmExternalSkinAssets(PawnSkinDef? skin)
        {
            if (skin == null)
                return;

            foreach (string texturePath in EnumerateExternalTexturePaths(skin))
            {
                try
                {
                    Texture2D? texture = RuntimeAssetLoader.LoadTextureRaw(texturePath);
                    if (texture != null)
                    {
                        RuntimeAssetLoader.GetMaterialForTexture(texture);
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Warning($"[CharacterStudio] 预热外部纹理失败，已继续应用流程: {texturePath} ({ex.Message})");
                }
            }
        }

        private static IEnumerable<string> EnumerateExternalTexturePaths(PawnSkinDef skin)
        {
            HashSet<string> yielded = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            List<string> pendingExternalPaths = new List<string>();

            void YieldIfExternal(string? path)
            {
                if (string.IsNullOrWhiteSpace(path))
                    return;

                string nonNullPath = path ?? string.Empty;
                if (!RuntimeAssetLoader.LooksLikeExternalTexturePath(nonNullPath))
                    return;

                string resolvedPath = RuntimeAssetLoader.ResolveTexturePathForLoad(nonNullPath);
                if (!yielded.Add(resolvedPath))
                    return;

                pendingExternalPaths.Add(resolvedPath);
            }

            if (!string.IsNullOrWhiteSpace(skin.previewTexPath))
                YieldIfExternal(skin.previewTexPath);

            if (skin.layers != null)
            {
                foreach (PawnLayerConfig layer in skin.layers)
                {
                    if (layer == null)
                        continue;

                    YieldIfExternal(layer.texPath);
                    YieldIfExternal(layer.maskTexPath);
                }
            }

            if (skin.equipments != null)
            {
                foreach (CharacterEquipmentDef equipment in skin.equipments)
                {
                    if (equipment == null)
                        continue;

                    YieldIfExternal(equipment.previewTexPath);
                    YieldIfExternal(equipment.worldTexPath);
                    YieldIfExternal(equipment.wornTexPath);
                    YieldIfExternal(equipment.maskTexPath);
                    YieldIfExternal(equipment.renderData?.texPath);
                    YieldIfExternal(equipment.renderData?.maskTexPath);
                }
            }

            if (skin.weaponRenderConfig?.carryVisual != null)
            {
                WeaponCarryVisualConfig carryVisual = skin.weaponRenderConfig.carryVisual;
                YieldIfExternal(carryVisual.texUndrafted);
                YieldIfExternal(carryVisual.texDrafted);
                YieldIfExternal(carryVisual.texCasting);
            }

            if (skin.faceConfig?.layeredParts != null)
            {
                foreach (var part in skin.faceConfig.layeredParts.Where(p => p != null))
                {
                    YieldIfExternal(part.texPath);
                    YieldIfExternal(part.texPathSouth);
                    YieldIfExternal(part.texPathEast);
                    YieldIfExternal(part.texPathNorth);
                }
            }

            return pendingExternalPaths;
        }

        public static bool ClearSkinFromPawn(Pawn? pawn)
        {
            if (pawn == null) return false;

            var comp = pawn.GetComp<CompPawnSkin>() ?? TryAddSkinComp(pawn);
            if (comp == null)
            {
                Log.Warning($"[CharacterStudio] Pawn 缺少 CompPawnSkin: {pawn.LabelShort}");
                return false;
            }

            // 仅在真实皮肤状态发生变化时，才继续执行运行时清理副作用。
            var writeResult = comp.ClearSkinWithResult();
            if (!writeResult.skinChanged)
                return true;

            // 清除皮肤时同样需要清空表情图形缓存，避免继续复用旧贴图。
            CharacterStudio.Rendering.PawnRenderNodeWorker_FaceComponent.ClearCache();
            CharacterStudio.Rendering.PawnRenderNodeWorker_EyeDirection.ClearCache();
            CharacterStudio.Rendering.PawnRenderNodeWorker_CustomLayer.ClearCache();

            try
            {
                Patch_PawnRenderTree.RefreshHiddenNodes(pawn);
                Patch_PawnRenderTree.ForceRebuildRenderTree(pawn);
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[CharacterStudio] ClearSkin ForceRebuildRenderTree 失败: {ex.Message}");
                comp.RequestRenderRefresh();
            }

            // 重新同步当前生效的技能装配；若无显式 loadout，则会自动撤销技能。
            AbilityLoadoutRuntimeUtility.GrantEffectiveLoadoutToPawn(pawn);
            CharacterAttributeBuffService.SyncAttributeBuff(pawn);

            return true;
        }

        private static CompPawnSkin? TryAddSkinComp(Pawn pawn)
        {
            var compsField = AccessTools.Field(typeof(ThingWithComps), "comps");
            if (compsField == null) return null;

            var comps = compsField.GetValue(pawn) as List<ThingComp>;
            if (comps == null)
            {
                comps = new List<ThingComp>();
                compsField.SetValue(pawn, comps);
            }

            foreach (var thingComp in comps)
            {
                if (thingComp is CompPawnSkin found) return found;
            }

            var comp = new CompPawnSkin
            {
                parent = pawn
            };
            comps.Add(comp);
            comp.Initialize(new CompProperties_PawnSkin());
            comp.PostSpawnSetup(pawn.Spawned);
            return comp;
        }
    }
}
