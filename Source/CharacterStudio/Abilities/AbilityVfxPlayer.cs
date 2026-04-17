// ─────────────────────────────────────────────
// 技能视觉特效播放器
// 从 CompAbilityEffect_Modular.cs 提取
//
// 负责：VFX 触发/排队/播放、位置解析、朝向解析、Mote 生成
// ─────────────────────────────────────────────

using System;
using System.Collections.Generic;
using CharacterStudio.Core;
using CharacterStudio.Rendering;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 技能视觉特效播放器。
    /// 封装 VFX 播放、位置/朝向解析、自定义贴图 Mote 生成等逻辑，
    /// 供 CompAbilityEffect_Modular 和其他触发点复用。
    /// </summary>
    internal static class AbilityVfxPlayer
    {
        private static readonly Dictionary<string, ThingDef> _customTextureMoteDefCache = new Dictionary<string, ThingDef>();
        private static readonly HashSet<string> _runtimeVfxWarnings = new HashSet<string>();

        // ─────────────────────────────────────────────
        // 公开 API
        // ─────────────────────────────────────────────

        /// <summary>
        /// 清理静态缓存，由 GameComponent 在地图切换等生命周期点调用。
        /// </summary>
        public static void ClearStaticCaches()
        {
            _customTextureMoteDefCache.Clear();
            _runtimeVfxWarnings.Clear();
        }

        /// <summary>
        /// 播放单个 VFX 配置对应的视觉特效。
        /// </summary>
        public static void PlayVfx(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride = null)
        {
            if (caster == null || caster.Map == null) return;

            try
            {
                vfx.NormalizeLegacyData();
                vfx.SyncLegacyFields();

                CompPawnSkin? skinComp = caster.GetComp<CompPawnSkin>();
                if (skinComp != null
                    && (vfx.linkedExpression.HasValue
                        || Math.Abs(vfx.linkedPupilBrightnessOffset) > 0.001f
                        || Math.Abs(vfx.linkedPupilContrastOffset) > 0.001f))
                {
                    skinComp.ApplyAbilityFaceOverride(
                        vfx.linkedExpression,
                        vfx.linkedExpressionDurationTicks,
                        vfx.linkedPupilBrightnessOffset,
                        vfx.linkedPupilContrastOffset);
                }

                if (vfx.UsesCustomTextureType)
                {
                    if (TryPlayCustomTextureVfx(vfx, target, caster, sourceOverride))
                    {
                        return;
                    }

                    Log.Warning($"[CharacterStudio] 自定义贴图特效缺少有效贴图路径，已跳过播放。");
                    return;
                }

                if ((vfx.type == AbilityVisualEffectType.LineTexture || vfx.type == AbilityVisualEffectType.WallTexture)
                    && TryPlaySpatialTextureVfx(vfx, target, caster, sourceOverride))
                {
                    return;
                }

                if (!TryResolveRuntimeVfxType(vfx, out AbilityVisualEffectType runtimeVfxType))
                {
                    return;
                }

                VisualEffectWorker worker = VisualEffectWorkerFactory.GetWorker(runtimeVfxType);
                worker.Play(vfx, CreateRuntimeVfxTarget(vfx, target, caster, sourceOverride), caster);
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] VFX 播放异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 播放 VFX 关联的声音。
        /// </summary>
        public static void PlayVfxSound(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride = null)
        {
            if (caster?.Map == null || string.IsNullOrWhiteSpace(vfx.soundDefName))
            {
                return;
            }

            try
            {
                SoundDef? soundDef = DefDatabase<SoundDef>.GetNamedSilentFail(vfx.soundDefName);
                if (soundDef == null)
                {
                    Log.Warning($"[CharacterStudio] 未找到 VFX 声音 Def: {vfx.soundDefName}");
                    return;
                }

                foreach (Vector3 soundPos in ResolveVfxPositions(vfx, target, caster, sourceOverride))
                {
                    SoundInfo soundInfo = SoundInfo.InMap(new TargetInfo(soundPos.ToIntVec3(), caster.Map, false), MaintenanceType.None);
                    soundInfo.volumeFactor = Mathf.Max(0f, vfx.soundVolume);
                    soundInfo.pitchFactor = Mathf.Max(0.01f, vfx.soundPitch);
                    soundDef.PlayOneShot(soundInfo);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[CharacterStudio] VFX 声音播放异常: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────
        // VFX 类型解析
        // ─────────────────────────────────────────────

        private static void LogRuntimeVfxWarningOnce(string key, string message)
        {
            if (_runtimeVfxWarnings.Add(key))
            {
                Log.Warning(message);
            }
        }

        private static bool TryResolveRuntimeVfxType(AbilityVisualEffectConfig vfx, out AbilityVisualEffectType resolvedType)
        {
            resolvedType = vfx.type;
            if (!vfx.UsesPresetType)
            {
                return true;
            }

            if (VisualEffectWorkerFactory.TryResolvePresetType(vfx.presetDefName, out resolvedType))
            {
                return true;
            }

            string presetName = string.IsNullOrWhiteSpace(vfx.presetDefName)
                ? "<empty>"
                : vfx.presetDefName.Trim();
            LogRuntimeVfxWarningOnce(
                $"PresetMissing:{presetName}",
                $"[CharacterStudio] 视觉特效预设 '{presetName}' 未注册到运行时 Worker，已跳过播放。");
            return false;
        }

        // ─────────────────────────────────────────────
        // 自定义贴图 VFX 播放
        // ─────────────────────────────────────────────

        private static bool TryPlayCustomTextureVfx(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride = null)
        {
            if (string.IsNullOrWhiteSpace(vfx.customTexturePath) || caster.Map == null)
            {
                return false;
            }

            bool useFrameAnim = vfx.UsesFrameAnimation;
            Type moteType = useFrameAnim ? typeof(Mote_VfxFrameAnimation) : typeof(MoteThrown);

            ThingDef moteDef = GetOrCreateCustomTextureMoteDef(
                vfx.customTexturePath,
                Mathf.Max(0.1f, vfx.drawSize),
                Mathf.Max(1, vfx.displayDurationTicks),
                moteType);

            float uniformScale = Mathf.Max(0.1f, vfx.scale);
            float scaleX = Mathf.Max(0.1f, vfx.textureScale.x) * uniformScale;
            float scaleZ = Mathf.Max(0.1f, vfx.textureScale.y) * uniformScale;
            bool playedAny = false;

            foreach (Vector3 spawnPos in ResolveVfxPositions(vfx, target, caster, sourceOverride))
            {
                MoteThrown? mote = ThingMaker.MakeThing(moteDef) as MoteThrown;
                if (mote == null)
                {
                    continue;
                }

                mote.exactPosition = spawnPos;
                mote.exactRotation = ResolveVfxRotation(vfx, target, caster, sourceOverride);
                mote.rotationRate = 0f;
                mote.instanceColor = Color.white;
                mote.linearScale = new Vector3(scaleX, 1f, scaleZ);
                mote.SetVelocity(0f, 0f);
                GenSpawn.Spawn(mote, spawnPos.ToIntVec3(), caster.Map, WipeMode.Vanish);

                if (useFrameAnim && mote is Mote_VfxFrameAnimation frameMote)
                {
                    frameMote.InitFrames(
                        vfx.customTexturePath,
                        vfx.frameCount,
                        vfx.frameIntervalTicks,
                        vfx.frameLoop);
                }

                playedAny = true;
            }

            return playedAny;
        }

        // ─────────────────────────────────────────────
        // 空间（线/墙）VFX 播放
        // ─────────────────────────────────────────────

        private static bool TryPlaySpatialTextureVfx(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride = null)
        {
            if (string.IsNullOrWhiteSpace(vfx.customTexturePath) || caster.Map == null)
            {
                return false;
            }

            if (!TryResolveSpatialAnchors(vfx, target, caster, sourceOverride, out Vector3 start, out Vector3 end))
            {
                return false;
            }

            Vector3 delta = end - start;
            delta.y = 0f;
            float length = delta.magnitude;
            if (length < 0.05f)
            {
                end = start + caster.Rotation.FacingCell.ToVector3() * 0.2f;
                delta = end - start;
                delta.y = 0f;
                length = Mathf.Max(0.2f, delta.magnitude);
            }

            ThingDef moteDef = GetOrCreateCustomTextureMoteDef(
                vfx.customTexturePath,
                Mathf.Max(0.1f, vfx.drawSize),
                Mathf.Max(1, vfx.displayDurationTicks));

            int requestedSegments = Mathf.Max(1, vfx.segmentCount);
            int segmentCount = vfx.tileByLength
                ? Mathf.Max(requestedSegments, Mathf.CeilToInt(length / Mathf.Max(0.2f, vfx.drawSize)))
                : requestedSegments;
            float rotation = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg + vfx.rotation;
            Vector3 forward = delta.normalized;
            if (forward == Vector3.zero)
            {
                forward = caster.Rotation.FacingCell.ToVector3();
            }

            bool playedAny = false;
            for (int i = 0; i < segmentCount; i++)
            {
                if (vfx.revealBySegments && i > 0)
                {
                    // v1 runtime keeps immediate full spawn; editor/preview expose the field already.
                }

                float t = segmentCount == 1 ? 0.5f : (i + 0.5f) / segmentCount;
                Vector3 spawnPos = Vector3.Lerp(start, end, t);
                if (vfx.followGround)
                {
                    spawnPos = spawnPos.ToIntVec3().ToVector3Shifted();
                }

                MoteThrown? mote = ThingMaker.MakeThing(moteDef) as MoteThrown;
                if (mote == null)
                {
                    continue;
                }

                float uniformScale = Mathf.Max(0.1f, vfx.scale);
                float segmentLength = length / segmentCount;
                float scaleX;
                float scaleZ;
                if (vfx.type == AbilityVisualEffectType.WallTexture)
                {
                    scaleX = Mathf.Max(0.1f, vfx.wallThickness) * uniformScale;
                    scaleZ = Mathf.Max(0.1f, vfx.wallHeight) * uniformScale;
                    spawnPos.y += Mathf.Max(0f, vfx.wallHeight) * 0.5f;
                }
                else
                {
                    scaleX = Mathf.Max(0.1f, vfx.lineWidth) * uniformScale;
                    scaleZ = Mathf.Max(0.1f, segmentLength) * Mathf.Max(0.1f, vfx.textureScale.y) * uniformScale;
                }

                scaleX *= Mathf.Max(0.1f, vfx.textureScale.x);

                mote.exactPosition = spawnPos;
                mote.exactRotation = rotation;
                mote.rotationRate = 0f;
                mote.instanceColor = Color.white;
                mote.linearScale = new Vector3(scaleX, 1f, scaleZ);
                mote.SetVelocity(0f, 0f);
                GenSpawn.Spawn(mote, spawnPos.ToIntVec3(), caster.Map, WipeMode.Vanish);
                playedAny = true;
            }

            return playedAny;
        }

        // ─────────────────────────────────────────────
        // 位置 / 朝向解析
        // ─────────────────────────────────────────────

        private static bool TryResolveSpatialAnchors(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride, out Vector3 start, out Vector3 end)
        {
            start = ResolveSpatialAnchor(vfx.anchorMode, target, caster, sourceOverride);
            end = ResolveSpatialAnchor(vfx.secondaryAnchorMode, target, caster, sourceOverride);

            if (vfx.pathMode == AbilityVisualPathMode.DirectLineCasterToTarget)
            {
                start = ResolveSpatialAnchor(AbilityVisualAnchorMode.Caster, target, caster, sourceOverride);
                end = ResolveSpatialAnchor(AbilityVisualAnchorMode.Target, target, caster, sourceOverride);
            }

            if ((end - start).sqrMagnitude < 0.0001f)
            {
                end = ResolveSpatialAnchor(AbilityVisualAnchorMode.Target, target, caster, sourceOverride);
            }

            return true;
        }

        private static Vector3 ResolveSpatialAnchor(AbilityVisualAnchorMode anchorMode, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride = null)
        {
            switch (anchorMode)
            {
                case AbilityVisualAnchorMode.Caster:
                    return sourceOverride ?? caster.DrawPos;
                case AbilityVisualAnchorMode.TargetCell:
                    return target.IsValid ? target.Cell.ToVector3Shifted() : caster.Position.ToVector3Shifted();
                case AbilityVisualAnchorMode.AreaCenter:
                    return target.IsValid ? target.Cell.ToVector3Shifted() : caster.DrawPos;
                case AbilityVisualAnchorMode.Target:
                default:
                    if (target.HasThing)
                    {
                        return target.Thing.DrawPos;
                    }

                    return target.IsValid ? target.Cell.ToVector3Shifted() : caster.DrawPos;
            }
        }

        internal static IEnumerable<Vector3> ResolveVfxPositions(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride = null)
        {
            if (vfx.target == VisualEffectTarget.Both)
            {
                Vector3 casterPos = ResolveVfxPosition(vfx, target, caster, VisualEffectTarget.Caster, sourceOverride);
                yield return casterPos;

                Vector3 targetPos = ResolveVfxPosition(vfx, target, caster, VisualEffectTarget.Target, sourceOverride);
                if ((targetPos - casterPos).sqrMagnitude > 0.0001f)
                {
                    yield return targetPos;
                }

                yield break;
            }

            yield return ResolveVfxPosition(vfx, target, caster, vfx.target, sourceOverride);
        }

        internal static Vector3 ResolveVfxPosition(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride = null)
        {
            VisualEffectTarget resolvedTarget = vfx.target == VisualEffectTarget.Both
                ? VisualEffectTarget.Caster
                : vfx.target;
            return ResolveVfxPosition(vfx, target, caster, resolvedTarget, sourceOverride);
        }

        internal static Vector3 ResolveVfxPosition(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, VisualEffectTarget targetMode, Vector3? sourceOverride = null)
        {
            Vector3 pos;
            switch (targetMode)
            {
                case VisualEffectTarget.Caster:
                    pos = sourceOverride ?? caster.DrawPos;
                    break;
                case VisualEffectTarget.Target:
                    if (target.HasThing)
                    {
                        pos = target.Thing.DrawPos;
                    }
                    else if (target.IsValid)
                    {
                        pos = target.Cell.ToVector3Shifted();
                    }
                    else
                    {
                        pos = caster.DrawPos;
                    }
                    break;
                case VisualEffectTarget.Both:
                default:
                    pos = caster.DrawPos;
                    break;
            }

            pos += vfx.offset;
            pos.y += vfx.heightOffset;

            if (!TryResolveFacingBasis(vfx, target, caster, sourceOverride, out Vector3 forward, out Vector3 right))
            {
                return pos;
            }

            return pos + forward * vfx.forwardOffset + right * vfx.sideOffset;
        }

        internal static float ResolveVfxRotation(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride = null)
        {
            return vfx.rotation + ResolveAutoFacingAngle(vfx, target, caster, sourceOverride);
        }

        internal static float ResolveAutoFacingAngle(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride = null)
        {
            if (!TryResolveFacingBasis(vfx, target, caster, sourceOverride, out Vector3 forward, out _))
            {
                return 0f;
            }

            // 使用连续角度（Mathf.Atan2）替代4方向量化，确保特效旋转精确对准施法方向
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        internal static bool TryResolveFacingBasis(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride, out Vector3 forward, out Vector3 right)
        {
            AbilityVisualFacingMode facingMode = ResolveFacingMode(vfx);
            if (facingMode == AbilityVisualFacingMode.None)
            {
                forward = Vector3.zero;
                right = Vector3.zero;
                return false;
            }

            if (facingMode == AbilityVisualFacingMode.CastDirection)
            {
                // 使用连续方向向量替代4方向量化，确保特效精确对准施法方向
                forward = ResolveCastDirectionVector(target, caster, sourceOverride);
            }
            else
            {
                forward = caster.Rotation.FacingCell.ToVector3();
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = caster.Rotation.FacingCell.ToVector3();
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = new Vector3(0f, 0f, 1f);
            }

            forward = forward.normalized;
            right = new Vector3(forward.z, 0f, -forward.x);
            return true;
        }

        /// <summary>
        /// 计算从施法者到目标的连续方向向量（不量化为4方向），
        /// 用于特效的精确朝向和旋转。
        /// </summary>
        internal static Vector3 ResolveCastDirectionVector(LocalTargetInfo target, Pawn caster, Vector3? sourceOverride = null)
        {
            Vector3 origin = sourceOverride ?? caster.DrawPos;
            Vector3 destination;
            if (target.HasThing)
            {
                destination = target.Thing.DrawPos;
            }
            else if (target.IsValid)
            {
                destination = target.Cell.ToVector3Shifted();
            }
            else
            {
                return caster.Rotation.FacingCell.ToVector3();
            }

            Vector3 delta = destination - origin;
            delta.y = 0f;

            if (delta.sqrMagnitude < 0.0001f)
            {
                return caster.Rotation.FacingCell.ToVector3();
            }

            return delta.normalized;
        }

        internal static AbilityVisualFacingMode ResolveFacingMode(AbilityVisualEffectConfig vfx)
        {
            AbilityVisualFacingMode facingMode = vfx.facingMode;
            if (!Enum.IsDefined(typeof(AbilityVisualFacingMode), facingMode))
            {
                return vfx.useCasterFacing ? AbilityVisualFacingMode.CasterFacing : AbilityVisualFacingMode.None;
            }

            if (facingMode == AbilityVisualFacingMode.None && vfx.useCasterFacing)
            {
                return AbilityVisualFacingMode.CasterFacing;
            }

            return facingMode;
        }

        private static LocalTargetInfo CreateRuntimeVfxTarget(AbilityVisualEffectConfig vfx, LocalTargetInfo target, Pawn caster, Vector3? sourceOverride)
        {
            if (!sourceOverride.HasValue || vfx.target != VisualEffectTarget.Caster)
            {
                return target;
            }

            return new LocalTargetInfo(sourceOverride.Value.ToIntVec3());
        }

        // ─────────────────────────────────────────────
        // Mote Def 缓存
        // ─────────────────────────────────────────────

        /// <summary>
        /// 获取或创建自定义贴图 Mote ThingDef。
        /// 
        /// 注意：此方法在运行时动态创建 ThingDef 但不注册到 DefDatabase。
        /// 这意味着某些依赖 DefDatabase 查找的系统可能无法找到这些 Def。
        /// 当前使用场景（MoteThrown 渲染）不需要 DefDatabase 注册，因此可以安全使用。
        /// 如果未来需要更广泛的 Def 查找，应改为在 Defs XML 中预定义一批 Mote Def。
        /// </summary>
        internal static ThingDef GetOrCreateCustomTextureMoteDef(string texturePath, float drawSize, int displayDurationTicks, Type? moteThingClass = null)
        {
            Type resolvedClass = moteThingClass ?? typeof(MoteThrown);
            string key = $"{texturePath}|{drawSize:F3}|{displayDurationTicks}|{resolvedClass.Name}";
            if (_customTextureMoteDefCache.TryGetValue(key, out ThingDef cachedDef))
            {
                return cachedDef;
            }

            // 缓存大小上限保护，防止异常情况下无限增长
            if (_customTextureMoteDefCache.Count > 500)
            {
                Log.Warning("[CharacterStudio] 自定义贴图 Mote 缓存已超过 500 条，执行清理。这通常意味着贴图路径组合过多。");
                _customTextureMoteDefCache.Clear();
            }

            var def = new ThingDef
            {
                defName = $"CS_RuntimeCustomVfx_{_customTextureMoteDefCache.Count}",
                label = "runtime custom vfx mote",
                thingClass = resolvedClass,
                category = ThingCategory.Mote,
                altitudeLayer = AltitudeLayer.MoteOverhead,
                drawerType = DrawerType.RealtimeOnly,
                useHitPoints = false,
                drawGUIOverlay = false,
                tickerType = TickerType.Normal,
                mote = new MoteProperties
                {
                    realTime = true,
                    fadeInTime = 0f,
                    solidTime = Mathf.Max(1, displayDurationTicks) / 60f,
                    fadeOutTime = 0.2f,
                    needsMaintenance = false,
                    collide = false,
                    speedPerTime = 0f,
                    growthRate = 0f
                },
                graphicData = new GraphicData
                {
                    texPath = texturePath,
                    graphicClass = (texturePath.Contains(":") || texturePath.Contains("/") || texturePath.Contains("\\"))
                        ? typeof(Graphic_Runtime)
                        : typeof(Graphic_Single),
                    shaderType = ShaderTypeDefOf.Transparent,
                    drawSize = new Vector2(drawSize, drawSize),
                    color = Color.white,
                    colorTwo = Color.white
                }
            };

            _customTextureMoteDefCache[key] = def;
            return def;
        }
    }
}
