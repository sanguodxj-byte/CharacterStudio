using System.Collections.Generic;
using HarmonyLib;
using Verse;
using CharacterStudio.Rendering;
using CharacterStudio.Abilities;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 皮肤运行时工具
    /// 提供统一的应用/清除入口（必要时会为 Pawn 动态补齐 CompPawnSkin）
    /// </summary>
    public static class PawnSkinRuntimeUtility
    {
        public static bool ApplySkinToPawn(Pawn? pawn, PawnSkinDef? skin)
        {
            if (pawn == null) return false;

            var comp = pawn.GetComp<CompPawnSkin>() ?? TryAddSkinComp(pawn);
            if (comp == null)
            {
                Log.Warning($"[CharacterStudio] Pawn 缺少 CompPawnSkin: {pawn.LabelShort}");
                return false;
            }

            // 使用静默赋值避免 setter 在此处触发 RequestRenderRefresh，
            // 后续 RefreshHiddenNodes + ForceRebuildRenderTree 负责完整刷新，
            // 防止同一帧对同一 Pawn 产生三次冗余重绘。
            comp.SetActiveSkinSilent(skin);

            // 皮肤切换时清除表情图形缓存，避免新皮肤使用旧贴图
            CharacterStudio.Rendering.PawnRenderNodeWorker_FaceComponent.ClearCache();

            try
            {
                // RefreshHiddenNodes 在主线程执行注入（纹理加载必须在主线程）
                Patch_PawnRenderTree.RefreshHiddenNodes(pawn);
                Patch_PawnRenderTree.ForceRebuildRenderTree(pawn);
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[CharacterStudio] ForceRebuildRenderTree 失败，回退到 RequestRenderRefresh: {ex.Message}");
                comp.RequestRenderRefresh();
            }

            // 同步授予皮肤中的技能给 Pawn
            if (skin != null)
                AbilityGrantUtility.GrantSkinAbilitiesToPawn(pawn, skin);

            return true;
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

            // ClearSkin 内部已直接置空字段，不走 setter，无需静默赋值
            comp.ClearSkin();

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

            // 撤销所有 CS 技能
            AbilityGrantUtility.RevokeAllCSAbilitiesFromPawn(pawn);

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
