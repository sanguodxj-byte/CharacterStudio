using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Abilities;
using CharacterStudio.Rendering;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_AbilityEditor
    {
        private enum AbilityPreviewEventType
        {
            CastStart,
            Warmup,
            CastFinish,
            Projectile,
            Effect,
            VisualEffect,
            RuntimeComponent,
            Summary
        }

        private enum AbilityPreviewInteractionMode
        {
            None,
            Move,
            Rotate
        }

        private sealed class AbilityPreviewContext
        {
            public Vector2 casterPos = new Vector2(0.28f, 0.78f);
            public Vector2 targetPos = new Vector2(0.74f, 0.36f);
            public float playbackSpeed = 1f;
            public bool showGrid = true;
            public bool showTimeline = true;
        }

        private sealed class AbilityPreviewEvent
        {
            public int tick;
            public AbilityPreviewEventType type;
            public string label = string.Empty;
            public string detail = string.Empty;
            public Vector2 normalizedPos = new Vector2(0.5f, 0.5f);
            public float radius = 0f;
            public AbilityAreaShape areaShape = AbilityAreaShape.Circle;
            public string irregularPattern = string.Empty;
            public bool areaCenteredOnCaster;
            public Color color = Color.white;
            public int order;
            public string previewTexturePath = string.Empty;
            public Vector2 previewTextureScale = Vector2.one;
            public float previewRotation;
            public float previewDrawSize = 1f;
            public AbilityVisualEffectConfig? sourceVfx;
            public int sourceVfxIndex = -1;
            public int repeatIndex;
            public Rect lastTextureRect;
            public float lastTextureRotation;
        }

        private sealed class AbilityPreviewPlan
        {
            public string title = string.Empty;
            public string summary = string.Empty;
            public int totalTicks;
            public readonly List<AbilityPreviewEvent> events = new List<AbilityPreviewEvent>();
            public readonly List<string> summaries = new List<string>();
            public readonly List<string> notes = new List<string>();
        }

        private sealed class AbilityPreviewInteractionState
        {
            public int selectedVfxIndex = -1;
            public int selectedRepeatIndex = -1;
            public AbilityPreviewInteractionMode mode;
            public Vector2 dragStartMouse;
            public float startForwardOffset;
            public float startSideOffset;
            public float startRotation;
            public Vector2 startTextureScale = Vector2.one;
        }

        private readonly AbilityPreviewContext abilityPreviewContext = new AbilityPreviewContext();
        private readonly AbilityPreviewInteractionState abilityPreviewInteraction = new AbilityPreviewInteractionState();
        private Vector2 abilityPreviewLogScrollPos;
        private Vector2 abilityPreviewSummaryScrollPos;
        private float abilityPreviewTimeTicks;
        private float abilityPreviewLastRealtime = -1f;
        private bool abilityPreviewPlaying;
        private bool abilityPreviewLoop;
        private bool abilityPreviewDirty = true;
        private AbilityPreviewPlan? cachedAbilityPreviewPlan;
        private const int PreviewGridCellsPerAxis = 12;
        private const float PreviewHeightPixelsFactor = 0.65f;
        private const float PreviewScaleWheelStep = 0.08f;
        private const float PreviewScaleMin = 0.1f;
        private const float PreviewScaleMax = 20f;
        private const float PreviewRotationMin = -360f;
        private const float PreviewRotationMax = 360f;

        private void NotifyAbilityPreviewDirty(bool resetPlayback = false)
        {
            abilityPreviewDirty = true;
            cachedAbilityPreviewPlan = null;
            if (resetPlayback)
            {
                ResetAbilityPreviewPlayback(false);
            }
        }

        private void ResetAbilityPreviewPlayback(bool keepPlayingState)
        {
            abilityPreviewTimeTicks = 0f;
            abilityPreviewLastRealtime = Time.realtimeSinceStartup;
            if (!keepPlayingState)
            {
                abilityPreviewPlaying = false;
            }
        }

        private AbilityPreviewPlan BuildAbilityPreviewPlan()
        {
            if (!abilityPreviewDirty && cachedAbilityPreviewPlan != null)
            {
                return cachedAbilityPreviewPlan;
            }

            var ability = selectedAbility;
            var plan = new AbilityPreviewPlan();
            if (ability == null)
            {
                plan.title = "CS_Studio_Ability_PreviewEmptyTitle".Translate();
                plan.summary = "CS_Studio_Ability_PreviewEmptyHint".Translate();
                cachedAbilityPreviewPlan = plan;
                abilityPreviewDirty = false;
                return plan;
            }

            plan.title = string.IsNullOrWhiteSpace(ability.label) ? ability.defName ?? "<unnamed>" : ability.label;
            string carrier = GetCarrierTypeLabel(ModularAbilityDefExtensions.NormalizeCarrierType(ability.carrierType));
            string target = GetTargetTypeLabel(ModularAbilityDefExtensions.NormalizeTargetType(ability));
            plan.summary = "CS_Studio_Ability_PreviewSummaryLine".Translate(carrier, target);

            plan.events.Add(new AbilityPreviewEvent
            {
                tick = 0,
                type = AbilityPreviewEventType.CastStart,
                label = "CS_Studio_Ability_PreviewEventCastStart".Translate(),
                detail = "CS_Studio_Ability_PreviewEventCasterOrigin".Translate(),
                normalizedPos = abilityPreviewContext.casterPos,
                color = new Color(0.42f, 0.88f, 1f),
                order = 0
            });

            int warmupTicks = Math.Max(0, (int)ability.warmupTicks);
            if (warmupTicks > 0)
            {
                plan.events.Add(new AbilityPreviewEvent
                {
                    tick = warmupTicks,
                    type = AbilityPreviewEventType.Warmup,
                    label = "CS_Studio_Ability_PreviewEventWarmup".Translate(),
                    detail = "CS_Studio_Ability_PreviewWarmupDetail".Translate(warmupTicks.ToString()),
                    normalizedPos = Vector2.Lerp(abilityPreviewContext.casterPos, abilityPreviewContext.targetPos, 0.2f),
                    color = new Color(1f, 0.82f, 0.35f),
                    order = 1
                });
            }

            int castFinishTick = warmupTicks;
            plan.events.Add(new AbilityPreviewEvent
            {
                tick = castFinishTick,
                type = AbilityPreviewEventType.CastFinish,
                label = "CS_Studio_Ability_PreviewEventCastFinish".Translate(),
                detail = "CS_Studio_Ability_PreviewCastReleaseDetail".Translate(),
                normalizedPos = abilityPreviewContext.casterPos,
                color = new Color(0.32f, 1f, 0.68f),
                order = 2
            });

            AbilityCarrierType normalizedCarrier = ModularAbilityDefExtensions.NormalizeCarrierType(ability.carrierType);
            AbilityTargetType normalizedTarget = ModularAbilityDefExtensions.NormalizeTargetType(ability);
            Vector2 impactPos = ResolvePreviewImpactPosition(normalizedCarrier, normalizedTarget);
            float radius = ability.useRadius ? Mathf.Max(0f, ability.radius) : 0f;
            AbilityAreaShape areaShape = ModularAbilityDefExtensions.NormalizeAreaShape(ability);
            bool areaCenteredOnCaster = ModularAbilityDefExtensions.NormalizeAreaCenter(ability) == AbilityAreaCenter.Self;

            if (normalizedCarrier == AbilityCarrierType.Projectile)
            {
                plan.events.Add(new AbilityPreviewEvent
                {
                    tick = castFinishTick,
                    type = AbilityPreviewEventType.Projectile,
                    label = "CS_Studio_Ability_PreviewEventProjectile".Translate(),
                    detail = ability.projectileDef?.label ?? "CS_Studio_Ability_PreviewProjectileGeneric".Translate(),
                    normalizedPos = Vector2.Lerp(abilityPreviewContext.casterPos, impactPos, 0.55f),
                    color = new Color(1f, 0.52f, 0.3f),
                    order = 3
                });
            }

            if (ability.effects != null)
            {
                for (int i = 0; i < ability.effects.Count; i++)
                {
                    AbilityEffectConfig? effect = ability.effects[i];
                    if (effect == null)
                        continue;

                    string summary = BuildEffectSummary(effect);
                    plan.summaries.Add($"[{i + 1}] {summary}");
                    plan.events.Add(new AbilityPreviewEvent
                    {
                        tick = castFinishTick,
                        type = AbilityPreviewEventType.Effect,
                        label = "CS_Studio_Ability_PreviewEventEffect".Translate(),
                        detail = summary,
                        normalizedPos = ResolveEffectPreviewPosition(effect, normalizedTarget, impactPos),
                        radius = radius,
                        areaShape = areaShape,
                        irregularPattern = ability.irregularAreaPattern ?? string.Empty,
                        areaCenteredOnCaster = areaCenteredOnCaster,
                        color = GetEffectPreviewColor(effect.type),
                        order = 10 + i
                    });
                }
            }

            if (ability.visualEffects != null)
            {
                for (int i = 0; i < ability.visualEffects.Count; i++)
                {
                    AbilityVisualEffectConfig? vfx = ability.visualEffects[i];
                    if (vfx == null || !vfx.enabled)
                        continue;

                    int repeatCount = Math.Max(1, vfx.repeatCount);
                    int repeatInterval = Math.Max(0, vfx.repeatIntervalTicks);
                    for (int repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
                    {
                        int tick = castFinishTick + Math.Max(0, vfx.delayTicks) + repeatIndex * repeatInterval;
                        plan.events.Add(new AbilityPreviewEvent
                        {
                            tick = tick,
                            type = AbilityPreviewEventType.VisualEffect,
                            label = "CS_Studio_Ability_PreviewEventVfx".Translate(),
                            detail = BuildVfxSummary(vfx, repeatIndex),
                            normalizedPos = ResolveVfxPreviewPosition(vfx, impactPos),
                            radius = GetPreviewRadiusForVfx(vfx, radius),
                            areaShape = areaShape,
                            irregularPattern = ability.irregularAreaPattern ?? string.Empty,
                            areaCenteredOnCaster = areaCenteredOnCaster,
                            color = GetVfxPreviewColor(vfx),
                            order = 200 + i * 10 + repeatIndex,
                            previewTexturePath = vfx.UsesCustomTextureType
                                ? vfx.customTexturePath ?? string.Empty
                                : string.Empty,
                            previewTextureScale = vfx.textureScale,
                            previewRotation = vfx.rotation,
                            previewDrawSize = vfx.drawSize,
                            sourceVfx = vfx,
                            sourceVfxIndex = i,
                            repeatIndex = repeatIndex
                        });
                    }
                }
            }

            if (ability.runtimeComponents != null)
            {
                for (int i = 0; i < ability.runtimeComponents.Count; i++)
                {
                    AbilityRuntimeComponentConfig? component = ability.runtimeComponents[i];
                    if (component == null || !component.enabled)
                        continue;

                    string summary = BuildRuntimeComponentSummary(component);
                    plan.notes.Add(summary);
                    plan.events.Add(new AbilityPreviewEvent
                    {
                        tick = castFinishTick,
                        type = AbilityPreviewEventType.RuntimeComponent,
                        label = "CS_Studio_Ability_PreviewEventRuntime".Translate(),
                        detail = summary,
                        normalizedPos = abilityPreviewContext.casterPos,
                        color = new Color(0.72f, 0.58f, 1f),
                        order = 400 + i
                    });
                }
            }

            plan.events.Sort((a, b) =>
            {
                int tickCompare = a.tick.CompareTo(b.tick);
                return tickCompare != 0 ? tickCompare : a.order.CompareTo(b.order);
            });

            int lastTick = plan.events.Count > 0 ? plan.events.Max(e => e.tick) : 0;
            plan.totalTicks = Math.Max(90, lastTick + 45);
            if (!ability.useRadius)
            {
                plan.notes.Add("CS_Studio_Ability_PreviewNoRadiusNote".Translate());
            }
            if ((ability.effects == null || ability.effects.Count == 0) && (ability.visualEffects == null || ability.visualEffects.Count == 0))
            {
                plan.notes.Add("CS_Studio_Ability_PreviewNoPayloadNote".Translate());
            }

            cachedAbilityPreviewPlan = plan;
            abilityPreviewDirty = false;
            return plan;
        }

        private Vector2 ResolvePreviewImpactPosition(AbilityCarrierType carrierType, AbilityTargetType targetType)
        {
            if (carrierType == AbilityCarrierType.Self || targetType == AbilityTargetType.Self)
            {
                return abilityPreviewContext.casterPos;
            }

            return abilityPreviewContext.targetPos;
        }

        private Vector2 ResolveEffectPreviewPosition(AbilityEffectConfig effect, AbilityTargetType targetType, Vector2 impactPos)
        {
            return ResolveUnifiedPreviewAnchor(targetType == AbilityTargetType.Self, impactPos);
        }

        private Vector2 ResolveVfxPreviewPosition(AbilityVisualEffectConfig vfx, Vector2 impactPos)
        {
            return ResolveUnifiedPreviewAnchor(vfx.target == VisualEffectTarget.Caster, impactPos);
        }

        private Vector2 ResolveUnifiedPreviewAnchor(bool useCasterAnchor, Vector2 impactPos)
        {
            return useCasterAnchor ? abilityPreviewContext.casterPos : impactPos;
        }

        private Vector2 ResolvePreviewCastDirection()
        {
            Vector2 dir = abilityPreviewContext.targetPos - abilityPreviewContext.casterPos;
            if (dir.sqrMagnitude < 0.0001f)
            {
                return new Vector2(0f, -1f);
            }

            return dir.normalized;
        }

        private Vector2 ResolvePreviewSideDirection(Vector2 forwardDir)
        {
            return new Vector2(forwardDir.y, -forwardDir.x);
        }

        private Rect GetPreviewGridRect(Rect drawRect)
        {
            return drawRect;
        }

        private float GetPreviewCellSize(Rect drawRect)
        {
            Rect gridRect = GetPreviewGridRect(drawRect);
            float cellWidth = Mathf.Max(1f, gridRect.width / PreviewGridCellsPerAxis);
            float cellHeight = Mathf.Max(1f, gridRect.height / PreviewGridCellsPerAxis);
            return Mathf.Min(cellWidth, cellHeight);
        }

        private Vector2 GetPreviewCellStep(Rect drawRect)
        {
            Rect gridRect = GetPreviewGridRect(drawRect);
            return new Vector2(
                Mathf.Max(1f, gridRect.width / PreviewGridCellsPerAxis),
                Mathf.Max(1f, gridRect.height / PreviewGridCellsPerAxis));
        }

        private float GetPreviewPixelsPerOffsetUnit(Rect drawRect)
        {
            Vector2 cellStep = GetPreviewCellStep(drawRect);
            return Mathf.Min(cellStep.x, cellStep.y);
        }

        private Vector2 ResolvePreviewEventCanvasPosition(Rect drawRect, AbilityPreviewEvent evt)
        {
            Vector2 pos = ToCanvasPosition(drawRect, evt.normalizedPos);
            AbilityVisualEffectConfig? vfx = evt.sourceVfx;
            if (vfx == null)
            {
                return pos;
            }

            Vector2 forwardDir = ResolvePreviewCastDirection();
            Vector2 sideDir = ResolvePreviewSideDirection(forwardDir);
            float pixelsPerUnit = GetPreviewPixelsPerOffsetUnit(drawRect);
            Vector2 offset = forwardDir * (vfx.forwardOffset * pixelsPerUnit)
                + sideDir * (vfx.sideOffset * pixelsPerUnit)
                + new Vector2(0f, -vfx.heightOffset * pixelsPerUnit * PreviewHeightPixelsFactor);
            return pos + offset;
        }

        private AbilityVisualEffectConfig? GetPreviewVisualEffectByIndex(int index)
        {
            if (selectedAbility?.visualEffects == null || index < 0 || index >= selectedAbility.visualEffects.Count)
            {
                return null;
            }

            return selectedAbility.visualEffects[index];
        }

        private bool IsPreviewEventSelected(AbilityPreviewEvent evt)
        {
            return evt.sourceVfxIndex >= 0
                && evt.sourceVfxIndex == abilityPreviewInteraction.selectedVfxIndex
                && evt.repeatIndex == abilityPreviewInteraction.selectedRepeatIndex;
        }

        private void SelectPreviewEvent(AbilityPreviewEvent evt)
        {
            abilityPreviewInteraction.selectedVfxIndex = evt.sourceVfxIndex;
            abilityPreviewInteraction.selectedRepeatIndex = evt.repeatIndex;
        }

        private void ClearPreviewEventSelection()
        {
            abilityPreviewInteraction.selectedVfxIndex = -1;
            abilityPreviewInteraction.selectedRepeatIndex = -1;
        }

        private AbilityPreviewEvent? GetSelectedPreviewEvent(AbilityPreviewPlan plan)
        {
            return plan.events.FirstOrDefault(IsPreviewEventSelected);
        }

        private AbilityPreviewEvent? GetInteractivePreviewEventAt(AbilityPreviewPlan plan, Vector2 mousePos)
        {
            for (int i = plan.events.Count - 1; i >= 0; i--)
            {
                AbilityPreviewEvent evt = plan.events[i];
                if (evt.sourceVfx == null || string.IsNullOrWhiteSpace(evt.previewTexturePath))
                {
                    continue;
                }

                if (evt.tick > abilityPreviewTimeTicks || evt.lastTextureRect.width <= 0.1f || evt.lastTextureRect.height <= 0.1f)
                {
                    continue;
                }

                if (PointInRotatedRect(mousePos, evt.lastTextureRect, evt.lastTextureRotation))
                {
                    return evt;
                }
            }

            return null;
        }

        private void BeginPreviewInteraction(AbilityPreviewEvent evt, AbilityPreviewInteractionMode mode, Vector2 mousePos)
        {
            SelectPreviewEvent(evt);
            abilityPreviewInteraction.mode = mode;
            abilityPreviewInteraction.dragStartMouse = mousePos;
            abilityPreviewInteraction.startForwardOffset = evt.sourceVfx?.forwardOffset ?? 0f;
            abilityPreviewInteraction.startSideOffset = evt.sourceVfx?.sideOffset ?? 0f;
            abilityPreviewInteraction.startRotation = evt.sourceVfx?.rotation ?? evt.previewRotation;
            abilityPreviewInteraction.startTextureScale = evt.sourceVfx?.textureScale ?? evt.previewTextureScale;
        }

        private void EndPreviewInteraction()
        {
            abilityPreviewInteraction.mode = AbilityPreviewInteractionMode.None;
        }

        private void ApplyPreviewMove(Rect drawRect, AbilityPreviewEvent evt, Vector2 currentMousePos)
        {
            AbilityVisualEffectConfig? vfx = evt.sourceVfx;
            if (vfx == null)
            {
                return;
            }

            Vector2 delta = currentMousePos - abilityPreviewInteraction.dragStartMouse;
            Vector2 forwardDir = ResolvePreviewCastDirection();
            Vector2 sideDir = ResolvePreviewSideDirection(forwardDir);
            float pixelsPerUnit = GetPreviewPixelsPerOffsetUnit(drawRect);
            float forwardDelta = Vector2.Dot(delta, forwardDir) / pixelsPerUnit;
            float sideDelta = Vector2.Dot(delta, sideDir) / pixelsPerUnit;
            float newForward = Mathf.Clamp(abilityPreviewInteraction.startForwardOffset + forwardDelta, -20f, 20f);
            float newSide = Mathf.Clamp(abilityPreviewInteraction.startSideOffset + sideDelta, -20f, 20f);
            if (!Mathf.Approximately(newForward, vfx.forwardOffset) || !Mathf.Approximately(newSide, vfx.sideOffset))
            {
                vfx.forwardOffset = newForward;
                vfx.sideOffset = newSide;
                NotifyAbilityPreviewDirty();
            }
        }

        private void ApplyPreviewRotation(AbilityPreviewEvent evt, Vector2 currentMousePos)
        {
            AbilityVisualEffectConfig? vfx = evt.sourceVfx;
            if (vfx == null)
            {
                return;
            }

            Vector2 center = evt.lastTextureRect.center;
            float startAngle = Mathf.Atan2(abilityPreviewInteraction.dragStartMouse.y - center.y, abilityPreviewInteraction.dragStartMouse.x - center.x) * Mathf.Rad2Deg;
            float currentAngle = Mathf.Atan2(currentMousePos.y - center.y, currentMousePos.x - center.x) * Mathf.Rad2Deg;
            float deltaAngle = Mathf.DeltaAngle(startAngle, currentAngle);
            float nextRotation = Mathf.Clamp(abilityPreviewInteraction.startRotation + deltaAngle, PreviewRotationMin, PreviewRotationMax);
            if (!Mathf.Approximately(nextRotation, vfx.rotation))
            {
                vfx.rotation = nextRotation;
                NotifyAbilityPreviewDirty();
            }
        }

        private void ApplyPreviewScale(AbilityPreviewEvent evt, float wheelDelta)
        {
            AbilityVisualEffectConfig? vfx = evt.sourceVfx;
            if (vfx == null)
            {
                return;
            }

            float factor = Mathf.Clamp(1f - wheelDelta * PreviewScaleWheelStep, 0.6f, 1.6f);
            Vector2 scale = vfx.textureScale;
            scale.x = Mathf.Clamp(scale.x * factor, PreviewScaleMin, PreviewScaleMax);
            scale.y = Mathf.Clamp(scale.y * factor, PreviewScaleMin, PreviewScaleMax);
            if (!Mathf.Approximately(scale.x, vfx.textureScale.x) || !Mathf.Approximately(scale.y, vfx.textureScale.y))
            {
                vfx.textureScale = scale;
                NotifyAbilityPreviewDirty();
            }
        }

        private void HandleAbilityPreviewCanvasInteraction(Rect drawRect, AbilityPreviewPlan plan)
        {
            Event current = Event.current;
            if (current == null)
            {
                return;
            }

            int controlId = GUIUtility.GetControlID("CharacterStudio.AbilityPreviewCanvas".GetHashCode(), FocusType.Passive, drawRect);
            Vector2 mousePos = current.mousePosition;
            AbilityPreviewEvent? hoveredEvent = drawRect.Contains(mousePos) ? GetInteractivePreviewEventAt(plan, mousePos) : null;
            AbilityPreviewEvent? selectedEvent = GetSelectedPreviewEvent(plan) ?? hoveredEvent;

            if (current.type == EventType.MouseDown && drawRect.Contains(mousePos))
            {
                if (current.button == 0)
                {
                    if (hoveredEvent != null)
                    {
                        BeginPreviewInteraction(hoveredEvent, AbilityPreviewInteractionMode.Move, mousePos);
                        GUIUtility.hotControl = controlId;
                        current.Use();
                        return;
                    }

                    ClearPreviewEventSelection();
                }
                else if (current.button == 1 && hoveredEvent != null)
                {
                    BeginPreviewInteraction(hoveredEvent, AbilityPreviewInteractionMode.Rotate, mousePos);
                    GUIUtility.hotControl = controlId;
                    current.Use();
                    return;
                }
            }

            if (current.type == EventType.MouseDrag && GUIUtility.hotControl == controlId && selectedEvent != null)
            {
                if (abilityPreviewInteraction.mode == AbilityPreviewInteractionMode.Move)
                {
                    ApplyPreviewMove(drawRect, selectedEvent, mousePos);
                    current.Use();
                    return;
                }

                if (abilityPreviewInteraction.mode == AbilityPreviewInteractionMode.Rotate)
                {
                    ApplyPreviewRotation(selectedEvent, mousePos);
                    current.Use();
                    return;
                }
            }

            if (current.type == EventType.MouseUp && GUIUtility.hotControl == controlId)
            {
                GUIUtility.hotControl = 0;
                EndPreviewInteraction();
                current.Use();
                return;
            }

            if (current.type == EventType.ScrollWheel && drawRect.Contains(mousePos) && selectedEvent != null)
            {
                ApplyPreviewScale(selectedEvent, current.delta.y);
                SelectPreviewEvent(selectedEvent);
                current.Use();
            }
        }

        private float GetPreviewRadiusForVfx(AbilityVisualEffectConfig vfx, float defaultRadius)
        {
            if (vfx.UsesCustomTextureType)
            {
                return Mathf.Max(0.08f, vfx.drawSize * 0.12f);
            }
 
            return Mathf.Max(0.06f, defaultRadius * 0.15f, vfx.scale * 0.08f);
        }

        private string BuildEffectSummary(AbilityEffectConfig effect)
        {
            switch (effect.type)
            {
                case AbilityEffectType.Damage:
                    return "CS_Studio_Ability_PreviewEffectDamage".Translate(effect.amount.ToString("F0"), effect.damageDef?.label ?? "-" );
                case AbilityEffectType.Heal:
                    return "CS_Studio_Ability_PreviewEffectHeal".Translate(effect.amount.ToString("F0"));
                case AbilityEffectType.Buff:
                    return "CS_Studio_Ability_PreviewEffectBuff".Translate(effect.hediffDef?.label ?? "-");
                case AbilityEffectType.Debuff:
                    return "CS_Studio_Ability_PreviewEffectDebuff".Translate(effect.hediffDef?.label ?? "-");
                case AbilityEffectType.Summon:
                    return "CS_Studio_Ability_PreviewEffectSummon".Translate(effect.summonKind?.label ?? "-", effect.summonCount.ToString());
                case AbilityEffectType.Teleport:
                    return "CS_Studio_Ability_PreviewEffectTeleport".Translate();
                case AbilityEffectType.Control:
                    return "CS_Studio_Ability_PreviewEffectControl".Translate(GetControlModeLabel(effect.controlMode), effect.duration.ToString("F1"));
                case AbilityEffectType.Terraform:
                    return "CS_Studio_Ability_PreviewEffectTerraform".Translate(GetTerraformModeLabel(effect.terraformMode));
                default:
                    return effect.type.ToString();
            }
        }

        private string BuildVfxSummary(AbilityVisualEffectConfig vfx, int repeatIndex)
        {
            string body = "CS_Studio_Ability_PreviewVfxBody".Translate(
                GetVfxTypeLabel(vfx.type),
                GetVfxTargetLabel(vfx.target),
                GetVfxTriggerLabel(vfx.trigger));
            body += $" · {"CS_Studio_VFX_DisplayDurationShort".Translate()}:{vfx.displayDurationTicks}t";
            if (vfx.linkedExpression.HasValue)
            {
                body += $" · {"CS_Studio_VFX_LinkedExpressionShort".Translate()}:{vfx.linkedExpression.Value}";
            }
            if (Math.Abs(vfx.linkedPupilBrightnessOffset) > 0.001f)
            {
                body += $" · {"CS_Studio_VFX_PupilBrightnessShort".Translate()}:{vfx.linkedPupilBrightnessOffset:+0.##;-0.##;0}";
            }
            if (Math.Abs(vfx.linkedPupilContrastOffset) > 0.001f)
            {
                body += $" · {"CS_Studio_VFX_PupilContrastShort".Translate()}:{vfx.linkedPupilContrastOffset:+0.##;-0.##;0}";
            }
            if (repeatIndex <= 0)
                return body;

            return body + " · " + "CS_Studio_Ability_PreviewRepeatIndex".Translate((repeatIndex + 1).ToString());
        }

        private string BuildRuntimeComponentSummary(AbilityRuntimeComponentConfig component)
        {
            switch (component.type)
            {
                case AbilityRuntimeComponentType.QComboWindow:
                    return "CS_Studio_Ability_PreviewRuntimeQCombo".Translate(component.comboWindowTicks.ToString());
                case AbilityRuntimeComponentType.HotkeyOverride:
                    return "CS_Studio_Ability_PreviewRuntimeHotkeyOverride".Translate(
                        ($"CS_Studio_Ability_Hotkey_{component.overrideHotkeySlot}").Translate(),
                        string.IsNullOrWhiteSpace(component.overrideAbilityDefName) ? "-" : component.overrideAbilityDefName,
                        component.overrideDurationTicks.ToString());
                case AbilityRuntimeComponentType.FollowupCooldownGate:
                    return "CS_Studio_Ability_PreviewRuntimeFollowupCooldownGate".Translate(
                        ($"CS_Studio_Ability_Hotkey_{component.followupCooldownHotkeySlot}").Translate(),
                        component.followupCooldownTicks.ToString());
                case AbilityRuntimeComponentType.SmartJump:
                    return "CS_Studio_Ability_PreviewRuntimeSmartJump".Translate(
                        component.smartCastOffsetCells.ToString(),
                        component.jumpDistance.ToString(),
                        component.cooldownTicks.ToString());
                case AbilityRuntimeComponentType.EShortJump:
                    return "CS_Studio_Ability_PreviewRuntimeEShortJump".Translate(component.jumpDistance.ToString(), component.cooldownTicks.ToString());
                case AbilityRuntimeComponentType.RStackDetonation:
                    return "CS_Studio_Ability_PreviewRuntimeRStack".Translate(component.requiredStacks.ToString(), component.delayTicks.ToString());
                case AbilityRuntimeComponentType.PeriodicPulse:
                    return $"{GetRuntimeComponentTypeLabel(component.type)} · {component.pulseIntervalTicks}t / {component.pulseTotalTicks}t";
                case AbilityRuntimeComponentType.KillRefresh:
                    return $"{GetRuntimeComponentTypeLabel(component.type)} · {($"CS_Studio_Ability_Hotkey_{component.killRefreshHotkeySlot}").Translate()} · {(component.killRefreshCooldownPercent * 100f):0}%";
                case AbilityRuntimeComponentType.ShieldAbsorb:
                    return $"{GetRuntimeComponentTypeLabel(component.type)} · {component.shieldMaxDamage:0} / H{component.shieldHealRatio:0.##} / D{component.shieldBonusDamageRatio:0.##}";
                case AbilityRuntimeComponentType.ChainBounce:
                    return $"{GetRuntimeComponentTypeLabel(component.type)} · {component.maxBounceCount} / {component.bounceRange:0.#} / -{(component.bounceDamageFalloff * 100f):0}%";
                case AbilityRuntimeComponentType.ExecuteBonusDamage:
                    return "CS_Studio_Ability_PreviewRuntimeExecuteBonusDamage".Translate(component.executeThresholdPercent, component.executeBonusDamageScale);
                case AbilityRuntimeComponentType.MissingHealthBonusDamage:
                    return "CS_Studio_Ability_PreviewRuntimeMissingHealthBonusDamage".Translate(component.missingHealthBonusPerTenPercent, component.missingHealthBonusMaxScale);
                case AbilityRuntimeComponentType.FullHealthBonusDamage:
                    return "CS_Studio_Ability_PreviewRuntimeFullHealthBonusDamage".Translate(component.fullHealthThresholdPercent, component.fullHealthBonusDamageScale);
                case AbilityRuntimeComponentType.NearbyEnemyBonusDamage:
                    return "CS_Studio_Ability_PreviewRuntimeNearbyEnemyBonusDamage".Translate(
                        component.nearbyEnemyBonusMaxTargets.ToString(),
                        component.nearbyEnemyBonusPerTarget,
                        component.nearbyEnemyBonusRadius.ToString("0.#"));
                case AbilityRuntimeComponentType.IsolatedTargetBonusDamage:
                    return "CS_Studio_Ability_PreviewRuntimeIsolatedTargetBonusDamage".Translate(
                        component.isolatedTargetRadius.ToString("0.#"),
                        component.isolatedTargetBonusDamageScale);
                case AbilityRuntimeComponentType.MarkDetonation:
                    return "CS_Studio_Ability_PreviewRuntimeMarkDetonation".Translate(component.markMaxStacks.ToString(), component.markDurationTicks.ToString(), component.markDetonationDamage.ToString("0.#"));
                case AbilityRuntimeComponentType.ComboStacks:
                    return "CS_Studio_Ability_PreviewRuntimeComboStacks".Translate(component.comboStackMax.ToString(), component.comboStackBonusDamagePerStack);
                case AbilityRuntimeComponentType.HitSlowField:
                    return "CS_Studio_Ability_PreviewRuntimeHitSlowField".Translate(
                        component.slowFieldDurationTicks.ToString(),
                        component.slowFieldRadius.ToString("0.#"),
                        string.IsNullOrWhiteSpace(component.slowFieldHediffDefName) ? "-" : component.slowFieldHediffDefName);
                case AbilityRuntimeComponentType.PierceBonusDamage:
                    return "CS_Studio_Ability_PreviewRuntimePierceBonusDamage".Translate(
                        component.pierceMaxTargets.ToString(),
                        component.pierceBonusDamagePerTarget,
                        component.pierceSearchRange.ToString("0.#"));
                case AbilityRuntimeComponentType.DashEmpoweredStrike:
                    return "CS_Studio_Ability_PreviewRuntimeDashEmpoweredStrike".Translate(component.dashEmpowerDurationTicks.ToString(), component.dashEmpowerBonusDamageScale);
                case AbilityRuntimeComponentType.HitHeal:
                    return "CS_Studio_Ability_PreviewRuntimeHitHeal".Translate(component.hitHealAmount.ToString("0.#"), component.hitHealRatio);
                case AbilityRuntimeComponentType.HitCooldownRefund:
                    return "CS_Studio_Ability_PreviewRuntimeHitCooldownRefund".Translate(("CS_Studio_Ability_Hotkey_" + component.refundHotkeySlot).Translate(), component.hitCooldownRefundPercent);
                case AbilityRuntimeComponentType.ProjectileSplit:
                    return "CS_Studio_Ability_PreviewRuntimeProjectileSplit".Translate(component.splitProjectileCount.ToString(), component.splitDamageScale, component.splitSearchRange.ToString("0.#"));
                case AbilityRuntimeComponentType.FlightState:
                    return "CS_Studio_Ability_PreviewRuntimeFlightState".Translate(component.flightDurationTicks.ToString(), component.flightHeightFactor.ToString("0.##"));
                default:
                    return GetRuntimeComponentTypeLabel(component.type);
            }
        }

        private Color GetEffectPreviewColor(AbilityEffectType type)
        {
            switch (type)
            {
                case AbilityEffectType.Damage:
                    return new Color(1f, 0.38f, 0.38f);
                case AbilityEffectType.Heal:
                    return new Color(0.38f, 1f, 0.58f);
                case AbilityEffectType.Buff:
                    return new Color(0.42f, 0.86f, 1f);
                case AbilityEffectType.Debuff:
                    return new Color(0.88f, 0.46f, 1f);
                case AbilityEffectType.Summon:
                    return new Color(1f, 0.8f, 0.42f);
                case AbilityEffectType.Teleport:
                    return new Color(0.62f, 0.92f, 1f);
                case AbilityEffectType.Control:
                    return new Color(1f, 0.6f, 0.24f);
                case AbilityEffectType.Terraform:
                    return new Color(0.7f, 0.86f, 0.48f);
                default:
                    return Color.white;
            }
        }

        private Color GetVfxPreviewColor(AbilityVisualEffectConfig vfx)
        {
            if (vfx.UsesCustomTextureType)
                return new Color(1f, 0.72f, 0.44f);
            if (vfx.UsesPresetType)
                return new Color(0.7f, 0.78f, 1f);
 
            switch (vfx.type)
            {
                case AbilityVisualEffectType.DustPuff:
                    return new Color(0.76f, 0.76f, 0.76f);
                case AbilityVisualEffectType.MicroSparks:
                    return new Color(1f, 0.92f, 0.35f);
                case AbilityVisualEffectType.LightningGlow:
                    return new Color(0.62f, 0.82f, 1f);
                case AbilityVisualEffectType.FireGlow:
                    return new Color(1f, 0.58f, 0.28f);
                case AbilityVisualEffectType.Smoke:
                    return new Color(0.66f, 0.68f, 0.74f);
                case AbilityVisualEffectType.ExplosionEffect:
                    return new Color(1f, 0.48f, 0.34f);
                default:
                    return Color.white;
            }
        }

        private void DrawAbilityPreviewPanelBody(Rect rect)
        {
            AbilityPreviewPlan plan = BuildAbilityPreviewPlan();
            UpdateAbilityPreviewPlayback(plan);

            Rect toolbarRect = new Rect(rect.x, rect.y, rect.width, 24f);
            float buttonW = 74f;
            float smallW = 64f;
            float x = toolbarRect.x;

            DrawPanelButton(new Rect(x, toolbarRect.y, buttonW, toolbarRect.height), "CS_Studio_Ability_PreviewPlayOnce".Translate(), () =>
            {
                abilityPreviewPlaying = true;
                abilityPreviewLoop = false;
                abilityPreviewTimeTicks = 0f;
                abilityPreviewLastRealtime = Time.realtimeSinceStartup;
            }, true);
            x += buttonW + 6f;

            DrawPanelButton(new Rect(x, toolbarRect.y, buttonW, toolbarRect.height), abilityPreviewLoop ? "CS_Studio_Ability_PreviewLoopOn".Translate() : "CS_Studio_Ability_PreviewLoop".Translate(), () =>
            {
                abilityPreviewLoop = !abilityPreviewLoop;
                abilityPreviewPlaying = abilityPreviewLoop || abilityPreviewPlaying;
                abilityPreviewLastRealtime = Time.realtimeSinceStartup;
            }, abilityPreviewLoop);
            x += buttonW + 6f;

            DrawPanelButton(new Rect(x, toolbarRect.y, smallW, toolbarRect.height), "CS_Studio_Btn_Reset".Translate(), () => ResetAbilityPreviewPlayback(false));
            x += smallW + 10f;

            DrawPanelButton(new Rect(x, toolbarRect.y, 56f, toolbarRect.height), "CS_Studio_Ability_PreviewGrid".Translate(), () => abilityPreviewContext.showGrid = !abilityPreviewContext.showGrid, abilityPreviewContext.showGrid);
            x += 62f;

            DrawPanelButton(new Rect(x, toolbarRect.y, 72f, toolbarRect.height), "CS_Studio_Ability_PreviewTimeline".Translate(), () => abilityPreviewContext.showTimeline = !abilityPreviewContext.showTimeline, abilityPreviewContext.showTimeline);

            Rect speedRect = new Rect(rect.xMax - 68f, toolbarRect.y, 68f, toolbarRect.height);
            DrawPanelButton(speedRect, $"{abilityPreviewContext.playbackSpeed:0.0}x", () =>
            {
                abilityPreviewContext.playbackSpeed = Mathf.Approximately(abilityPreviewContext.playbackSpeed, 1f)
                    ? 2f
                    : Mathf.Approximately(abilityPreviewContext.playbackSpeed, 2f)
                        ? 0.5f
                        : 1f;
            });

            Rect bannerRect = new Rect(rect.x, toolbarRect.yMax + 6f, rect.width, 26f);
            DrawAbilityInfoBanner(bannerRect, "CS_Studio_Ability_PreviewHintLine".Translate(plan.title, Mathf.RoundToInt(abilityPreviewTimeTicks).ToString(), plan.totalTicks.ToString()), true);

            float canvasY = bannerRect.yMax + 6f;
            float footerHeight = 34f;
            float canvasHeight = Mathf.Max(150f, rect.height * 0.44f);
            Rect canvasRect = new Rect(rect.x, canvasY, rect.width, canvasHeight);
            Rect canvasDrawRect = new Rect(canvasRect.x, canvasRect.y, canvasRect.width, Mathf.Max(96f, canvasRect.height - footerHeight - 4f));
            Rect canvasInfoRect = new Rect(canvasRect.x, canvasDrawRect.yMax + 4f, canvasRect.width, footerHeight);
            DrawAbilityPreviewCanvas(canvasRect, canvasDrawRect, canvasInfoRect, plan);

            float nextY = canvasRect.yMax + 8f;
            if (abilityPreviewContext.showTimeline)
            {
                Rect timelineRect = new Rect(rect.x, nextY, rect.width, 58f);
                DrawAbilityPreviewTimeline(timelineRect, plan);
                nextY = timelineRect.yMax + 8f;
            }

            float lowerHeight = rect.yMax - nextY;
            float leftW = rect.width * 0.48f;
            Rect summaryRect = new Rect(rect.x, nextY, leftW, lowerHeight);
            Rect logRect = new Rect(summaryRect.xMax + 8f, nextY, rect.width - leftW - 8f, lowerHeight);
            DrawAbilityPreviewSummary(summaryRect, plan);
            DrawAbilityPreviewLog(logRect, plan);
        }

        private void UpdateAbilityPreviewPlayback(AbilityPreviewPlan plan)
        {
            float now = Time.realtimeSinceStartup;
            if (abilityPreviewLastRealtime < 0f)
            {
                abilityPreviewLastRealtime = now;
            }

            if (!abilityPreviewPlaying)
            {
                abilityPreviewLastRealtime = now;
                return;
            }

            float delta = Mathf.Max(0f, now - abilityPreviewLastRealtime);
            abilityPreviewLastRealtime = now;
            abilityPreviewTimeTicks += delta * 60f * Mathf.Max(0.1f, abilityPreviewContext.playbackSpeed);

            if (abilityPreviewTimeTicks >= plan.totalTicks)
            {
                if (abilityPreviewLoop)
                {
                    abilityPreviewTimeTicks = 0f;
                }
                else
                {
                    abilityPreviewTimeTicks = plan.totalTicks;
                    abilityPreviewPlaying = false;
                }
            }
        }

        private void DrawAbilityPreviewCanvas(Rect rect, Rect canvasDrawRect, Rect canvasInfoRect, AbilityPreviewPlan plan)
        {
            Widgets.DrawBoxSolid(rect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), UIHelper.AccentSoftColor);

            Rect headerRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 34f);
            Widgets.DrawBoxSolid(headerRect, new Color(1f, 1f, 1f, 0.03f));
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            bool oldWrap = Text.WordWrap;
            Text.WordWrap = true;
            Rect headerLabelRect = new Rect(headerRect.x + 8f, headerRect.y + 4f, headerRect.width - 16f, headerRect.height - 8f);
            Widgets.Label(headerLabelRect, plan.summary);
            TooltipHandler.TipRegion(headerLabelRect, plan.summary);
            Text.WordWrap = oldWrap;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Rect drawRect = new Rect(canvasDrawRect.x + 8f, headerRect.yMax + 6f, canvasDrawRect.width - 16f, Mathf.Max(80f, canvasDrawRect.yMax - headerRect.yMax - 12f));
            Widgets.DrawBoxSolid(drawRect, UIHelper.PanelFillColor);
            GUI.color = new Color(1f, 1f, 1f, 0.08f);
            Widgets.DrawBox(drawRect, 1);
            GUI.color = Color.white;

            Rect gridRect = GetPreviewGridRect(drawRect);
            float cellSize = GetPreviewCellSize(drawRect);
            Vector2 cellStep = GetPreviewCellStep(drawRect);
            Widgets.DrawBoxSolid(gridRect, new Color(1f, 1f, 1f, 0.02f));
            GUI.color = new Color(1f, 1f, 1f, 0.10f);
            Widgets.DrawBox(gridRect, 1);
            GUI.color = Color.white;

            if (abilityPreviewContext.showGrid)
            {
                for (int i = 1; i < PreviewGridCellsPerAxis; i++)
                {
                    float tx = gridRect.x + cellStep.x * i;
                    Widgets.DrawLineVertical(tx, gridRect.y, gridRect.height);
                    float ty = gridRect.y + cellStep.y * i;
                    Widgets.DrawLineHorizontal(gridRect.x, ty, gridRect.width);
                }
            }

            Vector2 caster = ToCanvasPosition(gridRect, abilityPreviewContext.casterPos);
            Vector2 target = ToCanvasPosition(gridRect, abilityPreviewContext.targetPos);
            Widgets.DrawLine(caster, target, new Color(0.45f, 0.75f, 1f), 2f);

            float currentTick = abilityPreviewTimeTicks;
            foreach (AbilityPreviewEvent evt in plan.events)
            {
                evt.lastTextureRect = Rect.zero;
                evt.lastTextureRotation = 0f;
                if (evt.tick > currentTick)
                    continue;

                Vector2 pos = ResolvePreviewEventCanvasPosition(drawRect, evt);
                if (evt.radius > 0f)
                {
                    DrawPreviewAreaOverlay(gridRect, evt, pos, cellSize, evt.color, evt == GetCurrentPreviewEvent(plan, currentTick));
                }
            }

            DrawPreviewAnchor(caster, 8f, new Color(0.42f, 0.88f, 1f), "CS_Studio_Ability_PreviewCaster".Translate());
            DrawPreviewAnchor(target, 8f, new Color(1f, 0.72f, 0.36f), "CS_Studio_Ability_PreviewTarget".Translate());

            foreach (AbilityPreviewEvent evt in plan.events)
            {
                if (evt.tick > currentTick)
                    continue;

                Vector2 pos = ResolvePreviewEventCanvasPosition(drawRect, evt);
                float pulse = 1f + 0.18f * Mathf.Sin((currentTick - evt.tick) * 0.35f);
                float pulseRadius = Mathf.Max(6f, cellSize * 0.4f) * pulse;
                DrawPreviewPulse(pos, pulseRadius, evt.color);
                DrawPreviewEventTexture(drawRect, evt, pos, pulse);
            }

            HandleAbilityPreviewCanvasInteraction(drawRect, plan);

            AbilityPreviewEvent? activeEvent = GetCurrentPreviewEvent(plan, currentTick);
            AbilityPreviewEvent? selectedEvent = GetSelectedPreviewEvent(plan);
            Widgets.DrawBoxSolid(canvasInfoRect, new Color(0f, 0f, 0f, 0.18f));
            GUI.color = (selectedEvent?.color ?? activeEvent?.color) ?? UIHelper.BorderColor;
            Widgets.DrawBox(canvasInfoRect, 1);
            GUI.color = Color.white;
            Text.Font = GameFont.Tiny;
            oldWrap = Text.WordWrap;
            Text.WordWrap = false;
            Rect infoLabelRect = new Rect(canvasInfoRect.x + 8f, canvasInfoRect.y + 6f, canvasInfoRect.width - 16f, canvasInfoRect.height - 12f);
            string activeText = activeEvent == null
                ? "等待播放事件…"
                : selectedEvent != null
                    ? $"[{selectedEvent.tick}] {selectedEvent.label} · 拖动移动 / 滚轮缩放 / 右键拖动旋转"
                    : $"[{activeEvent.tick}] {activeEvent.label} · {activeEvent.detail}";
            Widgets.Label(infoLabelRect, activeText);
            TooltipHandler.TipRegion(infoLabelRect, activeText);
            Text.WordWrap = oldWrap;
            Text.Font = GameFont.Small;
        }

        private void DrawAbilityPreviewTimeline(Rect rect, AbilityPreviewPlan plan)
        {
            Widgets.DrawBoxSolid(rect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            Rect lineRect = new Rect(rect.x + 14f, rect.center.y, rect.width - 28f, 2f);
            Widgets.DrawBoxSolid(lineRect, new Color(1f, 1f, 1f, 0.16f));
            float progress = plan.totalTicks <= 0 ? 0f : Mathf.Clamp01(abilityPreviewTimeTicks / plan.totalTicks);
            Widgets.DrawBoxSolid(new Rect(lineRect.x, lineRect.y - 1f, lineRect.width * progress, 4f), UIHelper.AccentColor);

            foreach (AbilityPreviewEvent evt in plan.events)
            {
                float t = plan.totalTicks <= 0 ? 0f : Mathf.Clamp01((float)evt.tick / plan.totalTicks);
                float px = lineRect.x + lineRect.width * t;
                Rect marker = new Rect(px - 3f, lineRect.y - 7f, 6f, 16f);
                Widgets.DrawBoxSolid(marker, evt.color);
            }

            Rect labelRect = new Rect(rect.x + 8f, rect.y + 4f, rect.width - 16f, 18f);
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(labelRect, "CS_Studio_Ability_PreviewTimelineHint".Translate(Mathf.RoundToInt(abilityPreviewTimeTicks).ToString(), plan.totalTicks.ToString()));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawAbilityPreviewSummary(Rect rect, AbilityPreviewPlan plan)
        {
            Widgets.DrawBoxSolid(rect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), UIHelper.AccentSoftColor);

            Rect titleRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 24f);
            Widgets.DrawBoxSolid(titleRect, new Color(1f, 1f, 1f, 0.03f));
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(new Rect(titleRect.x + 8f, titleRect.y + 4f, titleRect.width - 16f, 18f), "CS_Studio_Ability_PreviewPayload".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Rect listRect = new Rect(rect.x + 6f, titleRect.yMax + 6f, rect.width - 12f, rect.height - 36f);
            List<string> lines = new List<string>();
            if (plan.summaries.Count > 0)
            {
                lines.AddRange(plan.summaries);
            }
            else
            {
                lines.Add("CS_Studio_Ability_PreviewNoEffects".Translate());
            }

            if (plan.notes.Count > 0)
            {
                lines.AddRange(plan.notes.Select(note => "• " + note));
            }

            float contentHeight = 0f;
            Text.Font = GameFont.Tiny;
            bool oldWrap = Text.WordWrap;
            Text.WordWrap = true;
            float rowWidth = Mathf.Max(60f, listRect.width - 24f);
            foreach (string line in lines)
            {
                contentHeight += Mathf.Max(28f, Text.CalcHeight(line, rowWidth) + 10f);
            }

            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(listRect.height, contentHeight + 4f));
            Widgets.BeginScrollView(listRect, ref abilityPreviewSummaryScrollPos, viewRect);
            float y = 0f;
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                float rowHeight = Mathf.Max(28f, Text.CalcHeight(line, viewRect.width - 16f) + 10f);
                Rect rowRect = new Rect(0f, y, viewRect.width, rowHeight - 4f);
                Widgets.DrawBoxSolid(rowRect, i % 2 == 0 ? new Color(1f, 1f, 1f, 0.03f) : new Color(1f, 1f, 1f, 0.06f));
                Widgets.Label(new Rect(rowRect.x + 8f, rowRect.y + 4f, rowRect.width - 16f, rowRect.height - 8f), line);
                TooltipHandler.TipRegion(rowRect, line);
                y += rowHeight;
            }
            Widgets.EndScrollView();
            Text.WordWrap = oldWrap;
            Text.Font = GameFont.Small;
        }

        private void DrawAbilityPreviewLog(Rect rect, AbilityPreviewPlan plan)
        {
            Widgets.DrawBoxSolid(rect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), UIHelper.AccentSoftColor);

            Rect titleRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 24f);
            Widgets.DrawBoxSolid(titleRect, new Color(1f, 1f, 1f, 0.03f));
            Text.Font = GameFont.Tiny;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(new Rect(titleRect.x + 8f, titleRect.y + 4f, titleRect.width - 16f, 18f), "CS_Studio_Ability_PreviewLog".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Rect listRect = new Rect(rect.x + 6f, titleRect.yMax + 6f, rect.width - 12f, rect.height - 36f);
            Text.Font = GameFont.Tiny;
            bool oldWrap = Text.WordWrap;
            Text.WordWrap = true;
            float contentHeight = 0f;
            float rowWidth = Mathf.Max(60f, listRect.width - 28f);
            foreach (AbilityPreviewEvent evt in plan.events)
            {
                string line = $"[{evt.tick}] {evt.label} · {evt.detail}";
                contentHeight += Mathf.Max(28f, Text.CalcHeight(line, rowWidth) + 10f);
            }

            Rect viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(listRect.height, contentHeight + 4f));
            Widgets.BeginScrollView(listRect, ref abilityPreviewLogScrollPos, viewRect);
            float y = 0f;
            for (int i = 0; i < plan.events.Count; i++)
            {
                AbilityPreviewEvent evt = plan.events[i];
                string line = $"[{evt.tick}] {evt.label} · {evt.detail}";
                float rowHeight = Mathf.Max(28f, Text.CalcHeight(line, viewRect.width - 20f) + 10f);
                Rect row = new Rect(0f, y, viewRect.width, rowHeight - 4f);
                Widgets.DrawBoxSolid(row, i % 2 == 0 ? new Color(1f, 1f, 1f, 0.02f) : new Color(1f, 1f, 1f, 0.05f));
                Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), evt.color);
                GUI.color = evt.tick <= abilityPreviewTimeTicks ? Color.white : UIHelper.SubtleColor;
                Widgets.Label(new Rect(row.x + 8f, row.y + 4f, row.width - 12f, row.height - 8f), line);
                TooltipHandler.TipRegion(row, line);
                GUI.color = Color.white;
                y += rowHeight;
            }
            Widgets.EndScrollView();
            Text.WordWrap = oldWrap;
            Text.Font = GameFont.Small;
        }

        private AbilityPreviewEvent? GetCurrentPreviewEvent(AbilityPreviewPlan plan, float tick)
        {
            AbilityPreviewEvent? current = null;
            foreach (AbilityPreviewEvent evt in plan.events)
            {
                if (evt.tick > tick)
                    break;
                current = evt;
            }

            return current;
        }

        private Vector2 ToCanvasPosition(Rect rect, Vector2 normalized)
        {
            return new Vector2(rect.x + rect.width * normalized.x, rect.y + rect.height * normalized.y);
        }

        private void DrawPreviewAreaOverlay(Rect gridRect, AbilityPreviewEvent evt, Vector2 resolvedPos, float cellSize, Color color, bool highlight)
        {
            Vector2 center = evt.areaCenteredOnCaster
                ? ToCanvasPosition(gridRect, abilityPreviewContext.casterPos)
                : resolvedPos;
            AbilityAreaShape shape = evt.areaShape;
            Vector2 cellStep = GetPreviewCellStep(gridRect);
            if (shape == AbilityAreaShape.Circle)
            {
                DrawPreviewRadiusOverlay(center, evt.radius * Mathf.Min(cellStep.x, cellStep.y), color, highlight);
                return;
            }

            foreach (Vector2 offset in EnumeratePreviewAreaOffsets(shape, evt.radius, evt.irregularPattern))
            {
                Vector2 cellCenter = center + new Vector2(offset.x * cellStep.x, offset.y * cellStep.y);
                DrawPreviewAreaCell(cellCenter, Mathf.Min(cellStep.x, cellStep.y), color, highlight);
            }
        }

        private IEnumerable<Vector2> EnumeratePreviewAreaOffsets(AbilityAreaShape shape, float radius, string irregularPattern)
        {
            int discreteRadius = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(0f, radius)));
            switch (shape)
            {
                case AbilityAreaShape.Line:
                    for (int step = 0; step <= discreteRadius; step++)
                    {
                        yield return new Vector2(0f, -step);
                    }
                    break;
                case AbilityAreaShape.Cone:
                    for (int forward = 0; forward <= discreteRadius; forward++)
                    {
                        for (int side = -forward; side <= forward; side++)
                        {
                            yield return new Vector2(side, -forward);
                        }
                    }
                    break;
                case AbilityAreaShape.Cross:
                    yield return Vector2.zero;
                    for (int step = 1; step <= discreteRadius; step++)
                    {
                        yield return new Vector2(0f, -step);
                        yield return new Vector2(0f, step);
                        yield return new Vector2(step, 0f);
                        yield return new Vector2(-step, 0f);
                    }
                    break;
                case AbilityAreaShape.Square:
                    for (int forward = -discreteRadius; forward <= discreteRadius; forward++)
                    {
                        for (int side = -discreteRadius; side <= discreteRadius; side++)
                        {
                            yield return new Vector2(side, -forward);
                        }
                    }
                    break;
                case AbilityAreaShape.Irregular:
                    foreach (Vector2 offset in EnumeratePreviewIrregularOffsets(irregularPattern))
                    {
                        yield return offset;
                    }
                    break;
                default:
                    yield return Vector2.zero;
                    break;
            }
        }

        private IEnumerable<Vector2> EnumeratePreviewIrregularOffsets(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                yield return Vector2.zero;
                yield break;
            }

            string normalized = pattern.Replace("\r", string.Empty).Replace("/", "\n");
            string[] rows = normalized
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(row => row.Trim())
                .Where(row => row.Length > 0)
                .ToArray();
            if (rows.Length == 0)
            {
                yield return Vector2.zero;
                yield break;
            }

            int rowCenter = rows.Length / 2;
            int maxWidth = rows.Max(row => row.Length);
            int colCenter = maxWidth / 2;

            for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                string row = rows[rowIndex];
                for (int colIndex = 0; colIndex < row.Length; colIndex++)
                {
                    char token = row[colIndex];
                    if (token != '1' && token != 'x' && token != 'X' && token != '#')
                    {
                        continue;
                    }

                    int forwardOffset = rowCenter - rowIndex;
                    int sideOffset = colIndex - colCenter;
                    yield return new Vector2(sideOffset, -forwardOffset);
                }
            }
        }

        private void DrawPreviewAreaCell(Vector2 center, float cellSize, Color color, bool highlight)
        {
            float size = Mathf.Max(6f, cellSize - 4f);
            Rect cellRect = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
            Widgets.DrawBoxSolid(cellRect, new Color(color.r, color.g, color.b, highlight ? 0.20f : 0.11f));
            GUI.color = new Color(color.r, color.g, color.b, highlight ? 0.96f : 0.72f);
            Widgets.DrawBox(cellRect, highlight ? 2 : 1);
            GUI.color = Color.white;
        }

        private void DrawPreviewRadiusOverlay(Vector2 center, float radius, Color color, bool highlight)
        {
            float diameter = Mathf.Max(2f, radius * 2f);
            Rect outerRect = new Rect(center.x - radius, center.y - radius, diameter, diameter);
            Rect innerRect = outerRect.ContractedBy(Mathf.Max(1f, radius * 0.5f));
            Color fillColor = new Color(color.r, color.g, color.b, highlight ? 0.16f : 0.10f);
            Color ringColor = new Color(color.r, color.g, color.b, highlight ? 0.92f : 0.72f);

            Widgets.DrawBoxSolid(outerRect, fillColor);
            GUI.color = ringColor;
            Widgets.DrawBox(outerRect, highlight ? 2 : 1);
            GUI.color = new Color(color.r, color.g, color.b, highlight ? 0.55f : 0.35f);
            Widgets.DrawBox(innerRect, 1);
            GUI.color = Color.white;
        }

        private void DrawPreviewAnchor(Vector2 center, float size, Color color, string label)
        {
            Rect pointRect = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
            Widgets.DrawBoxSolid(pointRect, color);
            Widgets.DrawBox(pointRect, 1);
            Text.Font = GameFont.Tiny;
            GUI.color = color;
            Widgets.Label(new Rect(center.x + 6f, center.y - 8f, 100f, 18f), label);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        private void DrawPreviewEventTexture(Rect drawRect, AbilityPreviewEvent evt, Vector2 center, float pulse)
        {
            if (string.IsNullOrWhiteSpace(evt.previewTexturePath))
            {
                return;
            }

            Texture2D? texture = RuntimeAssetLoader.LoadTextureRaw(evt.previewTexturePath, true);
            if (texture == null)
            {
                return;
            }

            Vector2 textureScale = evt.sourceVfx?.textureScale ?? evt.previewTextureScale;
            float drawSize = evt.sourceVfx?.drawSize ?? evt.previewDrawSize;
            float rotation = evt.sourceVfx?.rotation ?? evt.previewRotation;
            float aspect = texture.height > 0 ? texture.width / (float)texture.height : 1f;
            float textureScaleX = Mathf.Max(0.25f, textureScale.x);
            float textureScaleY = Mathf.Max(0.25f, textureScale.y);
            float baseSize = Mathf.Clamp(26f * Mathf.Max(0.25f, drawSize) * pulse, 24f, Mathf.Min(drawRect.width, drawRect.height) * 0.28f);
            float width = Mathf.Clamp(baseSize * textureScaleX * Mathf.Max(0.35f, aspect), 24f, drawRect.width * 0.34f);
            float height = Mathf.Clamp(baseSize * textureScaleY / Mathf.Max(0.35f, aspect), 24f, drawRect.height * 0.34f);
            Rect texRect = new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
            float maxOverflowX = Mathf.Max(6f, width * 0.18f);
            float maxOverflowY = Mathf.Max(6f, height * 0.18f);
            texRect.x = Mathf.Clamp(texRect.x, drawRect.x - maxOverflowX, drawRect.xMax - texRect.width + maxOverflowX);
            texRect.y = Mathf.Clamp(texRect.y, drawRect.y - maxOverflowY, drawRect.yMax - texRect.height + maxOverflowY);
            evt.lastTextureRect = texRect;
            evt.lastTextureRotation = rotation;

            Widgets.DrawBoxSolid(texRect.ExpandedBy(1f), new Color(0f, 0f, 0f, 0.18f));
            Matrix4x4 oldMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(rotation, texRect.center);
            GUI.color = new Color(1f, 1f, 1f, 0.96f);
            Widgets.DrawTextureFitted(texRect, texture, 1f);
            GUI.color = IsPreviewEventSelected(evt) ? UIHelper.AccentColor : evt.color;
            Widgets.DrawBox(texRect, IsPreviewEventSelected(evt) ? 2 : 1);
            GUI.matrix = oldMatrix;
            GUI.color = Color.white;

            if (IsPreviewEventSelected(evt))
            {
                Rect handleRect = new Rect(texRect.xMax - 8f, texRect.y - 8f, 16f, 16f);
                Widgets.DrawBoxSolid(handleRect, UIHelper.AccentColor);
                Widgets.DrawBox(handleRect, 1);
            }

            string tip = $"{evt.previewTexturePath}\n缩放: {(evt.sourceVfx?.textureScale ?? evt.previewTextureScale).x:F2}, {(evt.sourceVfx?.textureScale ?? evt.previewTextureScale).y:F2}\n旋转: {(evt.sourceVfx?.rotation ?? evt.previewRotation):F1}°";
            TooltipHandler.TipRegion(texRect.ExpandedBy(4f), tip);
        }

        private static bool PointInRotatedRect(Vector2 point, Rect rect, float angleDegrees)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return false;
            }

            Vector2 center = rect.center;
            float radians = -angleDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            Vector2 delta = point - center;
            float localX = delta.x * cos - delta.y * sin;
            float localY = delta.x * sin + delta.y * cos;
            return Mathf.Abs(localX) <= rect.width * 0.5f && Mathf.Abs(localY) <= rect.height * 0.5f;
        }

        private void DrawPreviewPulse(Vector2 center, float radius, Color color)
        {
            Rect rect = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
            Widgets.DrawBoxSolid(rect, new Color(color.r, color.g, color.b, 0.08f));
            GUI.color = new Color(color.r, color.g, color.b, 0.9f);
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
        }
    }
}