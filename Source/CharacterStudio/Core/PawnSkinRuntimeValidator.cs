using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using CharacterStudio.Abilities;
using CharacterStudio.AI;
using CharacterStudio.Attributes;
using CharacterStudio.Rendering;

namespace CharacterStudio.Core
{
    /// <summary>
    /// PawnSkin 运行时校验与规范化工具。
    /// 目标不是做严格编辑器验证，而是在皮肤进入运行时渲染链之前，
    /// 尽可能补齐空对象、过滤明显非法数据并回退到安全默认值，
    /// 避免“编辑期可保存 / 可编译，但运行时状态不完整”导致的异常。
    /// </summary>
    public static class PawnSkinRuntimeValidator
    {
        private static readonly object missingTextureWarningLock = new object();
        private static readonly HashSet<string> missingTextureWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public static PawnSkinDef PrepareForRuntime(PawnSkinDef? skin)
        {
            if (skin == null)
            {
                return new PawnSkinDef();
            }

            var prepared = skin.Clone();

            prepared.hiddenPaths ??= new List<string>();
#pragma warning disable CS0618
            prepared.hiddenTags ??= new List<string>();
#pragma warning restore CS0618
            prepared.targetRaces ??= new List<string>();
            prepared.abilities ??= new List<ModularAbilityDef>();
            prepared.abilityHotkeys ??= new SkinAbilityHotkeyConfig();
            prepared.attributes ??= new CharacterAttributeProfile();
            prepared.statModifiers ??= new CharacterStatModifierProfile();
            prepared.animationConfig ??= new PawnAnimationConfig();
            prepared.faceConfig ??= new PawnFaceConfig();
            prepared.baseAppearance ??= new BaseAppearanceConfig();
            prepared.baseAppearance.EnsureAllSlotsExist();

            if (!IsFinite(prepared.globalTextureScale) || prepared.globalTextureScale <= 0f)
            {
                prepared.globalTextureScale = prepared.baseAppearance.drawSizeScale > 0f
                    ? prepared.baseAppearance.drawSizeScale
                    : (prepared.baseAppearance.globalScale > 0f ? prepared.baseAppearance.globalScale : 1f);
            }

            prepared.hiddenPaths.RemoveAll(string.IsNullOrWhiteSpace);
#pragma warning disable CS0618
            prepared.hiddenTags.RemoveAll(string.IsNullOrWhiteSpace);
#pragma warning restore CS0618
            prepared.targetRaces.RemoveAll(string.IsNullOrWhiteSpace);
            prepared.abilities.RemoveAll(a => a == null);

            NormalizeBaseAppearance(prepared.baseAppearance);
            NormalizeLayers(prepared.layers);

            if (string.IsNullOrWhiteSpace(prepared.label))
            {
                prepared.label = prepared.defName;
            }

            if (prepared.version.NullOrEmpty())
            {
                prepared.version = "1.0.0";
            }

            prepared.previewTexPath = prepared.previewTexPath?.Trim() ?? string.Empty;
            WarnIfMissingExternalTexturePath(prepared.previewTexPath);
            prepared.xenotypeDefName = prepared.xenotypeDefName?.Trim() ?? string.Empty;
            prepared.raceDisplayName = prepared.raceDisplayName?.Trim() ?? string.Empty;
            prepared.author = prepared.author?.Trim() ?? string.Empty;

            return prepared;
        }

        public static List<string> CollectMissingExternalTexturePaths(PawnSkinDef? skin)
        {
            var missingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (skin == null)
            {
                return new List<string>();
            }

            AddMissingExternalPath(skin.previewTexPath, missingPaths);

            if (skin.baseAppearance?.slots != null)
            {
                foreach (var slot in skin.baseAppearance.slots)
                {
                    if (slot == null)
                    {
                        continue;
                    }

                    AddMissingExternalPath(slot.texPath, missingPaths);
                    AddMissingExternalPath(slot.maskTexPath, missingPaths);
                }
            }

            if (skin.layers != null)
            {
                foreach (var layer in skin.layers)
                {
                    if (layer == null)
                    {
                        continue;
                    }

                    AddMissingExternalPath(layer.texPath, missingPaths);
                    AddMissingExternalPath(layer.maskTexPath, missingPaths);
                }
            }

            var orderedPaths = new List<string>(missingPaths);
            orderedPaths.Sort(StringComparer.OrdinalIgnoreCase);
            return orderedPaths;
        }

