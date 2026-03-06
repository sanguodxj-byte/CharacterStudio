using System.Collections.Generic;
using HarmonyLib;
using Verse;
using CharacterStudio.Rendering;

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

            comp.ActiveSkin = skin;

            try
            {
                // RefreshHiddenNodes 在主线程执行注入（纹理加载必须在主线程）
                // P1 守卫（CheckForCustomNodes）会阻止 Postfix 重复注入
                Patch_PawnRenderTree.RefreshHiddenNodes(pawn);
                Patch_PawnRenderTree.ForceRebuildRenderTree(pawn);
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[CharacterStudio] ForceRebuildRenderTree 失败，回退到 RequestRenderRefresh: {ex.Message}");
                comp.RequestRenderRefresh();
            }

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
