using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using CharacterStudio.Rendering;
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
        public const float BaseWidth = 75f;
        public const float BaseHeight = 75f;

        private readonly Pawn pawn;
        private readonly ModularAbilityDef modAbility;
        private readonly VisibleAbilitySlotEntry? slotEntry;
        private readonly Gizmo? vanillaCommand;
        private readonly AbilityDef? runtimeDef;
        private readonly Texture2D? iconTex;

        private static readonly Color ColorReady = new Color(0.74f, 0.90f, 1.0f, 0.98f);
        private static readonly Color ColorOnCD = new Color(0.60f, 0.66f, 0.74f, 0.88f);
        private static readonly Color ColorBg = new Color(0.05f, 0.07f, 0.11f, 0.96f);
        private static readonly Color ColorInset = new Color(0.10f, 0.16f, 0.26f, 0.20f);
        private static readonly Color ColorBorder = new Color(0.34f, 0.56f, 0.82f, 0.70f);
        private static readonly Color ColorBorderActive = new Color(0.62f, 0.84f, 1.0f, 0.88f);
        private static readonly Color ColorSlotBg = new Color(0.14f, 0.22f, 0.36f, 0.96f);
        private static readonly Color ColorSlotBorder = new Color(0.72f, 0.90f, 1.0f, 0.92f);
        private static readonly Color CooldownOverlay = new Color(0.02f, 0.03f, 0.05f, 0.56f);
        private static readonly Color CooldownBarBg = new Color(0.08f, 0.12f, 0.18f, 0.95f);
        private static readonly Color CooldownBarFill = new Color(0.36f, 0.72f, 1.0f, 0.95f);
        private static readonly Color GlowColor = new Color(0.72f, 0.90f, 1.0f, 0.12f);
        private static readonly Color ChargeBg = new Color(0.12f, 0.16f, 0.22f, 0.95f);
        private static readonly Color ChargeBorder = new Color(0.86f, 0.90f, 0.96f, 0.86f);
        private static readonly Color RuntimeTagBg = new Color(0.28f, 0.14f, 0.36f, 0.92f);
        private static readonly Color RuntimeTagBorder = new Color(0.92f, 0.76f, 1f, 0.88f);

        public Gizmo_CSAbility(Pawn pawn, ModularAbilityDef modAbility, VisibleAbilitySlotEntry? slotEntry = null, Gizmo? vanillaCommand = null)
        {
            this.pawn = pawn;
            this.modAbility = modAbility;
            this.slotEntry = slotEntry;
            this.vanillaCommand = vanillaCommand;
            this.runtimeDef = AbilityGrantUtility.GetRuntimeAbilityDef(modAbility.defName);
            this.iconTex = LoadAbilityIcon(modAbility.iconPath);

            this.Order = 10f;
        }

        public override float GetWidth(float maxWidth) => BaseWidth;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            float scale = Mathf.Clamp(maxWidth / BaseWidth, 0.32f, 1f);
            Rect outerRect = new Rect(topLeft.x, topLeft.y, BaseWidth * scale, BaseHeight * scale);
            Rect drawRect = new Rect(0f, 0f, BaseWidth, BaseHeight);

            Matrix4x4 oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(topLeft.x, topLeft.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

            Widgets.DrawBoxSolid(drawRect, ColorBg);
            Widgets.DrawBoxSolid(new Rect(drawRect.x + 1f, drawRect.y + 1f, drawRect.width - 2f, drawRect.height - 2f), ColorInset);

            float pulse = 0.5f + (0.5f * Mathf.Sin(((Find.TickManager?.TicksGame ?? 0) + pawn.thingIDNumber * 7f) / 8f));
            bool hovered = Mouse.IsOver(outerRect);
            float glowAlpha = hovered ? 0.16f : 0.06f + pulse * 0.03f;
            Widgets.DrawBoxSolid(new Rect(drawRect.x + 2f, drawRect.y + 2f, drawRect.width - 4f, 9f), new Color(GlowColor.r, GlowColor.g, GlowColor.b, glowAlpha));

            bool onCooldown = IsOnCooldown(out float cdPct, out string cdLabel);
            bool canUse = !onCooldown && CanPawnUseAbility();

            DrawSlotBadge(drawRect);
            DrawChargeBadge(drawRect);
            DrawRuntimeTag(drawRect);

            Rect iconFrameRect = new Rect(drawRect.x + 13f, drawRect.y + 9f, 48f, 40f);
            Widgets.DrawBoxSolid(iconFrameRect, new Color(0.03f, 0.05f, 0.08f, 0.92f));
            Widgets.DrawBox(iconFrameRect, 1);

            Rect iconRect = new Rect(drawRect.x + 15f, drawRect.y + 11f, 44f, 36f);
            GUI.color = canUse ? ColorReady : ColorOnCD;
            if (iconTex != null)
            {
                GUI.DrawTexture(iconRect, iconTex, ScaleMode.ScaleToFit, true);
            }
            else
            {
                Widgets.DrawBoxSolid(iconRect, new Color(0.2f, 0.2f, 0.3f));
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.white;
                Widgets.Label(iconRect, GetAbilityInitial());
            }

            if (onCooldown)
            {
                Widgets.DrawBoxSolid(iconFrameRect, CooldownOverlay);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(1f, 1f, 1f, 0.92f);
                Widgets.Label(iconFrameRect, cdLabel);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            Rect nameRect = new Rect(drawRect.x + 5f, drawRect.y + 51f, drawRect.width - 10f, 15f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            GUI.color = canUse ? Color.white : Color.gray;
            Widgets.Label(nameRect, GenText.Truncate(modAbility.label ?? modAbility.defName, 10f));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            if (slotEntry?.rStackCount > 0)
            {
                Rect stackRect = new Rect(drawRect.x + drawRect.width - 20f, drawRect.y + 4f, 16f, 14f);
                Widgets.DrawBoxSolid(stackRect, new Color(0.28f, 0.12f, 0.12f, 0.95f));
                GUI.color = new Color(1f, 0.78f, 0.72f, 0.92f);
                Widgets.DrawBox(stackRect, 1);
                GUI.color = Color.white;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(stackRect, slotEntry.rStackCount.ToString());
                Text.Anchor = TextAnchor.UpperLeft;
            }

            Rect cooldownBarRect = new Rect(drawRect.x + 6f, drawRect.y + 69f, drawRect.width - 12f, 4f);
            Widgets.DrawBoxSolid(cooldownBarRect, CooldownBarBg);
            float fillWidth = Mathf.Max(0f, (cooldownBarRect.width - 2f) * (onCooldown ? 1f - cdPct : 1f));
            if (fillWidth > 0.5f)
            {
                Widgets.DrawBoxSolid(new Rect(cooldownBarRect.x + 1f, cooldownBarRect.y + 1f, fillWidth, cooldownBarRect.height - 2f), onCooldown ? new Color(0.84f, 0.62f, 0.28f, 0.95f) : CooldownBarFill);
            }
            GUI.color = new Color(0.78f, 0.92f, 1f, 0.8f);
            Widgets.DrawBox(cooldownBarRect, 1);

            GUI.color = canUse ? (hovered ? ColorBorderActive : ColorBorder) : new Color(0.3f, 0.3f, 0.3f, 0.5f);
            Widgets.DrawBox(drawRect, 1);
            GUI.color = Color.white;
            GUI.matrix = oldMatrix;

            TooltipHandler.TipRegion(outerRect, BuildTooltip(onCooldown, cdLabel));

            if (Widgets.ButtonInvisible(outerRect))
            {
                if (!canUse)
                {
                    if (onCooldown)
                    {
                        Messages.Message(
                            "CS_Ability_OnCooldown".Translate(modAbility.label ?? modAbility.defName, cdLabel),
                            MessageTypeDefOf.RejectInput,
                            false);
                    }

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

            if (AbilityVanillaFlightUtility.ShouldBlockStandardAbilityAccessDuringFlight(pawn, modAbility))
            {
                Messages.Message(
                    "[CharacterStudio] Flight-only continuation required.",
                    MessageTypeDefOf.RejectInput,
                    false);
                return;
            }

            if (TryProcessViaVanillaCommand(ev))
            {
                return;
            }

            if (AbilityVanillaFlightUtility.TryNotifyFlightFollowupFailure(pawn, modAbility))
            {
                return;
            }

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

            if (!TryQueueAbilityWithTargeting(ability))
            {
                Messages.Message(
                    "CS_Ability_NotReady".Translate(modAbility.label ?? modAbility.defName),
                    MessageTypeDefOf.RejectInput, false);
            }
        }

        private bool TryProcessViaVanillaCommand(Event ev)
        {
            if (AbilityVanillaFlightUtility.ShouldBlockStandardAbilityAccessDuringFlight(pawn, modAbility))
            {
                return false;
            }

            if (GetSmartJumpComponent(modAbility) != null)
            {
                return false;
            }

            AbilityCarrierType normalizedCarrier = ModularAbilityDefExtensions.NormalizeCarrierType(modAbility.carrierType);
            AbilityTargetType normalizedTarget = ModularAbilityDefExtensions.NormalizeTargetType(modAbility);
            if (normalizedCarrier == AbilityCarrierType.Self || normalizedTarget == AbilityTargetType.Self)
            {
                return false;
            }

            if (vanillaCommand == null || ReferenceEquals(vanillaCommand, this))
            {
                return false;
            }

            try
            {
                vanillaCommand.ProcessInput(ev);
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[CharacterStudio] 委托原版技能 Gizmo 失败，回退到自定义 targeting: {ex.Message}");
                return false;
            }
        }

        private bool TryQueueAbilityWithTargeting(Ability runtimeAbility)
        {
            AbilityCarrierType normalizedCarrier = ModularAbilityDefExtensions.NormalizeCarrierType(modAbility.carrierType);
            AbilityTargetType normalizedTarget = ModularAbilityDefExtensions.NormalizeTargetType(modAbility);
            LocalTargetInfo forcedTarget = AbilityVanillaFlightUtility.ResolveFollowupTarget(pawn, modAbility, LocalTargetInfo.Invalid);

            if (normalizedCarrier == AbilityCarrierType.Self || normalizedTarget == AbilityTargetType.Self)
            {

                LocalTargetInfo selfTarget = new LocalTargetInfo(pawn);
                LocalTargetInfo selfDest = normalizedCarrier == AbilityCarrierType.Projectile && normalizedTarget == AbilityTargetType.Cell
                    ? new LocalTargetInfo(pawn.Position)
                    : LocalTargetInfo.Invalid;
                runtimeAbility.QueueCastingJob(selfTarget, selfDest);
                return true;
            }

            if (forcedTarget.IsValid)
            {
                LocalTargetInfo forcedDest = normalizedCarrier == AbilityCarrierType.Projectile && normalizedTarget == AbilityTargetType.Cell
                    ? forcedTarget
                    : LocalTargetInfo.Invalid;
                runtimeAbility.QueueCastingJob(forcedTarget, forcedDest);
                return true;
            }

            if (Find.Targeter == null)
            {
                return false;
            }

            TargetingParameters parms = BuildTargetingParameters(modAbility);
            Find.Targeter.BeginTargeting(
                parms,
                target =>
                {
                    try
                    {
                        LocalTargetInfo resolvedTarget = AbilityVanillaFlightUtility.ResolveFollowupTarget(pawn, modAbility, target);
                        LocalTargetInfo dest = normalizedCarrier == AbilityCarrierType.Projectile && normalizedTarget == AbilityTargetType.Cell
                            ? resolvedTarget
                            : LocalTargetInfo.Invalid;
                        runtimeAbility.QueueCastingJob(resolvedTarget, dest);
                    }
                    catch (System.Exception ex)
                    {
                        Log.Warning($"[CharacterStudio] Gizmo 技能目标施放失败: {ex.Message}");
                    }
                },
                pawn,
                null,
                iconTex,
                true);
            return true;
        }

        private static TargetingParameters BuildTargetingParameters(ModularAbilityDef ability)
        {
            AbilityCarrierType normalizedCarrier = ModularAbilityDefExtensions.NormalizeCarrierType(ability.carrierType);
            AbilityTargetType normalizedTarget = ModularAbilityDefExtensions.NormalizeTargetType(ability);

            return normalizedTarget switch
            {
                AbilityTargetType.Cell => new TargetingParameters
                {
                    canTargetLocations = true,
                    canTargetPawns = false,
                    canTargetBuildings = false,
                    canTargetSelf = false
                },
                AbilityTargetType.Entity => new TargetingParameters
                {
                    canTargetPawns = true,
                    canTargetBuildings = true,
                    canTargetLocations = false,
                    canTargetSelf = false
                },
                _ => new TargetingParameters
                {
                    canTargetSelf = true,
                    canTargetPawns = false,
                    canTargetBuildings = false,
                    canTargetLocations = false
                }
            };
        }

        // ─────────────────────────────────────────────
        // 辅助
        // ─────────────────────────────────────────────

        private bool IsOnCooldown(out float cdPct, out string cdLabel)
        {
            cdPct = 0f;
            cdLabel = string.Empty;

            int remain = GetSlotCooldownRemainingTicks();
            if (remain <= 0 && runtimeDef != null)
            {
                var ability = pawn.abilities?.GetAbility(runtimeDef);
                remain = ability?.CooldownTicksRemaining ?? 0;
            }

            if (remain <= 0)
            {
                return false;
            }

            cdPct = Mathf.Clamp01((float)remain / Mathf.Max(1f, GetCooldownBaseTicks()));
            cdLabel = remain >= 60 ? $"{remain / 60:0}s" : $"{remain}t";
            return true;
        }

        private int GetSlotCooldownRemainingTicks()
        {
            var comp = pawn.GetComp<CompPawnSkin>();
            if (comp == null || slotEntry == null || string.IsNullOrWhiteSpace(slotEntry.slotId))
            {
                return 0;
            }

            int cooldownUntilTick = slotEntry.slotId switch
            {
                "Q" => comp.qCooldownUntilTick,
                "W" => comp.wCooldownUntilTick,
                "E" => comp.eCooldownUntilTick,
                "T" => comp.tCooldownUntilTick,
                "A" => comp.aCooldownUntilTick,
                "S" => comp.sCooldownUntilTick,
                "D" => comp.dCooldownUntilTick,
                "F" => comp.fCooldownUntilTick,
                "Z" => comp.zCooldownUntilTick,
                "X" => comp.xCooldownUntilTick,
                "C" => comp.cCooldownUntilTick,
                "V" => comp.vCooldownUntilTick,
                "R" => comp.rCooldownUntilTick,
                _ => 0
            };

            int now = Find.TickManager?.TicksGame ?? 0;
            return cooldownUntilTick > now ? cooldownUntilTick - now : 0;
        }

        private int GetCooldownBaseTicks()
        {
            int runtimeCooldown = runtimeDef?.cooldownTicksRange.min ?? 0;
            return Mathf.Max(1, Mathf.RoundToInt(modAbility.cooldownTicks), runtimeCooldown);
        }

        private bool CanPawnUseAbility()
        {
            if (pawn.Dead || pawn.Downed || pawn.InMentalState) return false;
            if (!pawn.Drafted) return false;
            if (!AbilityTimeStopRuntimeController.CanPawnAct(pawn)) return false;
            if (AbilityVanillaFlightUtility.ShouldBlockStandardAbilityAccessDuringFlight(pawn, modAbility)) return false;
            if (runtimeDef == null) return false;
            if (!AbilityVanillaFlightUtility.CanUseFlightFollowup(pawn, modAbility, out _, out _)) return false;
            return pawn.abilities?.GetAbility(runtimeDef) != null;
        }

        private void DrawSlotBadge(Rect outerRect)
        {
            string slotBadge = slotEntry?.slotBadge ?? slotEntry?.slotId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(slotBadge))
            {
                return;
            }

            Rect slotRect = new Rect(outerRect.x + 4f, outerRect.y + 4f, 22f, 14f);
            Widgets.DrawBoxSolid(slotRect, ColorSlotBg);
            GUI.color = ColorSlotBorder;
            Widgets.DrawBox(slotRect, 1);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(slotRect, slotBadge);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawChargeBadge(Rect outerRect)
        {
            if (slotEntry == null || slotEntry.charges <= 1)
            {
                return;
            }

            Rect chargeRect = new Rect(outerRect.x + 4f, outerRect.y + outerRect.height - 20f, 18f, 14f);
            Widgets.DrawBoxSolid(chargeRect, ChargeBg);
            GUI.color = ChargeBorder;
            Widgets.DrawBox(chargeRect, 1);
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(chargeRect, slotEntry.charges.ToString());
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawRuntimeTag(Rect outerRect)
        {
            if (slotEntry == null || string.IsNullOrWhiteSpace(slotEntry.runtimeTag))
            {
                return;
            }

            Rect tagRect = new Rect(outerRect.x + outerRect.width - 20f, outerRect.y + outerRect.height - 20f, 16f, 14f);
            Widgets.DrawBoxSolid(tagRect, RuntimeTagBg);
            GUI.color = RuntimeTagBorder;
            Widgets.DrawBox(tagRect, 1);
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(tagRect, slotEntry.runtimeTag);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private string BuildTooltip(bool onCooldown, string cdLabel)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>{modAbility.label ?? modAbility.defName}</b>");

            if (!string.IsNullOrEmpty(modAbility.description))
            {
                sb.AppendLine(modAbility.description);
            }

            sb.AppendLine();

            string carrierKey = $"CS_Ability_CarrierType_{modAbility.carrierType}";
            sb.AppendLine("CS_Ability_Carrier".Translate() + ": " +
                (carrierKey.CanTranslate() ? (string)carrierKey.Translate() : modAbility.carrierType.ToString()));

            if (modAbility.range > 0f)
            {
                sb.AppendLine("CS_Studio_Ability_Range".Translate() + $": {modAbility.range:0}");
            }

            if (modAbility.cooldownTicks > 0f)
            {
                sb.AppendLine("CS_Studio_Ability_Cooldown".Translate() + $": {modAbility.cooldownTicks / 60f:0.0}s");
            }

            if (slotEntry != null)
            {
                if (slotEntry.charges > 1)
                {
                    sb.AppendLine("Charges: " + slotEntry.charges);
                }
                if (slotEntry.isOverride)
                {
                    sb.AppendLine("Override: Active");
                }
                if (slotEntry.isCombo)
                {
                    sb.AppendLine("Combo Window: Active");
                }
                if (slotEntry.rStackCount > 0)
                {
                    sb.AppendLine("Stacks: " + slotEntry.rStackCount);
                }
                if (!string.IsNullOrWhiteSpace(slotEntry.runtimeSummary))
                {
                    sb.AppendLine("Runtime: " + slotEntry.runtimeSummary);
                }
            }

            if (onCooldown)
            {
                sb.AppendLine();
                sb.AppendLine("<color=#ff8888>" +
                    "CS_Ability_OnCooldown".Translate(modAbility.label ?? modAbility.defName, cdLabel) +
                    "</color>");
            }

            List<AbilityEffectConfig> validEffects = (modAbility.effects ?? Enumerable.Empty<AbilityEffectConfig>())
                .OfType<AbilityEffectConfig>()
                .ToList();
            if (validEffects.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("CS_Studio_Effect_Title".Translate() + $" ({validEffects.Count}):");
                foreach (AbilityEffectConfig effect in validEffects.Take(3))
                {
                    string ek = $"CS_Ability_EffectType_{effect.type}";
                    string el = ek.CanTranslate() ? (string)ek.Translate() : effect.type.ToString();
                    sb.AppendLine(effect.amount > 0f ? $"  - {el} x{effect.amount:0}" : $"  - {el}");
                }

                if (validEffects.Count > 3)
                {
                    sb.AppendLine($"  ... +{validEffects.Count - 3}");
                }
            }

            return sb.ToString().TrimEnd();
        }

        private string GetAbilityInitial()
        {
            string label = modAbility.label ?? modAbility.defName ?? "?";
            return string.IsNullOrWhiteSpace(label)
                ? "?"
                : label.Trim().Substring(0, 1).ToUpperInvariant();
        }

        private static Texture2D? LoadAbilityIcon(string iconPath)
        {
            if (!string.IsNullOrWhiteSpace(iconPath))
            {
                if (System.IO.Path.IsPathRooted(iconPath))
                {
                    var tex = RuntimeAssetLoader.LoadTextureRaw(iconPath, true);
                    if (tex != null)
                        return tex;
                }
                else
                {
                    var tex = ContentFinder<Texture2D>.Get(iconPath, false);
                    if (tex != null)
                        return tex;

                    tex = RuntimeAssetLoader.LoadTextureRaw(iconPath, true);
                    if (tex != null)
                        return tex;
                }
            }

            return ContentFinder<Texture2D>.Get("UI/Designators/Strip", true);
        }

        private static AbilityRuntimeComponentConfig? GetSmartJumpComponent(ModularAbilityDef? ability)
        {
            if (ability?.runtimeComponents == null)
            {
                return null;
            }

            return ability.runtimeComponents.FirstOrDefault(component => component != null
                && component.enabled
                && (component.type == AbilityRuntimeComponentType.SmartJump
                    || component.type == AbilityRuntimeComponentType.EShortJump));
        }
    }

    public class Gizmo_CSShieldStatus : Gizmo
    {
        private readonly Pawn pawn;

        private const float ShieldHudWidth = 118f;
        private const float LowShieldThreshold = 0.22f;
        private const float CriticalShieldThreshold = 0.08f;
        private const int ActivatePulseTicks = 24;
        private const int BreakFlashTicks = 28;

        private static readonly Dictionary<int, ShieldHudFxState> shieldHudFxByPawn = new Dictionary<int, ShieldHudFxState>();

        private static readonly Color PanelBg = new Color(0.05f, 0.07f, 0.11f, 0.92f);
        private static readonly Color PanelInset = new Color(0.11f, 0.18f, 0.28f, 0.20f);
        private static readonly Color BorderIdle = new Color(0.34f, 0.56f, 0.78f, 0.58f);
        private static readonly Color BorderActive = new Color(0.56f, 0.80f, 1.00f, 0.78f);
        private static readonly Color BarBg = new Color(0.08f, 0.12f, 0.18f, 0.95f);
        private static readonly Color BarFill = new Color(0.30f, 0.68f, 1.00f, 0.95f);
        private static readonly Color BarGlow = new Color(0.72f, 0.90f, 1.00f, 0.18f);
        private static readonly Color Accent = new Color(0.72f, 0.90f, 1.00f, 0.95f);
        private static readonly Color SoftText = new Color(0.84f, 0.91f, 1.00f, 0.92f);
        private static readonly Color FaintText = new Color(0.62f, 0.72f, 0.84f, 0.90f);
        private static readonly Color EmptyText = new Color(0.56f, 0.64f, 0.74f, 0.88f);
        private static readonly Color BreakFlash = new Color(0.95f, 0.99f, 1.00f, 1.00f);

        private struct ShieldHudFxState
        {
            public float lastShield;
            public int lastSeenTick;
            public int activatePulseUntilTick;
            public int breakFlashUntilTick;
        }

        public Gizmo_CSShieldStatus(Pawn pawn)
        {
            this.pawn = pawn;
            Order = 9f;
        }

        public override float GetWidth(float maxWidth) => ShieldHudWidth;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect outerRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), Gizmo_CSAbility.BaseHeight);
            CompPawnSkin? skin = pawn.GetComp<CompPawnSkin>();
            float currentShield = Mathf.Max(0f, skin?.shieldRemainingDamage ?? 0f);
            float storedShield = Mathf.Max(0f, skin?.shieldStoredHeal ?? 0f);
            float totalShield = Mathf.Max(currentShield, currentShield + storedShield);
            float fillPercent = totalShield > 0.001f ? Mathf.Clamp01(currentShield / totalShield) : 0f;
            int nowTick = Find.TickManager?.TicksGame ?? 0;
            int expireTick = skin?.shieldExpireTick ?? -1;
            int ticksLeft = expireTick > nowTick ? expireTick - nowTick : 0;
            ShieldHudFxState fxState = UpdateAndGetFxState(pawn, currentShield, nowTick);
            float activateAlpha = fxState.activatePulseUntilTick > nowTick
                ? Mathf.Clamp01((fxState.activatePulseUntilTick - nowTick) / (float)ActivatePulseTicks)
                : 0f;
            float breakAlpha = fxState.breakFlashUntilTick > nowTick
                ? Mathf.Clamp01((fxState.breakFlashUntilTick - nowTick) / (float)BreakFlashTicks)
                : 0f;
            float pulseSpeed = fillPercent < LowShieldThreshold ? 8f : 11f;
            float pulse = 0.5f + (0.5f * Mathf.Sin((nowTick + pawn.thingIDNumber * 13f) / pulseSpeed));
            bool lowShield = currentShield > 0.001f && fillPercent < LowShieldThreshold;
            bool criticalShield = currentShield > 0.001f && fillPercent < CriticalShieldThreshold;

            DrawPanel(outerRect, fillPercent, currentShield, totalShield, storedShield, ticksLeft, pulse, activateAlpha, breakAlpha, lowShield, criticalShield);
            TooltipHandler.TipRegion(outerRect, BuildTooltip(currentShield, totalShield, storedShield, ticksLeft));

            return Mouse.IsOver(outerRect)
                ? new GizmoResult(GizmoState.Mouseover)
                : new GizmoResult(GizmoState.Clear);
        }

        public static bool ShouldKeepVisibleForFeedback(Pawn pawn, float currentShield, int nowTick)
        {
            ShieldHudFxState state = UpdateAndGetFxState(pawn, currentShield, nowTick);
            return state.breakFlashUntilTick > nowTick;
        }

        private static ShieldHudFxState UpdateAndGetFxState(Pawn pawn, float currentShield, int nowTick)
        {
            int pawnId = pawn.thingIDNumber;
            ShieldHudFxState state = shieldHudFxByPawn.TryGetValue(pawnId, out ShieldHudFxState existing)
                ? existing
                : default;

            if (state.lastShield <= 0.001f && currentShield > 0.001f)
            {
                state.activatePulseUntilTick = nowTick + ActivatePulseTicks;
            }

            if (state.lastShield > 0.001f && currentShield <= 0.001f)
            {
                state.breakFlashUntilTick = nowTick + BreakFlashTicks;
            }

            state.lastShield = currentShield;
            state.lastSeenTick = nowTick;
            shieldHudFxByPawn[pawnId] = state;
            return state;
        }

        private static void DrawPanel(Rect rect, float fillPercent, float currentShield, float totalShield, float storedShield, int ticksLeft, float pulse, float activateAlpha, float breakAlpha, bool lowShield, bool criticalShield)
        {
            Widgets.DrawBoxSolid(rect, PanelBg);

            float headerGlowAlpha = 0.04f + (pulse * 0.05f) + (activateAlpha * 0.16f);
            Rect glowRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 11f);
            Widgets.DrawBoxSolid(glowRect, new Color(0.22f, 0.48f, 0.82f, headerGlowAlpha));

            Rect insetRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
            Widgets.DrawBoxSolid(insetRect, PanelInset);

            GUI.color = GetBorderColor(pulse, activateAlpha, breakAlpha, lowShield);
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            Rect iconRect = new Rect(rect.x + 7f, rect.y + 8f, 10f, 10f);
            DrawShieldIcon(iconRect, pulse, activateAlpha, breakAlpha);

            Rect titleRect = new Rect(rect.x + 21f, rect.y + 3f, 46f, 18f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = SoftText;
            Widgets.Label(titleRect, "CS_ShieldHud_Title".Translate());

            Rect timerRect = new Rect(rect.xMax - 36f, rect.y + 3f, 29f, 18f);
            Text.Anchor = TextAnchor.UpperRight;
            GUI.color = ticksLeft > 0 ? FaintText : EmptyText;
            Widgets.Label(timerRect, ticksLeft > 0 ? $"{ticksLeft / 60f:0.0}s" : "--");

            Rect currentRect = new Rect(rect.x + 7f, rect.y + 21f, 32f, 18f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            float criticalPulse = criticalShield ? (0.76f + (pulse * 0.24f)) : 1f;
            GUI.color = currentShield > 0.001f
                ? new Color(1f, 1f, 1f, criticalPulse)
                : EmptyText;
            Widgets.Label(currentRect, Mathf.CeilToInt(currentShield).ToString());

            Rect totalRect = new Rect(rect.x + 36f, rect.y + 21f, rect.width - 43f, 18f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = FaintText;
            Widgets.Label(totalRect, totalShield > 0.001f ? $"/ {Mathf.CeilToInt(totalShield)}" : "/ --");

            Rect statusRect = new Rect(rect.x + 7f, rect.y + 40f, rect.width - 14f, 18f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = breakAlpha > 0.001f && currentShield <= 0.001f ? EmptyText : FaintText;
            Widgets.Label(statusRect, BuildStatusLabel(currentShield, storedShield, fillPercent, breakAlpha));

            Rect barOuter = new Rect(rect.x + 7f, rect.y + 60f, rect.width - 14f, 8f);
            Widgets.DrawBoxSolid(barOuter, BarBg);

            float fillWidth = Mathf.Max(0f, (barOuter.width - 2f) * fillPercent);
            Rect fillRect = new Rect(barOuter.x + 1f, barOuter.y + 1f, fillWidth, barOuter.height - 2f);
            if (fillRect.width > 0.5f)
            {
                Color fillColor = GetMeterFillColor(pulse, activateAlpha, breakAlpha, lowShield, criticalShield);
                Widgets.DrawBoxSolid(fillRect, fillColor);

                Rect shineRect = new Rect(fillRect.x, fillRect.y, fillRect.width, Mathf.Min(2f, fillRect.height));
                Widgets.DrawBoxSolid(shineRect, new Color(1f, 1f, 1f, 0.10f + (pulse * 0.08f) + (activateAlpha * 0.06f)));

                Rect glowFillRect = new Rect(fillRect.x, fillRect.y, fillRect.width, Mathf.Min(4f, fillRect.height));
                Widgets.DrawBoxSolid(glowFillRect, new Color(BarGlow.r, BarGlow.g, BarGlow.b, BarGlow.a + (activateAlpha * 0.10f)));

                Rect edgeRect = new Rect(fillRect.xMax - 2f, fillRect.y, 2f, fillRect.height);
                Widgets.DrawBoxSolid(edgeRect, new Color(0.92f, 0.98f, 1f, 0.25f + (pulse * 0.10f)));
            }

            GUI.color = new Color(0.8f, 0.94f, 1f, 0.85f);
            Widgets.DrawBox(barOuter, 1);
            GUI.color = Color.white;

            DrawBreakFlash(rect, barOuter, iconRect, breakAlpha);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private static string BuildStatusLabel(float currentShield, float storedShield, float fillPercent, float breakAlpha)
        {
            if (breakAlpha > 0.001f && currentShield <= 0.001f)
            {
                return "CS_ShieldHud_Status_Depleted".Translate();
            }

            if (storedShield > 0.001f)
            {
                return "CS_ShieldHud_Status_Stored".Translate(Mathf.CeilToInt(storedShield));
            }

            if (currentShield > 0.001f && fillPercent < LowShieldThreshold)
            {
                return "CS_ShieldHud_Status_Low".Translate();
            }

            return "CS_ShieldHud_Status_Active".Translate();
        }

        private static Color GetBorderColor(float pulse, float activateAlpha, float breakAlpha, bool lowShield)
        {
            Color borderColor = Color.Lerp(BorderIdle, BorderActive, 0.28f + (pulse * 0.32f) + (activateAlpha * 0.30f));
            if (lowShield)
            {
                borderColor = Color.Lerp(borderColor, new Color(0.9f, 0.96f, 1f, borderColor.a), 0.18f + (pulse * 0.16f));
            }
            if (breakAlpha > 0.001f)
            {
                borderColor = Color.Lerp(borderColor, BreakFlash, breakAlpha);
            }
            return borderColor;
        }

        private static Color GetMeterFillColor(float pulse, float activateAlpha, float breakAlpha, bool lowShield, bool criticalShield)
        {
            Color fillColor = Color.Lerp(BarFill, new Color(0.56f, 0.84f, 1f, 0.96f), (pulse * 0.10f) + (activateAlpha * 0.18f));
            if (lowShield)
            {
                fillColor = Color.Lerp(fillColor, new Color(0.84f, 0.93f, 1f, 0.96f), 0.22f + (pulse * 0.14f));
            }
            if (criticalShield)
            {
                fillColor = Color.Lerp(fillColor, new Color(0.95f, 0.99f, 1f, 0.98f), 0.16f + (pulse * 0.20f));
            }
            if (breakAlpha > 0.001f)
            {
                fillColor = Color.Lerp(fillColor, BreakFlash, breakAlpha);
            }
            return fillColor;
        }

        private static void DrawShieldIcon(Rect rect, float pulse, float activateAlpha, float breakAlpha)
        {
            float inset = 1f;
            Vector2 top = new Vector2(rect.center.x, rect.y + inset);
            Vector2 upperRight = new Vector2(rect.x + rect.width * 0.80f, rect.y + rect.height * 0.24f);
            Vector2 lowerRight = new Vector2(rect.x + rect.width * 0.68f, rect.y + rect.height * 0.88f);
            Vector2 bottom = new Vector2(rect.center.x, rect.yMax - inset);
            Vector2 lowerLeft = new Vector2(rect.x + rect.width * 0.32f, rect.y + rect.height * 0.88f);
            Vector2 upperLeft = new Vector2(rect.x + rect.width * 0.20f, rect.y + rect.height * 0.24f);

            Color iconColor = Color.Lerp(Accent, BreakFlash, breakAlpha * 0.8f);
            iconColor.a = Mathf.Clamp01(0.80f + (pulse * 0.10f) + (activateAlpha * 0.12f));

            Widgets.DrawLine(top, upperRight, iconColor, 1.2f);
            Widgets.DrawLine(upperRight, lowerRight, iconColor, 1.2f);
            Widgets.DrawLine(lowerRight, bottom, iconColor, 1.2f);
            Widgets.DrawLine(bottom, lowerLeft, iconColor, 1.2f);
            Widgets.DrawLine(lowerLeft, upperLeft, iconColor, 1.2f);
            Widgets.DrawLine(upperLeft, top, iconColor, 1.2f);

            Rect coreRect = new Rect(rect.x + 2f, rect.y + 3f, rect.width - 4f, rect.height - 4f);
            Widgets.DrawBoxSolid(coreRect, new Color(0.3f, 0.72f, 1f, 0.08f + (pulse * 0.05f) + (activateAlpha * 0.08f)));
        }

        private static void DrawBreakFlash(Rect rect, Rect barOuter, Rect iconRect, float breakAlpha)
        {
            if (breakAlpha <= 0.001f)
            {
                return;
            }

            Rect overlayRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
            Widgets.DrawBoxSolid(overlayRect, new Color(0.96f, 0.99f, 1f, breakAlpha * 0.16f));

            Rect flashBarRect = new Rect(barOuter.x + 1f, barOuter.y + 1f, barOuter.width - 2f, barOuter.height - 2f);
            Widgets.DrawBoxSolid(flashBarRect, new Color(0.96f, 0.99f, 1f, breakAlpha * 0.35f));

            Color shardColor = new Color(1f, 1f, 1f, breakAlpha * 0.70f);
            Widgets.DrawLine(new Vector2(iconRect.x + 1f, iconRect.yMax - 2f), new Vector2(iconRect.xMax - 1f, iconRect.y + 2f), shardColor, 1.1f);
            Widgets.DrawLine(new Vector2(iconRect.x + 3f, iconRect.center.y), new Vector2(iconRect.xMax - 1f, iconRect.yMax - 2f), shardColor, 0.9f);
        }

        private static string BuildTooltip(float currentShield, float totalShield, float storedShield, int ticksLeft)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>{"CS_ShieldHud_Title".Translate()}</b>");
            sb.AppendLine($"{"CS_ShieldHud_Tooltip_Current".Translate()}: {currentShield:0.#}");
            sb.AppendLine($"{"CS_ShieldHud_Tooltip_Capacity".Translate()}: {totalShield:0.#}");
            if (storedShield > 0.001f)
            {
                sb.AppendLine($"{"CS_ShieldHud_Tooltip_Stored".Translate()}: {storedShield:0.#}");
            }
            if (ticksLeft > 0)
            {
                sb.AppendLine($"{"CS_ShieldHud_Tooltip_Remaining".Translate()}: {ticksLeft / 60f:0.0}s");
            }

            return sb.ToString().TrimEnd();
        }
    }

    public class Gizmo_CSAbilityBar : Gizmo
    {
        private readonly List<Gizmo_CSAbility> abilities;

        private const int MaxColumns = 5;
        private const float HorizontalGap = 6f;
        private const float VerticalGap = 6f;
        private const float Padding = 8f;

        public Gizmo_CSAbilityBar(IEnumerable<Gizmo_CSAbility> abilities)
        {
            this.abilities = abilities?.Where(static a => a != null).ToList() ?? new List<Gizmo_CSAbility>();
            Order = 10f;
        }

        public override float GetWidth(float maxWidth)
        {
            LayoutMetrics metrics = CalculateLayout();
            int columns = Mathf.Min(MaxColumns, Mathf.Max(1, metrics.columnCount));
            return (columns * metrics.itemWidth) + ((columns - 1) * HorizontalGap) + Padding * 2f;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            if (abilities.Count == 0)
            {
                return new GizmoResult(GizmoState.Clear);
            }

            LayoutMetrics metrics = CalculateLayout();
            int columns = Mathf.Min(MaxColumns, Mathf.Max(1, metrics.columnCount));
            GizmoResult finalResult = new GizmoResult(GizmoState.Clear);

            float panelHeight = (metrics.rowCount * metrics.itemHeight) + ((metrics.rowCount - 1) * VerticalGap) + Padding * 2f;
            Rect panelRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), panelHeight);
            Widgets.DrawBoxSolid(panelRect, new Color(0.04f, 0.06f, 0.10f, 0.92f));
            Widgets.DrawBoxSolid(new Rect(panelRect.x + 1f, panelRect.y + 1f, panelRect.width - 2f, panelRect.height - 2f), new Color(0.10f, 0.16f, 0.26f, 0.16f));
            Widgets.DrawBoxSolid(new Rect(panelRect.x + 2f, panelRect.y + 2f, panelRect.width - 4f, 10f), new Color(0.62f, 0.84f, 1f, 0.08f));
            GUI.color = new Color(0.36f, 0.58f, 0.82f, 0.72f);
            Widgets.DrawBox(panelRect, 1);
            GUI.color = Color.white;

            for (int index = 0; index < abilities.Count; index++)
            {
                int row = index / columns;
                int column = index % columns;
                Vector2 childTopLeft = new Vector2(
                    topLeft.x + Padding + column * (metrics.itemWidth + HorizontalGap),
                    topLeft.y + Padding + row * (metrics.itemHeight + VerticalGap));

                Gizmo_CSAbility child = abilities[index];
                GizmoResult result = child.GizmoOnGUI(childTopLeft, metrics.itemWidth, parms);
                if (result.State == GizmoState.Interacted || result.State == GizmoState.OpenedFloatMenu)
                {
                    return result;
                }

                if (result.State == GizmoState.Mouseover)
                {
                    finalResult = result;
                }
            }

            return finalResult;
        }

        private LayoutMetrics CalculateLayout()
        {
            int count = Mathf.Max(1, abilities.Count);
            int rows = count <= 5 ? 1 : count <= 10 ? 2 : 3;
            int columns = Mathf.CeilToInt(count / (float)rows);
            float scale = rows switch
            {
                <= 1 => 1f,
                2 => 0.86f,
                _ => 0.72f
            };

            return new LayoutMetrics
            {
                rowCount = rows,
                columnCount = columns,
                itemWidth = Gizmo_CSAbility.BaseWidth * scale,
                itemHeight = Gizmo_CSAbility.BaseHeight * scale
            };
        }

        private struct LayoutMetrics
        {
            public int rowCount;
            public int columnCount;
            public float itemWidth;
            public float itemHeight;
        }
    }

    /// <summary>
    /// Harmony 补丁：为持有 CS 皮肤且皮肤含技能的 Pawn 注入技能 Gizmo
    /// </summary>
    public static class Patch_AbilityGizmos
    {
        private static readonly Dictionary<int, Dictionary<string, Gizmo>> cachedVanillaCommandsByPawn =
            new Dictionary<int, Dictionary<string, Gizmo>>();

        public static void Apply(Harmony harmony)
        {
            var method = AccessTools.Method(typeof(Pawn), "GetGizmos");
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

        public static bool TryProcessVanillaCommand(Pawn pawn, string abilityDefName)
        {
            if (pawn == null || string.IsNullOrWhiteSpace(abilityDefName))
            {
                return false;
            }

            if (!cachedVanillaCommandsByPawn.TryGetValue(pawn.thingIDNumber, out Dictionary<string, Gizmo>? commands))
            {
                return false;
            }

            if (!commands.TryGetValue(abilityDefName, out Gizmo? command) || command == null)
            {
                return false;
            }

            try
            {
                command.ProcessInput(Event.current ?? new Event());
                return true;
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[CharacterStudio] 热键委托原版技能 Gizmo 失败: {ex.Message}");
                return false;
            }
        }

        private static void GetGizmos_Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            if (!__instance.IsColonistPlayerControlled || !__instance.Drafted)
            {
                return;
            }

            var list = __result.ToList();
            var visibleSlots = AbilityLoadoutRuntimeUtility.EnumerateVisibleAbilitySlots(__instance)
                .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.abilityDefName))
                .ToList();
            bool showShieldHud = ShouldShowShieldHud(__instance);

            if (visibleSlots.Count == 0)
            {
                if (showShieldHud)
                {
                    list.Add(new Gizmo_CSShieldStatus(__instance));
                    __result = list;
                }
                return;
            }

            var vanillaCommandsByAbility = new Dictionary<string, Gizmo>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var gizmo in list)
            {
                if (TryGetVanillaCSAbilityDefName(gizmo, out string defName))
                {
                    vanillaCommandsByAbility[defName] = gizmo;
                }
            }

            cachedVanillaCommandsByPawn[__instance.thingIDNumber] =
                new Dictionary<string, Gizmo>(vanillaCommandsByAbility, System.StringComparer.OrdinalIgnoreCase);

            list.RemoveAll(gizmo => TryGetVanillaCSAbilityDefName(gizmo, out _));

            if (showShieldHud)
            {
                list.Add(new Gizmo_CSShieldStatus(__instance));
            }

            List<Gizmo_CSAbility> abilityGizmos = new List<Gizmo_CSAbility>();
            foreach (var entry in visibleSlots)
            {
                string defName = entry.abilityDefName!;
                var runtimeDef = AbilityGrantUtility.GetRuntimeAbilityDef(defName);
                if (runtimeDef == null || __instance.abilities?.GetAbility(runtimeDef) == null)
                {
                    continue;
                }

                var resolvedAbility = AbilityLoadoutRuntimeUtility.ResolveAbilityByDefName(__instance, defName);
                var placeholder = resolvedAbility?.Clone()
                    ?? AbilityGrantUtility.GetRuntimeAbilitySourceDef(defName)
                    ?? new ModularAbilityDef
                    {
                        defName = defName,
                        label = runtimeDef.label ?? defName,
                        description = runtimeDef.description ?? string.Empty,
                        cooldownTicks = runtimeDef.cooldownTicksRange.min,
                        iconPath = runtimeDef.iconPath ?? string.Empty,
                    };

                vanillaCommandsByAbility.TryGetValue(defName, out Gizmo? vanillaCommand);
                abilityGizmos.Add(new Gizmo_CSAbility(__instance, placeholder, entry, vanillaCommand));
            }

            if (abilityGizmos.Count > 0)
            {
                list.Add(new Gizmo_CSAbilityBar(abilityGizmos));
            }

            __result = list;
        }

        private static bool ShouldShowShieldHud(Pawn pawn)
        {
            CompPawnSkin? skin = pawn.GetComp<CompPawnSkin>();
            if (skin == null)
            {
                return false;
            }

            int now = Find.TickManager?.TicksGame ?? 0;
            float currentShield = Mathf.Max(0f, skin.shieldRemainingDamage);
            if (currentShield > 0.001f)
            {
                Gizmo_CSShieldStatus.ShouldKeepVisibleForFeedback(pawn, currentShield, now);
                return true;
            }

            if (skin.shieldExpireTick >= now && (skin.shieldStoredHeal > 0.001f || skin.shieldStoredBonusDamage > 0.001f))
            {
                Gizmo_CSShieldStatus.ShouldKeepVisibleForFeedback(pawn, currentShield, now);
                return true;
            }

            return Gizmo_CSShieldStatus.ShouldKeepVisibleForFeedback(pawn, currentShield, now);
        }

        private static bool TryGetVanillaCSAbilityDefName(Gizmo gizmo, out string defName)
        {
            defName = string.Empty;
            if (gizmo == null)
            {
                return false;
            }

            if (!string.Equals(gizmo.GetType().Name, "Command_Ability", System.StringComparison.Ordinal))
            {
                return false;
            }

            var abilityField = AccessTools.Field(gizmo.GetType(), "ability");
            if (abilityField?.GetValue(gizmo) is not Ability ability || ability.def == null)
            {
                return false;
            }

            if (!ability.def.defName.StartsWith("CS_RT_", System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            defName = ability.def.defName.Substring("CS_RT_".Length);
            return !string.IsNullOrWhiteSpace(defName);
        }
    }
}
