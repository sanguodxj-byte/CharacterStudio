using System.Collections.Generic;
using System.Xml.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.Core;
using UnityEngine;

namespace CharacterStudio.Exporter
{
    /// <summary>
    /// 技能能力数据 XML 序列化的单一事实来源。
    ///
    /// 用于统一：
    /// - 技能编辑器会话持久化
    /// - 皮肤工程保存
    /// - Mod/AbilityDef 导出时的共享能力块写出
    /// </summary>
    internal static class AbilityXmlSerialization
    {
        internal static XDocument CreateEditorSessionDocument(List<ModularAbilityDef>? abilities, SkinAbilityHotkeyConfig? hotkeys = null)
        {
            var defs = new XElement("Defs");
            var skinRoot = new XElement("CharacterStudio.Core.PawnSkinDef");

            skinRoot.Add(GenerateAbilitiesElement(abilities) ?? new XElement("abilities"));

            XElement? hotkeysElement = GenerateAbilityHotkeysElement(hotkeys);
            if (hotkeysElement != null)
            {
                skinRoot.Add(hotkeysElement);
            }

            defs.Add(skinRoot);
            return new XDocument(defs);
        }

        internal static XElement? GenerateAbilitiesElement(List<ModularAbilityDef>? abilities)
        {
            if (abilities == null || abilities.Count == 0)
            {
                return null;
            }

            var element = new XElement("abilities");
            foreach (ModularAbilityDef? ability in abilities)
            {
                XElement? abilityElement = GenerateAbilityElement(ability);
                if (abilityElement != null)
                {
                    element.Add(abilityElement);
                }
            }

            return element;
        }

        internal static XElement? GenerateAbilityEffectsElement(List<AbilityEffectConfig>? effects)
        {
            if (effects == null || effects.Count == 0)
            {
                return null;
            }

            var effectsElement = new XElement("effects");
            foreach (AbilityEffectConfig? effect in effects)
            {
                if (effect == null)
                {
                    continue;
                }

                effectsElement.Add(new XElement("li",
                    new XElement("type", effect.type.ToString()),
                    new XElement("amount", effect.amount),
                    new XElement("duration", effect.duration),
                    new XElement("chance", effect.chance),
                    effect.damageDef != null ? new XElement("damageDef", effect.damageDef.defName) : null,
                    effect.hediffDef != null ? new XElement("hediffDef", effect.hediffDef.defName) : null,
                    effect.summonKind != null ? new XElement("summonKind", effect.summonKind.defName) : null,
                    effect.summonFactionDef != null ? new XElement("summonFactionDef", effect.summonFactionDef.defName) : null,
                    new XElement("summonCount", effect.summonCount),
                    new XElement("controlMode", effect.controlMode.ToString()),
                    new XElement("controlMoveDistance", effect.controlMoveDistance),
                    new XElement("terraformMode", effect.terraformMode.ToString()),
                    effect.terraformThingDef != null ? new XElement("terraformThingDef", effect.terraformThingDef.defName) : null,
                    effect.terraformTerrainDef != null ? new XElement("terraformTerrainDef", effect.terraformTerrainDef.defName) : null,
                    new XElement("terraformSpawnCount", effect.terraformSpawnCount),
                    new XElement("canHurtSelf", SerializeBool(effect.canHurtSelf)),
                    !string.IsNullOrWhiteSpace(effect.weatherDefName) ? new XElement("weatherDefName", effect.weatherDefName) : null,
                    new XElement("weatherDurationTicks", effect.weatherDurationTicks),
                    new XElement("weatherTransitionTicks", effect.weatherTransitionTicks)
                ));
            }

            return effectsElement;
        }

