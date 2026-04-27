using UnityEngine;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 武器挥动动画预设参数
    /// </summary>
    public struct WeaponSwingPreset
    {
        public string nameKey;
        public float deployAngle;
        public float returnAngle;
        public int deployTicks;
        public int holdTicks;
        public int returnTicks;
        public Vector2 pivotOffset;
        public Vector3 deployOffset;

        public void ApplyTo(CharacterEquipmentRenderData renderData)
        {
            renderData.triggeredDeployAngle = deployAngle;
            renderData.triggeredReturnAngle = returnAngle;
            renderData.triggeredDeployTicks = deployTicks;
            renderData.triggeredHoldTicks = holdTicks;
            renderData.triggeredReturnTicks = returnTicks;
            renderData.triggeredPivotOffset = pivotOffset;
            renderData.triggeredDeployOffset = deployOffset;
        }

        public void ApplyTo(EquipmentTriggeredAnimationOverride overrideData)
        {
            overrideData.triggeredDeployAngle = deployAngle;
            overrideData.triggeredReturnAngle = returnAngle;
            overrideData.triggeredDeployTicks = deployTicks;
            overrideData.triggeredHoldTicks = holdTicks;
            overrideData.triggeredReturnTicks = returnTicks;
            overrideData.triggeredPivotOffset = pivotOffset;
            overrideData.triggeredDeployOffset = deployOffset;
        }
    }

    /// <summary>
    /// 内置武器挥动动画预设库
    /// </summary>
    public static class WeaponSwingPresetLibrary
    {
        public static readonly WeaponSwingPreset[] Presets = new WeaponSwingPreset[]
        {
            // 横斩：从右上向左下横劈
            new WeaponSwingPreset
            {
                nameKey = "CS_Studio_Equip_SwingPreset_HorizontalSlash",
                deployAngle = -120f,
                returnAngle = 10f,
                deployTicks = 12,
                holdTicks = 4,
                returnTicks = 18,
                pivotOffset = new Vector2(0f, -0.3f),
                deployOffset = new Vector3(0.2f, 0f, 0.1f)
            },
            // 纵劈：从上向下重劈
            new WeaponSwingPreset
            {
                nameKey = "CS_Studio_Equip_SwingPreset_VerticalSlash",
                deployAngle = 150f,
                returnAngle = -10f,
                deployTicks = 14,
                holdTicks = 6,
                returnTicks = 20,
                pivotOffset = new Vector2(0f, -0.2f),
                deployOffset = new Vector3(0f, 0f, 0.15f)
            },
            // 突刺：直线向前刺出
            new WeaponSwingPreset
            {
                nameKey = "CS_Studio_Equip_SwingPreset_Thrust",
                deployAngle = 5f,
                returnAngle = 5f,
                deployTicks = 8,
                holdTicks = 8,
                returnTicks = 16,
                pivotOffset = Vector2.zero,
                deployOffset = new Vector3(0.4f, 0f, -0.1f)
            },
            // 上挑：从下向上挑斩
            new WeaponSwingPreset
            {
                nameKey = "CS_Studio_Equip_SwingPreset_Uppercut",
                deployAngle = -30f,
                returnAngle = 130f,
                deployTicks = 10,
                holdTicks = 4,
                returnTicks = 18,
                pivotOffset = new Vector2(0f, -0.3f),
                deployOffset = new Vector3(0.1f, 0f, -0.1f)
            },
            // 回旋：大幅度回旋斩
            new WeaponSwingPreset
            {
                nameKey = "CS_Studio_Equip_SwingPreset_SpinSlash",
                deployAngle = -300f,
                returnAngle = 10f,
                deployTicks = 20,
                holdTicks = 2,
                returnTicks = 16,
                pivotOffset = new Vector2(0f, -0.15f),
                deployOffset = new Vector3(0.15f, 0f, 0f)
            },
            // 轻击：快速小幅挥击
            new WeaponSwingPreset
            {
                nameKey = "CS_Studio_Equip_SwingPreset_LightAttack",
                deployAngle = -45f,
                returnAngle = 0f,
                deployTicks = 6,
                holdTicks = 2,
                returnTicks = 10,
                pivotOffset = new Vector2(0f, -0.2f),
                deployOffset = new Vector3(0.1f, 0f, 0.05f)
            },
            // 重击：缓慢大力劈砍
            new WeaponSwingPreset
            {
                nameKey = "CS_Studio_Equip_SwingPreset_HeavyAttack",
                deployAngle = -160f,
                returnAngle = 15f,
                deployTicks = 20,
                holdTicks = 10,
                returnTicks = 25,
                pivotOffset = new Vector2(0f, -0.35f),
                deployOffset = new Vector3(0.3f, 0f, 0.2f)
            },
            // 格挡举盾：向前举起武器/盾牌
            new WeaponSwingPreset
            {
                nameKey = "CS_Studio_Equip_SwingPreset_Block",
                deployAngle = -70f,
                returnAngle = -70f,
                deployTicks = 10,
                holdTicks = 60,
                returnTicks = 15,
                pivotOffset = new Vector2(0f, -0.25f),
                deployOffset = new Vector3(-0.1f, 0f, 0.2f)
            },
        };
    }
}