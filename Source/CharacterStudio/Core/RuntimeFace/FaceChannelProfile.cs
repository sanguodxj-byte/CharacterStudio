using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 面部通道动画参数集。
    /// 一个通道（如 Brow）对应多个状态（如 Angry, Sad, Happy）的参数。
    /// JSON 反序列化入口，支持从外部 JSON 加载或从代码构建默认值。
    /// 
    /// 序列化约定（兼容 UnityEngine.JsonUtility）：
    ///   - 顶层使用数组字段存储 profile 列表
    ///   - 每个 profile 用 stateName 字符串键标识状态
    ///   - 参数以 TransformParams 内嵌结构存储
    /// </summary>
    [Serializable]
    public class FaceChannelProfileSet
    {
        /// <summary>通道标识（如 "brow", "upperLid", "overlay"）</summary>
        public string channel = string.Empty;

        /// <summary>该通道下所有状态的参数列表</summary>
        public FaceStateProfile[] profiles = Array.Empty<FaceStateProfile>();

        /// <summary>
        /// 运行时索引缓存。JsonUtility 不支持 Dictionary，
        /// 反序列化后调用此方法构建快速查找表。
        /// </summary>
        private Dictionary<string, FaceStateProfile>? _lookup;

        public void BuildLookup()
        {
            _lookup = new Dictionary<string, FaceStateProfile>(StringComparer.OrdinalIgnoreCase);
            if (profiles == null) return;
            foreach (var p in profiles)
            {
                if (p != null && !string.IsNullOrWhiteSpace(p.stateName))
                    _lookup[p.stateName] = p;
            }
        }

        /// <summary>
        /// 获取指定状态的参数。未找到时返回 null。
        /// </summary>
        public FaceStateProfile? GetProfile(string stateName)
        {
            if (_lookup == null) BuildLookup();
            if (_lookup!.TryGetValue(stateName, out var profile)) return profile;
            return null;
        }

        /// <summary>
        /// 获取指定状态的参数，未找到时回退到 Default。
        /// </summary>
        public FaceStateProfile? GetProfileOrDefault(string stateName)
        {
            FaceStateProfile? p = GetProfile(stateName);
            if (p != null) return p;
            return GetProfile("Default");
        }
    }

    /// <summary>
    /// 单个状态的面部变换参数。
    /// 包含基础偏移和波叠加参数，以及过渡时长。
    /// 
    /// 求值公式（在 FaceTransformEvaluator 中执行）：
    ///   angle   = angleBase   + primaryWave * angleWave
    ///   offsetX = offsetXBase + primaryWave * offsetXWave
    ///   offsetZ = offsetZBase + primaryWave * offsetZWave  +  slowWave * offsetZSlowWave
    ///   scaleX  = scaleXBase  + |wave| * scaleXWave
    ///   scaleZ  = scaleZBase  + |wave| * scaleZWave
    /// </summary>
    [Serializable]
    public class FaceStateProfile
    {
        /// <summary>状态名称键（如 "Angry", "Happy", "Smile", "Blink"）</summary>
        public string stateName = string.Empty;

        /// <summary>
        /// 过渡时长（Tick）。状态切换时从前一参数插值到目标参数的持续时间。
        /// 设为 0 表示跳变（无过渡），适用于纹理替换等场景。
        /// </summary>
        public int blendIn = 0;

        // ── Angle ──
        public float angleBase = 0f;
        public float angleWave = 0f;

        // ── Offset X ──
        public float offsetXBase = 0f;
        public float offsetXWave = 0f;

        // ── Offset Z ──
        public float offsetZBase = 0f;
        public float offsetZWave = 0f;
        public float offsetZSlowWave = 0f;

        // ── Scale X ──
        public float scaleXBase = 1f;
        public float scaleXWave = 0f;

        // ── Scale Z ──
        public float scaleZBase = 1f;
        public float scaleZWave = 0f;

        // ── 侧偏（用于眼睑、瞳孔等需要区分左右的部件）──
        public float sideBiasX = 0f;

        // ── 特殊用途字段（部分通道使用）──

        /// <summary>上眼睑闭合时下移量（仅 upperLid/lowerLid 通道的 Blink/Close 状态）</summary>
        public float moveDown = 0f;

        /// <summary>通用缩放覆盖（当通道需要统一的缩放而非分离 x/z 时）</summary>
        public float uniformScale = 0f;

        /// <summary>
        /// 子变体列表。用于眼睑的 HappySoft/HappyOpen 等基于 EyeAnimationVariant 的细分。
        /// JsonUtility 不支持嵌套数组中的嵌套数组，所以用平面结构。
        /// </summary>
        public FaceStateVariant[] variants = Array.Empty<FaceStateVariant>();

        /// <summary>
        /// 获取指定变体的增量参数。未找到返回 null。
        /// </summary>
        public FaceStateVariant? GetVariant(string variantName)
        {
            if (variants == null) return null;
            foreach (var v in variants)
            {
                if (v != null && string.Equals(v.variantName, variantName, StringComparison.OrdinalIgnoreCase))
                    return v;
            }
            return null;
        }

        /// <summary>
        /// 创建一个空白 profile（所有参数为零/默认值）。
        /// </summary>
        public static FaceStateProfile Identity(string stateName, int blendIn = 0)
        {
            return new FaceStateProfile
            {
                stateName = stateName,
                blendIn = blendIn,
                scaleXBase = 1f,
                scaleZBase = 1f,
            };
        }
    }

    /// <summary>
    /// 变体增量参数。
    /// 叠加到父 FaceStateProfile 的基础值上。
    /// 用于同一个 LidState 下因 EyeAnimationVariant 不同而需要微调的场景。
    /// </summary>
    [Serializable]
    public class FaceStateVariant
    {
        /// <summary>变体名称（对应 EyeAnimationVariant 枚举名称）</summary>
        public string variantName = string.Empty;

        /// <summary>增量偏移 Z</summary>
        public float extraOffsetZ = 0f;

        /// <summary>覆盖 scaleZ（非增量，直接替换父 profile 的 scaleZBase）</summary>
        public float overrideScaleZ = float.MinValue; // MinValue 表示"未设置，不覆盖"

        /// <summary>覆盖 moveDown（非增量）</summary>
        public float overrideMoveDown = float.MinValue;

        /// <summary>是否提供了 overrideScaleZ</summary>
        public bool HasOverrideScaleZ => overrideScaleZ > float.MinValue;

        /// <summary>是否提供了 overrideMoveDown</summary>
        public bool HasOverrideMoveDown => overrideMoveDown > float.MinValue;
    }
}
