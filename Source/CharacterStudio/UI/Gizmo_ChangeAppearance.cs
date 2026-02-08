using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 外观更换 Gizmo
    /// 允许玩家为殖民者选择已加载的皮肤
    /// </summary>
    public class Gizmo_ChangeAppearance : Command
    {
        private Pawn pawn;

        public Gizmo_ChangeAppearance(Pawn pawn)
        {
            this.pawn = pawn;
            this.defaultLabel = "CS_Gizmo_Appearance".Translate();
            this.defaultDesc = "CS_Gizmo_Appearance_Desc".Translate();
            this.icon = ContentFinder<Texture2D>.Get("UI/Commands/CS_Appearance", false) 
                ?? ContentFinder<Texture2D>.Get("UI/Designators/Strip", true);
        }

        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
        {
            get
            {
                // 获取所有可用皮肤
                var availableSkins = DefDatabase<PawnSkinDef>.AllDefs
                    .Where(s => s.IsValidForPawn(pawn))
                    .OrderBy(s => s.label)
                    .ToList();

                // 添加清除选项
                yield return new FloatMenuOption(
                    "CS_Appearance_ClearSkin".Translate(),
                    () => ClearSkin()
                );

                // 添加皮肤选项
                foreach (var skin in availableSkins)
                {
                    string label = skin.label ?? skin.defName;
                    
                    // 检查是否是当前皮肤
                    var currentSkin = GetCurrentSkin();
                    if (currentSkin == skin)
                    {
                        label = $"✓ {label}";
                    }

                    yield return new FloatMenuOption(
                        label,
                        () => ApplySkin(skin)
                    );
                }

                if (!availableSkins.Any())
                {
                    yield return new FloatMenuOption(
                        "CS_Appearance_NoSkins".Translate(),
                        null
                    );
                }

                // 添加打开编辑器选项
                yield return new FloatMenuOption(
                    "CS_Studio_OpenEditor".Translate(),
                    () => OpenEditor()
                );
            }
        }

        private void OpenEditor()
        {
            Find.WindowStack.Add(new Dialog_SkinEditor());
        }

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            
            // 左键点击打开菜单
            var options = RightClickFloatMenuOptions.ToList();
            if (options.Any())
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private void ApplySkin(PawnSkinDef skin)
        {
            var comp = pawn.GetComp<CompPawnSkin>();
            if (comp == null)
            {
                // 动态添加组件（如果可能）
                Log.Warning($"[CharacterStudio] Pawn {pawn.LabelShort} 没有 CompPawnSkin，无法应用皮肤");
                Messages.Message("CS_Appearance_NoComp".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            comp.ActiveSkin = skin;
            Messages.Message(
                "CS_Appearance_Applied".Translate(skin.label ?? skin.defName, pawn.LabelShort),
                MessageTypeDefOf.PositiveEvent,
                false
            );
        }

        private void ClearSkin()
        {
            var comp = pawn.GetComp<CompPawnSkin>();
            if (comp == null) return;

            comp.ClearSkin();
            Messages.Message(
                "CS_Appearance_Cleared".Translate(pawn.LabelShort),
                MessageTypeDefOf.NeutralEvent,
                false
            );
        }

        private PawnSkinDef? GetCurrentSkin()
        {
            var comp = pawn.GetComp<CompPawnSkin>();
            return comp?.ActiveSkin;
        }
    }

    /// <summary>
    /// Harmony 补丁：为殖民者添加外观 Gizmo
    /// </summary>
    [StaticConstructorOnStartup]
    public static class Patch_PawnGizmos
    {
        static Patch_PawnGizmos()
        {
            // 补丁在 ModEntryPoint 中应用
        }

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            var originalMethod = HarmonyLib.AccessTools.Method(typeof(Pawn), "GetGizmos");
            var postfixMethod = HarmonyLib.AccessTools.Method(typeof(Patch_PawnGizmos), nameof(GetGizmos_Postfix));

            if (originalMethod != null && postfixMethod != null)
            {
                harmony.Patch(originalMethod, postfix: new HarmonyLib.HarmonyMethod(postfixMethod));
                Log.Message("[CharacterStudio] Pawn.GetGizmos 补丁已应用");
            }
        }

        public static void Unpatch(HarmonyLib.Harmony harmony)
        {
            try
            {
                var originalMethod = HarmonyLib.AccessTools.Method(typeof(Pawn), "GetGizmos");
                if (originalMethod != null)
                {
                    harmony.Unpatch(originalMethod, HarmonyPatchType.Postfix, harmony.Id);
                    Log.Message("[CharacterStudio] Pawn.GetGizmos 补丁已移除");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[CharacterStudio] 移除 Pawn.GetGizmos 补丁时出错: {ex}");
            }
        }

        private static void GetGizmos_Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            // 只对玩家殖民者显示
            if (!__instance.IsColonistPlayerControlled) return;
            if (!__instance.RaceProps.Humanlike) return;

            // 添加外观 Gizmo
            var originalGizmos = __result.ToList();
            originalGizmos.Add(new Gizmo_ChangeAppearance(__instance));
            __result = originalGizmos;
        }
    }
}