        internal static XElement? GenerateAbilityVisualEffectsElement(List<AbilityVisualEffectConfig>? visualEffects)
        {
            if (visualEffects == null || visualEffects.Count == 0)
            {
                return null;
            }

            var root = new XElement("visualEffects");
            foreach (AbilityVisualEffectConfig? visualEffect in visualEffects)
            {
                if (visualEffect == null)
                {
                    continue;
                }

                visualEffect.NormalizeLegacyData();
                visualEffect.SyncLegacyFields();

                root.Add(new XElement("li",
                    new XElement("enabled", SerializeBool(visualEffect.enabled)),
                    new XElement("type", visualEffect.type.ToString()),
                    new XElement("sourceMode", visualEffect.sourceMode.ToString()),
                    new XElement("spatialMode", visualEffect.spatialMode.ToString()),
                    new XElement("anchorMode", visualEffect.anchorMode.ToString()),
                    new XElement("secondaryAnchorMode", visualEffect.secondaryAnchorMode.ToString()),
                    new XElement("pathMode", visualEffect.pathMode.ToString()),
                    new XElement("facingMode", visualEffect.facingMode.ToString()),
                    visualEffect.UsesCustomTextureType ? new XElement("textureSource", visualEffect.textureSource.ToString()) : null,
                    !string.IsNullOrWhiteSpace(visualEffect.presetDefName) ? new XElement("presetDefName", visualEffect.presetDefName) : null,
                    !string.IsNullOrWhiteSpace(visualEffect.customTexturePath) ? new XElement("customTexturePath", visualEffect.customTexturePath) : null,
                    !string.IsNullOrWhiteSpace(visualEffect.assetBundlePath) ? new XElement("assetBundlePath", visualEffect.assetBundlePath) : null,
                    !string.IsNullOrWhiteSpace(visualEffect.assetBundleEffectName) ? new XElement("assetBundleEffectName", visualEffect.assetBundleEffectName) : null,
                    !string.IsNullOrWhiteSpace(visualEffect.assetBundleTextureName) ? new XElement("assetBundleTextureName", visualEffect.assetBundleTextureName) : null,
                    new XElement("assetBundleEffectScale", visualEffect.assetBundleEffectScale),
                    new XElement("bundleRenderStrategy", visualEffect.bundleRenderStrategy.ToString()),
                    !string.IsNullOrWhiteSpace(visualEffect.shaderPath) ? new XElement("shaderPath", visualEffect.shaderPath) : null,
                    !string.IsNullOrWhiteSpace(visualEffect.shaderAssetBundlePath) ? new XElement("shaderAssetBundlePath", visualEffect.shaderAssetBundlePath) : null,
                    !string.IsNullOrWhiteSpace(visualEffect.shaderAssetBundleShaderName) ? new XElement("shaderAssetBundleShaderName", visualEffect.shaderAssetBundleShaderName) : null,
                    new XElement("shaderLoadFromAssetBundle", SerializeBool(visualEffect.shaderLoadFromAssetBundle)),
                    !string.IsNullOrWhiteSpace(visualEffect.shaderTexturePath) ? new XElement("shaderTexturePath", visualEffect.shaderTexturePath) : null,
                    new XElement("shaderTintColor", XmlExportHelper.FormatColor(visualEffect.shaderTintColor)),
                    new XElement("shaderIntensity", visualEffect.shaderIntensity),
                    new XElement("shaderSpeed", visualEffect.shaderSpeed),
                    new XElement("shaderParam1", visualEffect.shaderParam1),
                    new XElement("shaderParam2", visualEffect.shaderParam2),
                    new XElement("shaderParam3", visualEffect.shaderParam3),
                    new XElement("shaderParam4", visualEffect.shaderParam4),
                    new XElement("target", visualEffect.target.ToString()),
                    new XElement("trigger", visualEffect.trigger.ToString()),
                    new XElement("delayTicks", visualEffect.delayTicks),
                    new XElement("displayDurationTicks", visualEffect.displayDurationTicks),
                    new XElement("globalFilterMode", visualEffect.globalFilterMode.ToString()),
                    new XElement("globalFilterTransition", visualEffect.globalFilterTransition.ToString()),
                    new XElement("globalFilterTransitionTicks", visualEffect.globalFilterTransitionTicks),
                    visualEffect.linkedExpression.HasValue ? new XElement("linkedExpression", visualEffect.linkedExpression.Value.ToString()) : null,
                    new XElement("linkedExpressionDurationTicks", visualEffect.linkedExpressionDurationTicks),
                    new XElement("linkedPupilBrightnessOffset", visualEffect.linkedPupilBrightnessOffset),
                    new XElement("linkedPupilContrastOffset", visualEffect.linkedPupilContrastOffset),
                    new XElement("scale", visualEffect.scale),
                    new XElement("drawSize", visualEffect.drawSize),
                    new XElement("useCasterFacing", SerializeBool(visualEffect.useCasterFacing)),
                    new XElement("forwardOffset", visualEffect.forwardOffset),
                    new XElement("sideOffset", visualEffect.sideOffset),
                    new XElement("heightOffset", visualEffect.heightOffset),
                    new XElement("rotation", visualEffect.rotation),
                    visualEffect.textureScale != Vector2.one ? new XElement("textureScale", XmlExportHelper.FormatVector2(visualEffect.textureScale)) : null,
                    new XElement("lineWidth", visualEffect.lineWidth),
                    new XElement("wallHeight", visualEffect.wallHeight),
                    new XElement("wallThickness", visualEffect.wallThickness),
                    new XElement("tileByLength", SerializeBool(visualEffect.tileByLength)),
                    new XElement("followGround", SerializeBool(visualEffect.followGround)),
                    new XElement("segmentCount", visualEffect.segmentCount),
                    new XElement("revealBySegments", SerializeBool(visualEffect.revealBySegments)),
                    new XElement("segmentRevealIntervalTicks", visualEffect.segmentRevealIntervalTicks),
                    visualEffect.offset != Vector3.zero ? new XElement("offset", XmlExportHelper.FormatVector3(visualEffect.offset)) : null,
                    new XElement("repeatCount", visualEffect.repeatCount),
                    new XElement("repeatIntervalTicks", visualEffect.repeatIntervalTicks),
                    new XElement("attachToPawn", SerializeBool(visualEffect.attachToPawn)),
                    new XElement("attachToTargetCell", SerializeBool(visualEffect.attachToTargetCell)),
                    new XElement("playSound", SerializeBool(visualEffect.playSound)),
                    !string.IsNullOrWhiteSpace(visualEffect.soundDefName) ? new XElement("soundDefName", visualEffect.soundDefName) : null,
                    new XElement("soundDelayTicks", visualEffect.soundDelayTicks),
                    new XElement("soundVolume", visualEffect.soundVolume),
                    new XElement("soundPitch", visualEffect.soundPitch)
                ));
            }

            return root;
        }

