using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CharacterStudio.Core
{
    public readonly struct CharacterStudioLayerAnimationContext
    {
        public readonly Pawn pawn;
        public readonly PawnLayerConfig layer;
        public readonly Rot4 facing;
        public readonly int currentTick;
        public readonly bool isPreview;

        public CharacterStudioLayerAnimationContext(Pawn pawn, PawnLayerConfig layer, Rot4 facing, int currentTick, bool isPreview)
        {
            this.pawn = pawn;
            this.layer = layer;
            this.facing = facing;
            this.currentTick = currentTick;
            this.isPreview = isPreview;
        }
    }

    public readonly struct CharacterStudioLayerAnimationResult
    {
        public readonly float angle;
        public readonly Vector3 offset;
        public readonly Vector3 scaleMultiplier;

        public CharacterStudioLayerAnimationResult(float angle, Vector3 offset, Vector3 scaleMultiplier)
        {
            this.angle = angle;
            this.offset = offset;
            this.scaleMultiplier = scaleMultiplier;
        }

        public static CharacterStudioLayerAnimationResult Identity()
            => new CharacterStudioLayerAnimationResult(0f, Vector3.zero, Vector3.one);
    }

    public delegate bool CharacterStudioLayerAnimationMatcher(CharacterStudioLayerAnimationContext context);
    public delegate CharacterStudioLayerAnimationResult CharacterStudioLayerAnimationProvider(CharacterStudioLayerAnimationContext context);

    internal readonly struct CharacterStudioLayerAnimationRegistration
    {
        public readonly string id;
        public readonly CharacterStudioLayerAnimationMatcher? matcher;
        public readonly CharacterStudioLayerAnimationProvider provider;

        public CharacterStudioLayerAnimationRegistration(
            string id,
            CharacterStudioLayerAnimationMatcher? matcher,
            CharacterStudioLayerAnimationProvider provider)
        {
            this.id = id;
            this.matcher = matcher;
            this.provider = provider;
        }
    }

    /// <summary>
    /// 外部图层动画注册表。
    /// 允许第三方模组为每个 PawnLayerConfig 提供附加动画增量，运行时会叠加到内建动画结果之后。
    /// </summary>
    public static class CharacterStudioLayerAnimationRegistry
    {
        private static readonly Dictionary<string, CharacterStudioLayerAnimationRegistration> ProvidersById =
            new Dictionary<string, CharacterStudioLayerAnimationRegistration>(StringComparer.OrdinalIgnoreCase);

        public static bool HasProviders => ProvidersById.Count > 0;

        public static void RegisterProvider(string id, CharacterStudioLayerAnimationProvider provider)
            => RegisterProvider(id, null, provider);

        public static void RegisterProvider(
            string id,
            CharacterStudioLayerAnimationMatcher? matcher,
            CharacterStudioLayerAnimationProvider provider)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Provider id cannot be empty.", nameof(id));
            }

            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            string normalizedId = id.Trim();
            ProvidersById[normalizedId] = new CharacterStudioLayerAnimationRegistration(normalizedId, matcher, provider);
        }

        public static bool UnregisterProvider(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            return ProvidersById.Remove(id.Trim());
        }

        public static CharacterStudioLayerAnimationResult Evaluate(CharacterStudioLayerAnimationContext context)
        {
            float totalAngle = 0f;
            Vector3 totalOffset = Vector3.zero;
            Vector3 totalScale = Vector3.one;

            foreach (CharacterStudioLayerAnimationRegistration entry in ProvidersById.Values)
            {
                try
                {
                    if (entry.matcher != null && !entry.matcher(context))
                    {
                        continue;
                    }

                    CharacterStudioLayerAnimationResult result = entry.provider(context);
                    totalAngle += result.angle;
                    totalOffset += result.offset;
                    totalScale = Vector3.Scale(totalScale, result.scaleMultiplier);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CharacterStudio] Layer animation provider '{entry.id}' failed on layer '{context.layer?.layerName}': {ex.Message}");
                }
            }

            return new CharacterStudioLayerAnimationResult(totalAngle, totalOffset, totalScale);
        }
    }
}
