// ─────────────────────────────────────────────
// 视觉特效配置（VFX 类型、位置、朝向、Shader 等）
// 从 ModularAbilityDef.cs 提取
// ─────────────────────────────────────────────

using System;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    public class AbilityVisualEffectConfig
    {
        public AbilityVisualEffectType type = AbilityVisualEffectType.DustPuff;
        public AbilityVisualSpatialMode spatialMode = AbilityVisualSpatialMode.Point;
        public AbilityVisualAnchorMode anchorMode = AbilityVisualAnchorMode.Target;
        public AbilityVisualAnchorMode secondaryAnchorMode = AbilityVisualAnchorMode.Target;
        public AbilityVisualPathMode pathMode = AbilityVisualPathMode.None;
        public AbilityVisualEffectTextureSource textureSource = AbilityVisualEffectTextureSource.Vanilla;
        public AbilityVisualEffectSourceMode sourceMode = AbilityVisualEffectSourceMode.BuiltIn;
        public string presetDefName = string.Empty;
        public string customTexturePath = string.Empty;
        public VisualEffectTarget target = VisualEffectTarget.Target;
        public AbilityVisualEffectTrigger trigger = AbilityVisualEffectTrigger.OnTargetApply;
        public int delayTicks = 0;
        public int displayDurationTicks = 27;
        public ExpressionType? linkedExpression = null;
        public int linkedExpressionDurationTicks = 30;
        public float linkedPupilBrightnessOffset = 0f;
        public float linkedPupilContrastOffset = 0f;
        public float scale = 1.0f;
        public float drawSize = 1.5f;
        public AbilityVisualFacingMode facingMode = AbilityVisualFacingMode.None;
        public bool useCasterFacing = false;
        public float forwardOffset = 0f;
        public float sideOffset = 0f;
        public float heightOffset = 0f;
        public float rotation = 0f;
        public Vector2 textureScale = Vector2.one;
        public float lineWidth = 0.35f;
        public float wallHeight = 2.5f;
        public float wallThickness = 0.2f;
        public bool tileByLength = true;
        public bool followGround = false;
        public int segmentCount = 1;
        public bool revealBySegments = false;
        public int segmentRevealIntervalTicks = 3;
        public int repeatCount = 1;
        public int repeatIntervalTicks = 0;
        public Vector3 offset = Vector3.zero;
        public bool playSound = false;
        public string soundDefName = string.Empty;
        public int soundDelayTicks = 0;
        public float soundVolume = 1f;
        public float soundPitch = 1f;
        public bool attachToPawn = false;
        public bool attachToTargetCell = false;
        public bool enabled = true;

        /// <summary>
        /// VFX 起点来源图层名。填写后，该 VFX 的起点位置将从指定图层的实际渲染位置（包含动画偏移）获取，
        /// 而非默认的施法者位置。用于实现浮游炮等从动态图层位置发射弹道的效果。
        /// 留空则使用默认行为（从施法者中心发射）。
        /// </summary>
        public string vfxSourceLayerName = string.Empty;

        // Frame animation fields
        public bool enableFrameAnimation = false;
        public int frameCount = 2;
        public int frameIntervalTicks = 3;
        public bool frameLoop = true;

        // AssetBundle / VFX fields
        public string assetBundlePath = string.Empty;
        public string assetBundleEffectName = string.Empty;
        public string assetBundleTextureName = string.Empty;
        public float assetBundleEffectScale = 1.0f;
        public VfxBundleRenderStrategy bundleRenderStrategy = VfxBundleRenderStrategy.Default;

        // Shader fields
        public string shaderPath = string.Empty;
        public string shaderAssetBundlePath = string.Empty;
        public string shaderAssetBundleShaderName = string.Empty;
        public bool shaderLoadFromAssetBundle = false;
        public string shaderTexturePath = string.Empty;
        public Color shaderTintColor = Color.white;
        public float shaderIntensity = 1f;
        public float shaderSpeed = 1f;
        public float shaderParam1 = 0f;
        public float shaderParam2 = 0f;
        public float shaderParam3 = 0f;
        public float shaderParam4 = 0f;

        // Global filter
        public string globalFilterMode = string.Empty;
        public string globalFilterTransition = string.Empty;
        public int globalFilterTransitionTicks = 0;

        public bool UsesBuiltInType => type != AbilityVisualEffectType.Preset && type != AbilityVisualEffectType.CustomTexture && type != AbilityVisualEffectType.GlobalFilter;
        public bool UsesPresetType => type == AbilityVisualEffectType.Preset;
        public bool UsesCustomTextureType => type == AbilityVisualEffectType.CustomTexture;
        public bool IsGlobalFilterType => type == AbilityVisualEffectType.GlobalFilter;
        public bool UsesSpatialLine => type == AbilityVisualEffectType.LineTexture || spatialMode == AbilityVisualSpatialMode.Line;
        public bool UsesSpatialWall => type == AbilityVisualEffectType.WallTexture || spatialMode == AbilityVisualSpatialMode.Wall;
        public bool RequiresTexturePath => UsesCustomTextureType || type == AbilityVisualEffectType.LineTexture || type == AbilityVisualEffectType.WallTexture;
        public bool UsesFrameAnimation => enableFrameAnimation && frameCount > 1 && UsesCustomTextureType;

        public void NormalizeLegacyData()
        {
            if (!System.Enum.IsDefined(typeof(AbilityVisualSpatialMode), spatialMode))
            {
                spatialMode = type == AbilityVisualEffectType.WallTexture
                    ? AbilityVisualSpatialMode.Wall
                    : type == AbilityVisualEffectType.LineTexture
                        ? AbilityVisualSpatialMode.Line
                        : AbilityVisualSpatialMode.Point;
            }

            if (!System.Enum.IsDefined(typeof(AbilityVisualAnchorMode), anchorMode))
            {
                anchorMode = AbilityVisualAnchorMode.Target;
            }

            if (!System.Enum.IsDefined(typeof(AbilityVisualAnchorMode), secondaryAnchorMode))
            {
                secondaryAnchorMode = AbilityVisualAnchorMode.Target;
            }

            if (!System.Enum.IsDefined(typeof(AbilityVisualPathMode), pathMode))
            {
                pathMode = AbilityVisualPathMode.None;
            }

            if (!System.Enum.IsDefined(typeof(AbilityVisualFacingMode), facingMode))
            {
                facingMode = useCasterFacing ? AbilityVisualFacingMode.CasterFacing : AbilityVisualFacingMode.None;
            }

            if (facingMode == AbilityVisualFacingMode.None && useCasterFacing)
            {
                facingMode = AbilityVisualFacingMode.CasterFacing;
            }

            useCasterFacing = facingMode == AbilityVisualFacingMode.CasterFacing;

            if (type == AbilityVisualEffectType.LineTexture)
            {
                spatialMode = AbilityVisualSpatialMode.Line;
                if (pathMode == AbilityVisualPathMode.None)
                {
                    pathMode = AbilityVisualPathMode.DirectLineCasterToTarget;
                }
                if (anchorMode == AbilityVisualAnchorMode.Target)
                {
                    anchorMode = AbilityVisualAnchorMode.Caster;
                }
                if (secondaryAnchorMode == AbilityVisualAnchorMode.Caster)
                {
                    secondaryAnchorMode = AbilityVisualAnchorMode.Target;
                }
            }
            else if (type == AbilityVisualEffectType.WallTexture)
            {
                spatialMode = AbilityVisualSpatialMode.Wall;
                if (pathMode == AbilityVisualPathMode.None)
                {
                    pathMode = AbilityVisualPathMode.DirectLineCasterToTarget;
                }
                if (anchorMode == AbilityVisualAnchorMode.Target)
                {
                    anchorMode = AbilityVisualAnchorMode.Caster;
                }
                if (secondaryAnchorMode == AbilityVisualAnchorMode.Caster)
                {
                    secondaryAnchorMode = AbilityVisualAnchorMode.Target;
                }
            }
            else if (spatialMode != AbilityVisualSpatialMode.Point)
            {
                spatialMode = AbilityVisualSpatialMode.Point;
            }

            if (type == AbilityVisualEffectType.Preset || type == AbilityVisualEffectType.CustomTexture)
            {
                if (type == AbilityVisualEffectType.CustomTexture && string.IsNullOrWhiteSpace(customTexturePath))
                {
                    textureSource = AbilityVisualEffectTextureSource.LocalPath;
                }
                return;
            }

            switch (sourceMode)
            {
                case AbilityVisualEffectSourceMode.Preset:
                    type = AbilityVisualEffectType.Preset;
                    break;
                case AbilityVisualEffectSourceMode.CustomTexture:
                    type = AbilityVisualEffectType.CustomTexture;
                    if (string.IsNullOrWhiteSpace(customTexturePath))
                    {
                        textureSource = AbilityVisualEffectTextureSource.LocalPath;
                    }
                    break;
                default:
                    sourceMode = AbilityVisualEffectSourceMode.BuiltIn;
                    break;
            }
        }

        public void SyncLegacyFields()
        {
            sourceMode = type switch
            {
                AbilityVisualEffectType.Preset => AbilityVisualEffectSourceMode.Preset,
                AbilityVisualEffectType.CustomTexture => AbilityVisualEffectSourceMode.CustomTexture,
                _ => AbilityVisualEffectSourceMode.BuiltIn
            };

            useCasterFacing = facingMode == AbilityVisualFacingMode.CasterFacing;
        }

        public AbilityVisualEffectConfig Clone()
        {
            AbilityVisualEffectConfig clone = (AbilityVisualEffectConfig)MemberwiseClone();
            clone.SyncLegacyFields();
            return clone;
        }

        public void NormalizeForSave()
        {
            presetDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(presetDefName);
            customTexturePath = AbilityEditorNormalizationUtility.TrimOrEmpty(customTexturePath);
            soundDefName = AbilityEditorNormalizationUtility.TrimOrEmpty(soundDefName);
            NormalizeLegacyData();
            if (!Enum.IsDefined(typeof(AbilityVisualEffectTrigger), trigger))
            {
                trigger = AbilityVisualEffectTrigger.OnTargetApply;
            }
            delayTicks = AbilityEditorNormalizationUtility.ClampInt(delayTicks, 0, 60000);
            displayDurationTicks = AbilityEditorNormalizationUtility.ClampInt(displayDurationTicks, 1, 60000);
            linkedExpressionDurationTicks = AbilityEditorNormalizationUtility.ClampInt(linkedExpressionDurationTicks, 1, 60000);
            scale = AbilityEditorNormalizationUtility.ClampFloat(scale, 0.1f, 5f);
            drawSize = AbilityEditorNormalizationUtility.ClampFloat(drawSize, 0.1f, 20f);
            heightOffset = AbilityEditorNormalizationUtility.ClampFloat(heightOffset, -10f, 10f);
            rotation = AbilityEditorNormalizationUtility.ClampFloat(rotation, -360f, 360f);
            textureScale = new Vector2(
                AbilityEditorNormalizationUtility.ClampFloat(textureScale.x, 0.1f, 20f),
                AbilityEditorNormalizationUtility.ClampFloat(textureScale.y, 0.1f, 20f));
            lineWidth = AbilityEditorNormalizationUtility.ClampFloat(lineWidth, 0.05f, 20f);
            wallHeight = AbilityEditorNormalizationUtility.ClampFloat(wallHeight, 0.05f, 30f);
            wallThickness = AbilityEditorNormalizationUtility.ClampFloat(wallThickness, 0.05f, 20f);
            segmentCount = AbilityEditorNormalizationUtility.ClampInt(segmentCount, 1, 512);
            segmentRevealIntervalTicks = AbilityEditorNormalizationUtility.ClampInt(segmentRevealIntervalTicks, 0, 60000);
            repeatCount = AbilityEditorNormalizationUtility.ClampInt(repeatCount, 1, 999);
            repeatIntervalTicks = AbilityEditorNormalizationUtility.ClampInt(repeatIntervalTicks, 0, 60000);
            linkedPupilBrightnessOffset = AbilityEditorNormalizationUtility.ClampFloat(linkedPupilBrightnessOffset, -2f, 2f);
            linkedPupilContrastOffset = AbilityEditorNormalizationUtility.ClampFloat(linkedPupilContrastOffset, -2f, 2f);
            soundDelayTicks = AbilityEditorNormalizationUtility.ClampInt(soundDelayTicks, 0, 60000);
            soundVolume = AbilityEditorNormalizationUtility.ClampFloat(soundVolume, 0f, 4f);
            soundPitch = AbilityEditorNormalizationUtility.ClampFloat(soundPitch, 0.25f, 3f);
            vfxSourceLayerName = AbilityEditorNormalizationUtility.TrimOrEmpty(vfxSourceLayerName);
            SyncLegacyFields();
            frameCount = AbilityEditorNormalizationUtility.ClampInt(frameCount, 2, 120);
            frameIntervalTicks = AbilityEditorNormalizationUtility.ClampInt(frameIntervalTicks, 1, 60000);
        }

        public AbilityValidationResult Validate()
        {
            var result = new AbilityValidationResult();
            if (!enabled)
            {
                return result;
            }

            if (RequiresTexturePath && string.IsNullOrWhiteSpace(customTexturePath))
            {
                result.AddError("CS_Ability_Validate_VfxTexturePathRequired".Translate());
            }

            if (type == AbilityVisualEffectType.LineTexture && lineWidth <= 0f)
            {
                result.AddError("CS_Ability_Validate_VfxLineWidthPositive".Translate());
            }

            if (type == AbilityVisualEffectType.WallTexture)
            {
                if (wallHeight <= 0f)
                {
                    result.AddError("CS_Ability_Validate_VfxWallHeightPositive".Translate());
                }

                if (wallThickness <= 0f)
                {
                    result.AddError("CS_Ability_Validate_VfxWallThicknessPositive".Translate());
                }
            }

            if (segmentCount <= 0)
            {
                result.AddError("CS_Ability_Validate_VfxSegmentCountPositive".Translate());
            }

            if (segmentRevealIntervalTicks < 0)
            {
                result.AddError("CS_Ability_Validate_VfxSegmentRevealNonNegative".Translate());
            }

            if (repeatCount <= 0)
            {
                result.AddError("CS_Ability_Validate_VfxRepeatCountPositive".Translate());
            }

            if (displayDurationTicks <= 0)
            {
                result.AddError("CS_Ability_Validate_VfxDurationPositive".Translate());
            }

            if (enableFrameAnimation && frameCount < 2)
            {
                result.AddError("CS_Ability_Validate_VfxFrameCountMin".Translate());
            }

            return result;
        }
    }
}