        internal static XElement? GenerateRuntimeComponentsElement(List<AbilityRuntimeComponentConfig>? components)
        {
            if (components == null || components.Count == 0)
            {
                return null;
            }

            var root = new XElement("runtimeComponents");
            foreach (AbilityRuntimeComponentConfig? component in components)
            {
                if (component == null)
                {
                    continue;
                }

                root.Add(new XElement("li",
                    new XElement("type", component.type.ToString()),
                    new XElement("enabled", SerializeBool(component.enabled)),
                    new XElement("comboWindowTicks", component.comboWindowTicks),
                    new XElement("comboTargetHotkeySlot", component.comboTargetHotkeySlot.ToString()),
                    !string.IsNullOrWhiteSpace(component.comboTargetAbilityDefName) ? new XElement("comboTargetAbilityDefName", component.comboTargetAbilityDefName) : null,
                    new XElement("cooldownTicks", component.cooldownTicks),
                    new XElement("jumpDistance", component.jumpDistance),
                    new XElement("findCellRadius", component.findCellRadius),
                    new XElement("triggerAbilityEffectsAfterJump", SerializeBool(component.triggerAbilityEffectsAfterJump)),
                    new XElement("useMouseTargetCell", SerializeBool(component.useMouseTargetCell)),
                    new XElement("smartCastOffsetCells", component.smartCastOffsetCells),
                    new XElement("smartCastClampToMaxDistance", SerializeBool(component.smartCastClampToMaxDistance)),
                    new XElement("smartCastAllowFallbackForward", SerializeBool(component.smartCastAllowFallbackForward)),
                    new XElement("overrideHotkeySlot", component.overrideHotkeySlot.ToString()),
                    new XElement("overrideAbilityDefName", component.overrideAbilityDefName ?? string.Empty),
                    new XElement("overrideDurationTicks", component.overrideDurationTicks),
                    new XElement("followupCooldownHotkeySlot", component.followupCooldownHotkeySlot.ToString()),
                    new XElement("followupCooldownTicks", component.followupCooldownTicks),
                    new XElement("requiredStacks", component.requiredStacks),
                    new XElement("delayTicks", component.delayTicks),
                    new XElement("wave1Radius", component.wave1Radius),
                    new XElement("wave1Damage", component.wave1Damage),
                    new XElement("wave2Radius", component.wave2Radius),
                    new XElement("wave2Damage", component.wave2Damage),
                    new XElement("wave3Radius", component.wave3Radius),
                    new XElement("wave3Damage", component.wave3Damage),
                    component.waveDamageDef != null ? new XElement("waveDamageDef", component.waveDamageDef.defName) : null,
                    new XElement("pulseIntervalTicks", component.pulseIntervalTicks),
                    new XElement("pulseTotalTicks", component.pulseTotalTicks),
                    new XElement("pulseStartsImmediately", SerializeBool(component.pulseStartsImmediately)),
                    new XElement("killRefreshHotkeySlot", component.killRefreshHotkeySlot.ToString()),
                    new XElement("killRefreshCooldownPercent", component.killRefreshCooldownPercent),
                    new XElement("shieldMaxDamage", component.shieldMaxDamage),
                    new XElement("shieldDurationTicks", component.shieldDurationTicks),
                    new XElement("shieldHealRatio", component.shieldHealRatio),
                    new XElement("shieldBonusDamageRatio", component.shieldBonusDamageRatio),
                    new XElement("shieldVisualScale", component.shieldVisualScale),
                    new XElement("shieldVisualHeightOffset", component.shieldVisualHeightOffset),
                    !string.IsNullOrWhiteSpace(component.shieldInterceptorThingDefName) ? new XElement("shieldInterceptorThingDefName", component.shieldInterceptorThingDefName) : null,
                    new XElement("shieldInterceptorDurationTicks", component.shieldInterceptorDurationTicks),
                    new XElement("maxBounceCount", component.maxBounceCount),
                    new XElement("bounceRange", component.bounceRange),
                    new XElement("bounceDamageFalloff", component.bounceDamageFalloff),
                    new XElement("executeThresholdPercent", component.executeThresholdPercent),
                    new XElement("executeBonusDamageScale", component.executeBonusDamageScale),
                    new XElement("missingHealthBonusPerTenPercent", component.missingHealthBonusPerTenPercent),
                    new XElement("missingHealthBonusMaxScale", component.missingHealthBonusMaxScale),
                    new XElement("fullHealthThresholdPercent", component.fullHealthThresholdPercent),
                    new XElement("fullHealthBonusDamageScale", component.fullHealthBonusDamageScale),
                    new XElement("nearbyEnemyBonusMaxTargets", component.nearbyEnemyBonusMaxTargets),
                    new XElement("nearbyEnemyBonusPerTarget", component.nearbyEnemyBonusPerTarget),
                    new XElement("nearbyEnemyBonusRadius", component.nearbyEnemyBonusRadius),
                    new XElement("isolatedTargetRadius", component.isolatedTargetRadius),
                    new XElement("isolatedTargetBonusDamageScale", component.isolatedTargetBonusDamageScale),
                    new XElement("markDurationTicks", component.markDurationTicks),
                    new XElement("markMaxStacks", component.markMaxStacks),
                    new XElement("markDetonationDamage", component.markDetonationDamage),
                    component.markDamageDef != null ? new XElement("markDamageDef", component.markDamageDef.defName) : null,
                    new XElement("comboStackWindowTicks", component.comboStackWindowTicks),
                    new XElement("comboStackMax", component.comboStackMax),
                    new XElement("comboStackBonusDamagePerStack", component.comboStackBonusDamagePerStack),
                    new XElement("slowFieldDurationTicks", component.slowFieldDurationTicks),
                    new XElement("slowFieldRadius", component.slowFieldRadius),
                    new XElement("slowFieldHediffDefName", component.slowFieldHediffDefName ?? string.Empty),
                    new XElement("pierceMaxTargets", component.pierceMaxTargets),
                    new XElement("pierceBonusDamagePerTarget", component.pierceBonusDamagePerTarget),
                    new XElement("pierceSearchRange", component.pierceSearchRange),
                    new XElement("dashEmpowerDurationTicks", component.dashEmpowerDurationTicks),
                    new XElement("dashEmpowerBonusDamageScale", component.dashEmpowerBonusDamageScale),
                    new XElement("hitHealAmount", component.hitHealAmount),
                    new XElement("hitHealRatio", component.hitHealRatio),
                    new XElement("refundHotkeySlot", component.refundHotkeySlot.ToString()),
                    new XElement("hitCooldownRefundPercent", component.hitCooldownRefundPercent),
                    new XElement("splitProjectileCount", component.splitProjectileCount),
                    new XElement("splitDamageScale", component.splitDamageScale),
                    new XElement("splitSearchRange", component.splitSearchRange),
                    new XElement("flightDurationTicks", component.flightDurationTicks),
                    new XElement("flightHeightFactor", component.flightHeightFactor),
                    new XElement("suppressCombatActionsDuringFlightState", SerializeBool(component.suppressCombatActionsDuringFlightState)),
                    new XElement("flyerThingDefName", component.flyerThingDefName ?? string.Empty),
                    new XElement("flyerWarmupTicks", component.flyerWarmupTicks),
                    new XElement("launchFromCasterPosition", SerializeBool(component.launchFromCasterPosition)),
                    new XElement("requireValidTargetCell", SerializeBool(component.requireValidTargetCell)),
                    new XElement("storeTargetForFollowup", SerializeBool(component.storeTargetForFollowup)),
                    new XElement("enableFlightOnlyWindow", SerializeBool(component.enableFlightOnlyWindow)),
                    new XElement("flightOnlyWindowTicks", component.flightOnlyWindowTicks),
                    new XElement("flightOnlyAbilityDefName", component.flightOnlyAbilityDefName ?? string.Empty),
                    new XElement("hideCasterDuringTakeoff", SerializeBool(component.hideCasterDuringTakeoff)),
                    new XElement("autoExpireFlightMarkerOnLanding", SerializeBool(component.autoExpireFlightMarkerOnLanding)),
                    new XElement("requiredFlightSourceAbilityDefName", component.requiredFlightSourceAbilityDefName ?? string.Empty),
                    new XElement("requireReservedTargetCell", SerializeBool(component.requireReservedTargetCell)),
                    new XElement("consumeFlightStateOnCast", SerializeBool(component.consumeFlightStateOnCast)),
                    new XElement("onlyUseDuringFlightWindow", SerializeBool(component.onlyUseDuringFlightWindow)),
                    new XElement("landingBurstRadius", component.landingBurstRadius),
                    new XElement("landingBurstDamage", component.landingBurstDamage),
                    component.landingBurstDamageDef != null ? new XElement("landingBurstDamageDef", component.landingBurstDamageDef.defName) : null,
                    new XElement("landingEffecterDefName", component.landingEffecterDefName ?? string.Empty),
                    new XElement("landingSoundDefName", component.landingSoundDefName ?? string.Empty),
                    new XElement("affectBuildings", SerializeBool(component.affectBuildings)),
                    new XElement("affectCells", SerializeBool(component.affectCells)),
                    new XElement("knockbackTargets", SerializeBool(component.knockbackTargets)),
                    new XElement("knockbackDistance", component.knockbackDistance),
                    new XElement("timeStopDurationTicks", component.timeStopDurationTicks),
                    new XElement("freezeVisualsDuringTimeStop", SerializeBool(component.freezeVisualsDuringTimeStop)),
                    !string.IsNullOrWhiteSpace(component.weatherDefName) ? new XElement("weatherDefName", component.weatherDefName) : null,
                    new XElement("weatherDurationTicks", component.weatherDurationTicks),
                    new XElement("weatherTransitionTicks", component.weatherTransitionTicks)
                ));
            }

            return root;
        }

