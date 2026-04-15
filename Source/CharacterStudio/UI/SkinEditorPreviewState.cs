using System;
using CharacterStudio.Core;
using RimWorld;
using Verse;
using UnityEngine;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 皮肤编辑器预览状态。
    /// 包含所有预览覆写、动画播放和渲染缓存字段。
    /// 从 Dialog_SkinEditor 中提取，使各 tab partial 无需了解全局状态容器。
    /// </summary>
    public sealed class SkinEditorPreviewState
    {
        // ─── 基础预览参数 ───
        public Rot4 previewRotation = Rot4.South;
        public float previewZoom = 1f;

        // ─── 表情覆写 ───
        public bool previewExpressionOverrideEnabled = false;
        public ExpressionType previewExpression = ExpressionType.Neutral;
        public bool previewRuntimeExpressionOverrideEnabled = false;
        public ExpressionType previewRuntimeExpression = ExpressionType.Neutral;
        public bool previewMouthStateOverrideEnabled = false;
        public MouthState previewMouthState = MouthState.Normal;
        public bool previewLidStateOverrideEnabled = false;
        public LidState previewLidState = LidState.Normal;
        public bool previewBrowStateOverrideEnabled = false;
        public BrowState previewBrowState = BrowState.Normal;
        public bool previewEmotionStateOverrideEnabled = false;
        public EmotionOverlayState previewEmotionState = EmotionOverlayState.None;

        // ─── 眼睛方向覆写 ───
        public bool previewEyeDirectionOverrideEnabled = false;
        public EyeDirection previewEyeDirection = EyeDirection.Center;
        public bool previewGazeCursorEnabled = false;
        public Vector2 previewGazeCursorOffset = Vector2.zero;

        // ─── 自动播放 ───
        public bool previewAutoPlayEnabled = false;
        public float previewAutoPlayIntervalSeconds = 0.75f;
        public float previewAutoPlayNextStepTime = 0f;
        public int previewAutoPlayStepIndex = 0;

        // ─── 面部动画播放 ───
        public bool previewFaceAnimationPlaying = false;
        public bool previewFaceAnimationLoop = false;
        public float previewFaceAnimationLastRealtime = -1f;
        public float previewFaceAnimationElapsedTicks = 0f;

        // ─── 装备动画播放 ───
        public bool previewEquipmentAnimationPlaying = false;
        public bool previewEquipmentAnimationLoop = false;
        public float previewEquipmentAnimationLastRealtime = -1f;
        public float previewEquipmentAnimationElapsedTicks = 0f;
        public string previewEquipmentAnimationTriggerKey = string.Empty;

        // ─── 武器携带视觉 ───
        public WeaponCarryVisualState previewWeaponCarryState = WeaponCarryVisualState.Undrafted;

        /// <summary>
        /// 重置所有覆写为默认值。
        /// </summary>
        public void ResetAllOverrides()
        {
            previewExpressionOverrideEnabled = false;
            previewExpression = ExpressionType.Neutral;
            previewRuntimeExpressionOverrideEnabled = false;
            previewRuntimeExpression = ExpressionType.Neutral;
            previewMouthStateOverrideEnabled = false;
            previewMouthState = MouthState.Normal;
            previewLidStateOverrideEnabled = false;
            previewLidState = LidState.Normal;
            previewBrowStateOverrideEnabled = false;
            previewBrowState = BrowState.Normal;
            previewEmotionStateOverrideEnabled = false;
            previewEmotionState = EmotionOverlayState.None;
            previewEyeDirectionOverrideEnabled = false;
            previewEyeDirection = EyeDirection.Center;
            previewGazeCursorEnabled = false;
            previewGazeCursorOffset = Vector2.zero;
        }
    }
}
