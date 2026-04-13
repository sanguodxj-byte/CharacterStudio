using UnityEngine;
using Verse;

namespace CharacterStudio.Core
{
    public class DefModExtension_WeaponRender : DefModExtension
    {
        public bool enabled = true;
        public bool applyToOffHand = true;
        public Vector3 offset = Vector3.zero;
        public Vector3 offsetSouth = Vector3.zero;
        public Vector3 offsetNorth = Vector3.zero;
        public Vector3 offsetEast = Vector3.zero;
        public Vector2 scale = Vector2.one;

        public WeaponCarryVisualConfig carryVisual = new WeaponCarryVisualConfig();

        public PawnAnimationConfig ToConfig()
        {
            return new PawnAnimationConfig
            {
                enabled = true,
                weaponOverrideEnabled = this.enabled,
                applyToOffHand = this.applyToOffHand,
                offset = this.offset,
                offsetSouth = this.offsetSouth,
                offsetNorth = this.offsetNorth,
                offsetEast = this.offsetEast,
                scale = this.scale,
                carryVisual = this.carryVisual?.Clone() ?? new WeaponCarryVisualConfig()
            };
        }

        public static DefModExtension_WeaponRender FromConfig(PawnAnimationConfig config)
        {
            return new DefModExtension_WeaponRender
            {
                enabled = config.weaponOverrideEnabled,
                applyToOffHand = config.applyToOffHand,
                offset = config.offset,
                offsetSouth = config.offsetSouth,
                offsetNorth = config.offsetNorth,
                offsetEast = config.offsetEast,
                scale = config.scale,
                carryVisual = config.carryVisual?.Clone() ?? new WeaponCarryVisualConfig()
            };
        }
    }
}
