using HarmonyLib;
using Verse;
using UnityEngine;
using CharacterStudio.Abilities;

namespace CharacterStudio.Rendering
{
    public static class Patch_PawnRenderer
    {
        public static void Apply(Harmony harmony)
        {
            // 拦截 GetBodyDrawPos。这是原版 PawnRenderer 计算身体渲染中心点的地方。
            // 通过修改此返回值，我们可以平滑地将角色视觉拉高，且能自动带动所有 RenderNode（衣服、头、武器等）。
            var target = AccessTools.Method(typeof(PawnRenderer), "GetBodyDrawPos");
            var postfix = AccessTools.Method(typeof(Patch_PawnRenderer), nameof(GetBodyDrawPos_Postfix));
            
            if (target != null && postfix != null)
            {
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            }
        }

        public static void GetBodyDrawPos_Postfix(Pawn ___pawn, ref Vector3 __result)
        {
            if (___pawn == null || !___pawn.Spawned) return;

            var abilityComp = ___pawn.GetComp<CompCharacterAbilityRuntime>();
            if (abilityComp == null) return;

            float height = abilityComp.TotalFlightHeight;
            if (height > 0.001f)
            {
                // 将视觉高度应用到 Z 轴（屏幕垂直方向）
                __result.z += height;

                // 层级提升：只要进入飞行状态，就将渲染层级提升到极高水平。
                // 使用 MetaOverlays (约 100) 以上的层级，确保角色盖过所有建筑、屋顶和大部分特效。
                // 增加少量与 height 相关的偏移以保证多个飞行单位之间的高度排序正确。
                __result.y = AltitudeLayer.MetaOverlays.AltitudeFor() + 5f + (height * 0.01f);
            }
        }
    }
}
