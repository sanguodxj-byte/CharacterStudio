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
        private readonly Pawn pawn;
        private readonly ModularAbilityDef modAbility;
        private readonly VisibleAbilitySlotEntry? slotEntry;
        private readonly Gizmo? vanillaCommand;
        private readonly AbilityDef? runtimeDef;
        private readonly Texture2D? iconTex;

        private static readonly Color ColorReady = new Color(0.3f, 0.7f, 1.0f, 0.9f);
        private static readonly Color ColorOnCD = new Color(0.4f, 0.4f, 0.4f, 0.7f);
        private static readonly Color ColorBg = new Color(0.08f, 0.08f, 0.12f, 0.95f);
        private static readonly Color ColorBorder = new Color(0.4f, 0.6f, 0.9f, 0.8f);
        private static readonly Color ColorSlotBg = new Color(0.12f, 0.2f, 0.34f, 0.95f);
        private static readonly Color ColorSlotBorder = new Color(0.45f, 0.75f, 1.0f, 0.9f);

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

        public override float GetWidth(float maxWidth) => 75f;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect outerRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);

            Widgets.DrawBoxSolid(outerRect, ColorBg);

            bool onCooldown = IsOnCooldown(out float cdPct, out string cdLabel);
            bool canUse = !onCooldown && CanPawnUseAbility();

            DrawSlotBadge(outerRect);

            Rect iconRect = new Rect(outerRect.x + 15f, outerRect.y + 8f, 44f, 44f);
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

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            Rect nameRect = new Rect(outerRect.x + 2f, outerRect.y + 56f, outerRect.width - 4f, 16f);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperCenter;
            GUI.color = canUse ? Color.white : Color.gray;
            Widgets.Label(nameRect, modAbility.label ?? modAbility.defName);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            GUI.color = canUse ? ColorBorder : new Color(0.3f, 0.3f, 0.3f, 0.5f);
            Widgets.DrawBox(outerRect, 1);
            GUI.color = Color.white;

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

            if (TryProcessViaVanillaCommand(ev))
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

            if (normalizedCarrier == AbilityCarrierType.Self || normalizedTarget == AbilityTargetType.Self)
            {
                runtimeAbility.QueueCastingJob(pawn, LocalTargetInfo.Invalid);
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
                        LocalTargetInfo dest = normalizedCarrier == AbilityCarrierType.Projectile && normalizedTarget == AbilityTargetType.Cell
                            ? target
                            : LocalTargetInfo.Invalid;
                        runtimeAbility.QueueCastingJob(target, dest);
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
                _ => new TargetingParameters { canTargetSelf = true }
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
            if (runtimeDef == null) return false;
            return pawn.abilities?.GetAbility(runtimeDef) != null;
        }

        private void DrawSlotBadge(Rect outerRect)
        {
            string slotBadge = slotEntry?.slotBadge ?? slotEntry?.slotId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(slotBadge))
            {
                return;
            }

            Rect slotRect = new Rect(outerRect.x + 3f, outerRect.y + 3f, 24f, 14f);
            Widgets.DrawBoxSolid(slotRect, ColorSlotBg);
            GUI.color = ColorSlotBorder;
            Widgets.DrawBox(slotRect, 1);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(slotRect, slotBadge);
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

            if (visibleSlots.Count == 0)
            {
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
                list.Add(new Gizmo_CSAbility(__instance, placeholder, entry, vanillaCommand));
            }

            __result = list;
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