        internal static XElement? GenerateAbilityHotkeysElement(SkinAbilityHotkeyConfig? hotkeys)
        {
            if (hotkeys == null)
            {
                return null;
            }

            var elements = new List<object?>
            {
                new XElement("enabled", SerializeBool(hotkeys.enabled))
            };

            foreach (var kvp in hotkeys.slotBindings)
            {
                if (!string.IsNullOrEmpty(kvp.Value))
                    elements.Add(new XElement(kvp.Key.ToLowerInvariant() + "AbilityDefName", kvp.Value));
            }

            return new XElement("abilityHotkeys", elements.ToArray());
        }

        private static XElement? GenerateAbilityElement(ModularAbilityDef? ability)
        {
            if (ability == null || string.IsNullOrWhiteSpace(ability.defName))
            {
                return null;
            }

            return new XElement("li",
                new XElement("defName", ability.defName),
                !string.IsNullOrWhiteSpace(ability.label) ? new XElement("label", ability.label) : null,
                !string.IsNullOrWhiteSpace(ability.description) ? new XElement("description", ability.description) : null,
                !string.IsNullOrWhiteSpace(ability.iconPath) ? new XElement("iconPath", ability.iconPath) : null,
                new XElement("cooldownTicks", ability.cooldownTicks),
                new XElement("warmupTicks", ability.warmupTicks),
                new XElement("charges", ability.charges),
                new XElement("aiCanUse", ability.aiCanUse),
                new XElement("carrierType", ability.carrierType.ToString()),
                new XElement("targetType", ability.targetType.ToString()),
                new XElement("useRadius", SerializeBool(ability.useRadius)),
                new XElement("areaCenter", ability.areaCenter.ToString()),
                new XElement("areaShape", ability.areaShape.ToString()),
                !string.IsNullOrWhiteSpace(ability.irregularAreaPattern) ? new XElement("irregularAreaPattern", ability.irregularAreaPattern) : null,
                new XElement("range", ability.range),
                new XElement("radius", ability.radius),
                ability.projectileDef != null ? new XElement("projectileDef", ability.projectileDef.defName) : null,
                GenerateAbilityEffectsElement(ability.effects),
                GenerateAbilityVisualEffectsElement(ability.visualEffects),
                GenerateRuntimeComponentsElement(ability.runtimeComponents)
            );
        }

        private static string SerializeBool(bool value)
        {
            return value.ToString().ToLowerInvariant();
        }
    }
}
