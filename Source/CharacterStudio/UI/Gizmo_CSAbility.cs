﻿using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    /// <summary>
    /// CS 技能占位 Gizmo
    /// 显示在已绑定皮肤且皮肤含技能的 Pawn 底部控制栏
    /// </summary>
    public class Gizmo_CSAbility : Gizmo
    {
        private readonly Pawn pawn;
        private readonly ModularAbilityDef modAbility;
        private readonly AbilityDef? runtimeDef;
        private readonly Texture2D? iconTex;

        private static readonly Color ColorReady  = new Color(0.3f, 0.7f, 1.0f, 0.9f);
        private static readonly Color ColorOnCD   = new Color(0.4f, 0.4f, 0.4f, 0.7f);
        private static readonly Color ColorBg     = new Color(0.08f, 0.08f, 0.12f, 0.95f);
        private static readonly Color ColorBorder = new Color(0.4f, 0.6f, 0.9f, 0.8f);

        public Gizmo_CSAbility(Pawn pawn, ModularAbilityDef modAbility)
        {
            this.pawn       = pawn;
            this.modAbility = modAbility;
            this.runtimeDef = AbilityGrantUtility.GetRuntimeAbilityDef(modAbility.defName);

            if (!string.IsNullOrEmpty(modAbility.iconPath))
                iconTex = ContentFinder<Texture2D>.Get(modAbility.iconPath, false);
            iconTex ??= ContentFinder<Texture2D>.Get("UI/Abilities/Shoot", false);

            this.Order = 10f;
        }

        public override float GetWidth(float maxWidth) => 75f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect outerRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);

            Widgets.DrawBoxSolid(outerRect, ColorBg);

            bool onCooldown = IsOnCooldown(out float cdPct, out string cdLabel);
            bool canUse     = !onCooldown && CanPawnUseAbility();

            // 图标
            Rect iconRect = new Rect(outerRect.x + 15f, outerRect.y + 6f, 44f, 44f);
            GUI.color = canUse ? ColorReady : ColorOnCD;
            if (iconTex != null)
            {
                GUI.DrawTexture(iconRect, iconTex, ScaleMode.ScaleToFit, true);
            }
            else
            {
                Widgets.DrawBoxSolid(iconRect, new Color(0.2f, 0.2f, 0.3f));
                Text.Font   = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color   = Color.white;
                Widgets.Label(iconRect, (modAbility.label ?? "?").Substring(0, 1).ToUpper());
            }
            GUI.color   = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // 冷却遮罩
            if (onCooldown && cdPct > 0f)
            {
                float maskH = iconRect.height * cdPct;
                Widgets.DrawBoxSolid(
                    new Rect(iconRect.x, iconRect.y + iconRect.height - maskH, iconRect.width, maskH),
                    new Color(0, 0, 0, 0.55f));

                Text.Font   = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color   = Color.white;
                Widgets.Label(iconRect, cdLabel);
                GUI.color   = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            // 技能名称
            Rect nameRect = new Rect(outerRect.x + 2f, outerRect.y + 54f, outerRect.width - 4f, 18f);
            Text.Font   = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            GUI.color   = canUse ? Color.white : Color.gray;
            Widgets.Label(nameRect, modAbility.label ?? modAbility.defName);
            GUI.color   = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // 边框
            GUI.color = canUse ? ColorBorder : new Color(0.3f, 0.3f, 0.3f, 0.5f);
            Widgets.DrawBox(outerRect, 1);
            GUI.color = Color.white;

            // Tooltip
            TooltipHandler.TipRegion(outerRect, BuildTooltip(onCooldown, cdLabel));

            // 点击
            if (Widgets.ButtonInvisible(outerRect))
            {
                if (!canUse)
                {
                    if (onCooldown)
                        Messages.Message(
                            "CS_Ability_OnCooldown".Translate(modAbility.label ?? modAbility.defName, cdLabel),
                            MessageTypeDefOf.RejectInput, false);
                    return new GizmoResult(GizmoState.Mouseover);
                }
                return new GizmoResult(GizmoState.Interacted, Event.current);
            }

            return Mouse.IsOver(outerRect)
                ? new GizmoResult(GizmoState.Mouseover)
                : new GizmoResult(GizmoState.Clear);
        }

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);

            if (runtimeDef == null)
            {
                Messages.Message(
                    "CS_Ability_NotReady".Translate(modAbility.label ?? modAbility.defName),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            var ability = pawn.abilities?.GetAbility(runtimeDef);
            if (ability == null)
            {
                Messages.Message(
                    "CS_Ability_NotReady".Translate(modAbility.label ?? modAbility.defName),
                    MessageTypeDefOf.RejectInput, false);
                return;
            }

            // 委托原版 Ability 系统处理目标选择
            ability.QueueCastingJob(pawn, LocalTargetInfo.Invalid);
        }

        // ─────────────────────────────────────────────
        // 辅助
        // ─────────────────────────────────────────────

        private bool IsOnCooldown(out float cdPct, out string cdLabel)
        {
            cdPct   = 0f;
            cdLabel = string.Empty;
            if (runtimeDef == null) return false;

            var ability = pawn.abilities?.GetAbility(runtimeDef);
            if (ability == null) return false;

            int remain = ability.CooldownTicksRemaining;
            if (remain <= 0) return false;

            cdPct   = Mathf.Clamp01((float)remain / Mathf.Max(1f, (float)modAbility.cooldownTicks));
            cdLabel = remain >= 60 ? $"{remain / 60:0}s" : $"{remain}t";
            return true;
        }

        private bool CanPawnUseAbility()
        {
            if (pawn.Dead || pawn.Downed || pawn.InMentalState) return false;
            if (runtimeDef == null) return false;
            return pawn.abilities?.GetAbility(runtimeDef) != null;
        }

        private string BuildTooltip(bool onCooldown, string cdLabel)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>{modAbility.label ?? modAbility.defName}</b>");
            if (!string.IsNullOrEmpty(modAbility.description))
                sb.AppendLine(modAbility.description);
            sb.AppendLine();

            string carrierKey = $"CS_Ability_CarrierType_{modAbility.carrierType}";
            sb.AppendLine("CS_Ability_Carrier".Translate() + ": " +
                (carrierKey.CanTranslate() ? (string)carrierKey.Translate() : modAbility.carrierType.ToString()));

            if (modAbility.range > 0f)
                sb.AppendLine("CS_Studio_Ability_Range".Translate() + $": {modAbility.range:0}");
            if (modAbility.cooldownTicks > 0f)
                sb.AppendLine("CS_Studio_Ability_Cooldown".Translate() + $": {modAbility.cooldownTicks / 60f:0.0}s");

            if (onCooldown)
            {
                sb.AppendLine();
                sb.AppendLine("<color=#ff8888>" +
                    "CS_Ability_OnCooldown".Translate(modAbility.label ?? modAbility.defName, cdLabel) +
                    "</color>");
            }

            if (modAbility.effects?.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("CS_Studio_Effect_Title".Translate() + $" ({modAbility.effects.Count}):");
                foreach (var e in modAbility.effects.Take(3))
                {
                    string ek = $"CS_Ability_EffectType_{e.type}";
                    string el = ek.CanTranslate() ? (string)ek.Translate() : e.type.ToString();
                    sb.AppendLine(e.amount > 0f ? $"  - {el} x{e.amount:0}" : $"  - {el}");
                }
                if (modAbility.effects.Count > 3)
                    sb.AppendLine($"  ... +{modAbility.effects.Count - 3}");
            }

            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Harmony 补丁：为持有 CS 皮肤且皮肤含技能的 Pawn 注入技能 Gizmo
    /// </summary>
    public static class Patch_AbilityGizmos
    {
        public static void Apply(Harmony harmony)
        {
            var method  = AccessTools.Method(typeof(Pawn), "GetGizmos");
            var postfix = AccessTools.Method(typeof(Patch_AbilityGizmos), nameof(GetGizmos_Postfix));
            if (method != null && postfix != null)
            {
                harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                Log.Message("[CharacterStudio] Patch_AbilityGizmos 补丁已应用");
            }
        }

        public static void Unpatch(Harmony harmony)
        {
            var method = AccessTools.Method(typeof(Pawn), "GetGizmos");
            if (method != null)
                harmony.Unpatch(method, HarmonyPatchType.Postfix, harmony.Id);
        }

        private static void GetGizmos_Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (!__instance.IsColonistPlayerControlled) return;

            var list = __result.ToList();

            // 方式1：CS皮肤绑定的技能
            var comp = __instance.GetComp<CharacterStudio.Core.CompPawnSkin>();
            if (comp?.ActiveSkin?.abilities != null)
            {
                foreach (var ability in comp.ActiveSkin.abilities)
                {
                    if (ability != null)
                        list.Add(new Gizmo_CSAbility(__instance, ability));
                }
            }

            // 方式2：直接通过 AbilityGrantUtility 授予的技能（无需绑定皮肤）
            // 查找已授予此 Pawn 的 CS 技能并生成 Gizmo
            var grantedNames = CharacterStudio.Abilities.AbilityGrantUtility.GetGrantedAbilityNames(__instance);
            var skinAbilityNames = comp?.ActiveSkin?.abilities?.Select(a => a?.defName)
                .Where(n => n != null).ToHashSet() ?? new System.Collections.Generic.HashSet<string?>();

            foreach (var defName in grantedNames)
            {
                // 跳过已经通过皮肤显示的技能，避免重复
                if (skinAbilityNames.Contains(defName)) continue;

                var runtimeDef = CharacterStudio.Abilities.AbilityGrantUtility.GetRuntimeAbilityDef(defName);
                if (runtimeDef == null) continue;

                // 构造一个轻量 ModularAbilityDef 用于显示
                // 从 runtimeDef 还原显示信息
                var placeholder = new CharacterStudio.Abilities.ModularAbilityDef
                {
                    defName     = defName,
                    label       = runtimeDef.label ?? defName,
                    description = runtimeDef.description ?? string.Empty,
                    cooldownTicks = runtimeDef.cooldownTicksRange.min,
                };
                list.Add(new Gizmo_CSAbility(__instance, placeholder));
            }

            __result = list;
        }
    }
}