        private static void NormalizeLayers(List<PawnLayerConfig>? layers)
        {
            if (layers == null)
            {
                return;
            }

            for (int i = layers.Count - 1; i >= 0; i--)
            {
                var layer = layers[i];
                if (layer == null)
                {
                    layers.RemoveAt(i);
                    continue;
                }

                layer.layerName = layer.layerName?.Trim() ?? string.Empty;
                layer.texPath = layer.texPath?.Trim() ?? string.Empty;
                layer.anchorTag = string.IsNullOrWhiteSpace(layer.anchorTag) ? "Head" : layer.anchorTag.Trim();
                layer.anchorPath = layer.anchorPath?.Trim() ?? string.Empty;
                layer.maskTexPath = layer.maskTexPath?.Trim() ?? string.Empty;
                layer.shaderDefName = string.IsNullOrWhiteSpace(layer.shaderDefName) ? "Cutout" : layer.shaderDefName.Trim();

                WarnIfMissingExternalTexturePath(layer.maskTexPath);
                if (WarnIfMissingExternalTexturePath(layer.texPath))
                {
                    layer.visible = false;
                }

                if (!IsFinite(layer.offset))
                {
                    layer.offset = Vector3.zero;
                }

                if (!IsFinite(layer.offsetEast))
                {
                    layer.offsetEast = Vector3.zero;
                }

                if (!IsFinite(layer.offsetNorth))
                {
                    layer.offsetNorth = Vector3.zero;
                }

                if (!IsFinite(layer.drawOrder))
                {
                    layer.drawOrder = 0f;
                }

                if (!IsFinite(layer.scale) || layer.scale.x <= 0f || layer.scale.y <= 0f)
                {
                    layer.scale = Vector2.one;
                }

                if (!IsFinite(layer.rotation))
                {
                    layer.rotation = 0f;
                }

                if (!IsFinite(layer.animFrequency) || layer.animFrequency <= 0f)
                {
                    layer.animFrequency = 1f;
                }

                if (!IsFinite(layer.animAmplitude))
                {
                    layer.animAmplitude = 15f;
                }

                if (!IsFinite(layer.animSpeed) || layer.animSpeed <= 0f)
                {
                    layer.animSpeed = 1f;
                }

                if (!IsFinite(layer.animOffsetAmplitude))
                {
                    layer.animOffsetAmplitude = 0.02f;
                }

                if (!IsFinite(layer.animPhaseOffset))
                {
                    layer.animPhaseOffset = 0f;
                }

                if (!Enum.IsDefined(typeof(LayerAnimationType), layer.animationType))
                {
                    layer.animationType = LayerAnimationType.None;
                }

                if (!Enum.IsDefined(typeof(LayerColorSource), layer.colorSource))
                {
                    layer.colorSource = LayerColorSource.PawnHair;
                }

                if (!Enum.IsDefined(typeof(LayerColorSource), layer.colorTwoSource))
                {
                    layer.colorTwoSource = LayerColorSource.PawnHair;
                }

                if (string.IsNullOrWhiteSpace(layer.texPath))
                {
                    layer.visible = false;
                }
            }
        }

        private static void NormalizeBaseAppearance(BaseAppearanceConfig? baseAppearance)
        {
            if (baseAppearance == null)
            {
                return;
            }

            baseAppearance.EnsureAllSlotsExist();

            if (!IsFinite(baseAppearance.globalScale) || baseAppearance.globalScale <= 0f)
            {
                baseAppearance.globalScale = 1f;
            }

            if (!IsFinite(baseAppearance.drawSizeScale) || baseAppearance.drawSizeScale <= 0f)
            {
                baseAppearance.drawSizeScale = baseAppearance.globalScale > 0f ? baseAppearance.globalScale : 1f;
            }

            baseAppearance.globalScale = baseAppearance.drawSizeScale;

            foreach (var slot in baseAppearance.slots)
            {
                if (slot == null)
                {
                    continue;
                }

                slot.texPath = slot.texPath?.Trim() ?? string.Empty;
                slot.maskTexPath = slot.maskTexPath?.Trim() ?? string.Empty;
                slot.shaderDefName = string.IsNullOrWhiteSpace(slot.shaderDefName) ? "Cutout" : slot.shaderDefName.Trim();

                WarnIfMissingExternalTexturePath(slot.maskTexPath);
                if (WarnIfMissingExternalTexturePath(slot.texPath))
                {
                    slot.enabled = false;
                }

                if (!IsFinite(slot.offset))
                {
                    slot.offset = Vector3.zero;
                }

                if (!IsFinite(slot.offsetEast))
                {
                    slot.offsetEast = Vector3.zero;
                }

                if (!IsFinite(slot.offsetNorth))
                {
                    slot.offsetNorth = Vector3.zero;
                }

                if (!IsFinite(slot.scale) || slot.scale.x <= 0f || slot.scale.y <= 0f)
                {
                    slot.scale = Vector2.one;
                }

                if (!IsFinite(slot.rotation))
                {
                    slot.rotation = 0f;
                }

                if (!IsFinite(slot.drawOrderOffset))
                {
                    slot.drawOrderOffset = 0f;
                }

                if (!Enum.IsDefined(typeof(LayerColorSource), slot.colorSource))
                {
                    slot.colorSource = LayerColorSource.Fixed;
                }

                if (!Enum.IsDefined(typeof(LayerColorSource), slot.colorTwoSource))
                {
                    slot.colorTwoSource = LayerColorSource.Fixed;
                }

                if (string.IsNullOrWhiteSpace(slot.texPath))
                {
                    slot.enabled = false;
                }
            }
        }

        private static void AddMissingExternalPath(string? texturePath, HashSet<string> missingPaths)
        {
            if (TryGetMissingExternalTexturePath(texturePath, out string missingPath))
            {
                missingPaths.Add(missingPath);
            }
        }

        private static bool TryGetMissingExternalTexturePath(string? texturePath, out string missingPath)
        {
            missingPath = string.Empty;
            string trimmedPath = texturePath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedPath))
            {
                return false;
            }

            if (!RuntimeAssetLoader.LooksLikeExternalTexturePath(trimmedPath))
            {
                return false;
            }

            if (RuntimeAssetLoader.ExternalTextureExists(trimmedPath, out _))
            {
                return false;
            }

            missingPath = trimmedPath;
            return true;
        }

        private static bool WarnIfMissingExternalTexturePath(string? texturePath)
        {
            if (!TryGetMissingExternalTexturePath(texturePath, out string missingPath))
            {
                return false;
            }

            WarnMissingTextureOnce(missingPath, $"[CharacterStudio] 运行时检测到缺失的外部纹理，已安全跳过: {missingPath}");
            return true;
        }

        private static void WarnMissingTextureOnce(string warningKey, string message)
        {
            if (string.IsNullOrWhiteSpace(warningKey))
            {
                return;
            }

            lock (missingTextureWarningLock)
            {
                if (!missingTextureWarnings.Add(warningKey))
                {
                    return;
                }
            }

            Log.Warning(message);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }
    }
}
