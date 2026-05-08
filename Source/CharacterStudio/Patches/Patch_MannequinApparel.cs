using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Patches
{
    /// <summary>
    /// 允许预览人偶穿戴任意种族的服装。
    /// 拦截 Pawn_ApparelTracker.CanWear，对人偶 pawn 直接放行。
    /// 同时 patch ApparelGraphicRecordGetter.TryGetGraphicApparel，
    /// 当带 bodyType 后缀的路径找不到纹理时，回退到不带后缀的原始路径。
    /// </summary>
    public static class Patch_MannequinApparel
    {
        public static void Apply(Harmony harmony)
        {
            var canWearMethod = AccessTools.Method(
                typeof(Pawn_ApparelTracker),
                "CanWear",
                new[] { typeof(ThingDef), typeof(bool) });

            if (canWearMethod == null)
            {
                Log.Warning("[CharacterStudio] Patch_MannequinApparel: 未找到 Pawn_ApparelTracker.CanWear，跳过注册。");
            }
            else
            {
                harmony.Patch(canWearMethod,
                    prefix: new HarmonyMethod(typeof(Patch_MannequinApparel), nameof(CanWear_Prefix)));
            }

            // Patch ApparelGraphicRecordGetter.TryGetGraphicApparel
            // 当人偶 Pawn 穿戴装备时，如果拼接了 _Male/_Female 后缀的路径找不到纹理，
            // 回退到不带后缀的原始 wornGraphicPath。
            var tryGetGraphicMethod = AccessTools.Method(
                typeof(ApparelGraphicRecordGetter),
                "TryGetGraphicApparel",
                new[] { typeof(Apparel), typeof(BodyTypeDef), typeof(bool), typeof(ApparelGraphicRecord).MakeByRefType() });

            if (tryGetGraphicMethod == null)
            {
                Log.Warning("[CharacterStudio] Patch_MannequinApparel: 未找到 ApparelGraphicRecordGetter.TryGetGraphicApparel，跳过注册。");
            }
            else
            {
                harmony.Patch(tryGetGraphicMethod,
                    postfix: new HarmonyMethod(typeof(Patch_MannequinApparel), nameof(TryGetGraphicApparel_Postfix)));
            }

            Log.Message("[CharacterStudio] Patch_MannequinApparel 已注册。");
        }

        private static bool CanWear_Prefix(Pawn_ApparelTracker __instance, ref AcceptanceReport __result)
        {
            if (__instance.pawn != null && UI.MannequinManager.IsMannequinPawn(__instance.pawn))
            {
                __result = true;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 当人偶 Pawn 的装备 Graphic 为 BadTex（纹理未找到）时，
        /// 尝试用不带 bodyType 后缀的原始路径重新获取。
        /// </summary>
        private static void TryGetGraphicApparel_Postfix(
            Apparel apparel,
            BodyTypeDef bodyType,
            bool forStatue,
            ref ApparelGraphicRecord __result)
        {
            // 只处理 Graphic 无效的情况（null 或 BadTex 表示路径未找到）
            if (__result.graphic == null || (__result.graphic.MatSingle != null && __result.graphic.MatSingle.mainTexture == BaseContent.BadTex))
            {
                if (apparel?.WornGraphicPath == null || apparel.WornGraphicPath == BaseContent.PlaceholderImagePath)
                    return;

                // 尝试不带 bodyType 后缀的路径
                Shader shader = ShaderDatabase.Cutout;
                if (!forStatue)
                {
                    if (apparel.StyleDef?.graphicData?.shaderType != null)
                        shader = apparel.StyleDef.graphicData.shaderType.Shader;
                    else if ((apparel.StyleDef == null && apparel.def.apparel?.useWornGraphicMask == true)
                          || (apparel.StyleDef != null && apparel.StyleDef.UseWornGraphicMask))
                        shader = ShaderDatabase.CutoutComplex;
                }

                Graphic fallbackGraphic = GraphicDatabase.Get<Graphic_Multi>(
                    apparel.WornGraphicPath,
                    shader,
                    apparel.def.graphicData?.drawSize ?? Vector2.one,
                    apparel.DrawColor);

                if (fallbackGraphic != null && fallbackGraphic.MatSingle != null && fallbackGraphic.MatSingle.mainTexture != BaseContent.BadTex)
                {
                    __result = new ApparelGraphicRecord(fallbackGraphic, apparel);
                }
            }
        }
    }
}
