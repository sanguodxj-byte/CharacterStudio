using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 表情类型枚举
    /// 完整覆盖 NL Facial Animation 所有状态类别 + CS 独有状态
    /// </summary>
    public enum ExpressionType
    {
        // ── 基础情绪（对应 NL FA Mood.xml）──
        Neutral,        // 平静（默认回退）
        Happy,          // 快乐（mood > 0.8）
        Cheerful,       // 愉快（mood > 0.9，NL FA moodCheerful）
        Gloomy,         // 阴郁（mood 0.2–0.4，NL FA moodGloomy）
        Sad,            // 悲伤（mood < 0.2）
        Hopeless,       // 绝望（mood < 0.1，NL FA moodHopeless）

        // ── 生理/身体状态 ──
        Tired,          // 疲劳（rest < 0.15）
        Pain,           // 痛苦（倒地 / 受伤濒死）
        Sleeping,       // 睡眠（在床/闭眼）

        // ── 危险/战斗状态（对应 NL FA ForJobs/Attack*）──
        Angry,          // 愤怒（心理崩溃 / 攻击性精神状态）
        Scared,         // 恐惧（逃跑 / Panic 精神状态）
        Shock,          // 震惊（受到攻击，NL FA Reactive/ReceivedAnAttack）
        WaitCombat,     // 备战（NL FA ForJobs/WaitCombat — 举枪警戒状态）
        AttackMelee,    // 近战攻击中（NL FA ForJobs/AttackMelee）
        AttackRanged,   // 远程攻击中（NL FA ForJobs/AttackStatic）

        // ── 日常活动（对应 NL FA ForJobs）──
        Eating,         // 进食 / 饮酒（NL FA ForJobs/Ingest）
        Working,        // 工作 / 制作（NL FA ForJobs/DoBill）
        Hauling,        // 搬运（NL FA ForJobs/Haul）
        Reading,        // 阅读（NL FA ForJobs/Reading）
        SocialRelax,    // 社交放松（NL FA SocialRelax）
        Lovin,          // 亲密（NL FA ForJobs/Lovin）
        Strip,          // 脱装备（NL FA ForJobs/Strip）
        Goto,           // 移动中（NL FA ForJobs/Goto）
        LayDown,        // 躺下（NL FA ForJobs/LayDown）

        // ── 死亡状态 ──
        Dead,           // 死亡

        // ── 眨眼（内部驱动，不对应游戏状态）──
        Blink,          // 眨眼（随机触发，优先级最高）
    }

    /// <summary>
    /// 单帧表情帧定义
    /// </summary>
    public class ExpressionFrame
    {
        /// <summary>该帧的贴图路径（支持绝对路径和游戏内相对路径）</summary>
        public string texPath = "";

        /// <summary>
        /// 该帧持续时间（单位：Tick，60 Tick = 1秒）。
        /// 设为 0 或负数时视为静态帧（不参与循环推进）。
        /// </summary>
        public int durationTicks = 60;
    }

    /// <summary>
    /// 单条表情配置（支持单张静态贴图和多帧帧动画）
    ///
    /// 单张静态贴图（向后兼容）：
    ///   texPath = "path/to/face"
    ///
    /// 多帧帧动画：
    ///   frames = [ { texPath="frame0", durationTicks=6 }, { texPath="frame1", durationTicks=6 } ]
    ///   当 frames 不为空时，优先使用 frames；texPath 作为回退。
    /// </summary>
    public class ExpressionTexPath
    {
        public ExpressionType expression;

        /// <summary>静态贴图路径（单张，向后兼容）</summary>
        public string texPath = "";

        /// <summary>
        /// 帧动画序列。不为空时优先于 texPath 使用。
        /// 循环播放，每帧按 durationTicks 推进。
        /// </summary>
        public List<ExpressionFrame> frames = new List<ExpressionFrame>();

        /// <summary>是否为帧动画（有多帧定义）</summary>
        public bool IsAnimated => frames != null && frames.Count > 1;

        /// <summary>
        /// 获取指定 Tick 对应的贴图路径。
        /// 单张：忽略 tick，直接返回 texPath。
        /// 多帧：按 durationTicks 累计定位当前帧，循环播放。
        /// </summary>
        public string GetTexPathAtTick(int tick)
        {
            if (frames == null || frames.Count == 0)
                return texPath;

            if (frames.Count == 1)
                return frames[0].texPath;

            // 计算总周期
            int totalDuration = 0;
            foreach (var f in frames)
                totalDuration += f.durationTicks > 0 ? f.durationTicks : 1;

            if (totalDuration <= 0) return frames[0].texPath;

            int t = tick % totalDuration;
            int accumulated = 0;
            foreach (var f in frames)
            {
                int d = f.durationTicks > 0 ? f.durationTicks : 1;
                accumulated += d;
                if (t < accumulated)
                    return f.texPath;
            }
            return frames[frames.Count - 1].texPath;
        }
    }

    /// <summary>
    /// 完整的面部表情配置
    ///
    /// 设计思路：通过切换整张头部贴图（或帧序列）来实现表情变化。
    /// 支持帧动画：为同一表情配置多帧 ExpressionFrame，系统按 Tick 自动循环播放。
    ///
    /// 性能优化：内部维护 Dictionary 缓存，GetTexPath 为 O(1) 查找。
    /// </summary>
    public class PawnFaceConfig
    {
        public bool enabled = false;

        /// <summary>各表情对应的贴图/帧序列配置（XML 序列化用）</summary>
        public List<ExpressionTexPath> expressions = new List<ExpressionTexPath>();

        /// <summary>
        /// 眼睛方向覆盖层配置（可选，为 null 时不启用方向功能）
        /// 空值安全：渲染与编辑器侧均需做 null 防护。
        /// </summary>
        public PawnEyeDirectionConfig? eyeDirectionConfig = null;

        // Dictionary 缓存，首次 GetExpression 时懒初始化
        private Dictionary<ExpressionType, ExpressionTexPath>? _lookupCache;

        private Dictionary<ExpressionType, ExpressionTexPath> GetLookup()
        {
            if (_lookupCache != null) return _lookupCache;

            _lookupCache = new Dictionary<ExpressionType, ExpressionTexPath>();
            foreach (var e in expressions)
            {
                bool hasContent = !string.IsNullOrEmpty(e.texPath)
                    || (e.frames != null && e.frames.Count > 0);
                if (hasContent)
                    _lookupCache[e.expression] = e;
            }
            return _lookupCache;
        }

        private void InvalidateLookup() => _lookupCache = null;

        /// <summary>
        /// 获取指定表情在指定 Tick 的贴图路径（O(1) 查找 + 帧动画支持）。
        /// 未找到时回退到 Neutral；Neutral 也未配置则返回空字符串。
        /// </summary>
        public string GetTexPath(ExpressionType expression, int tick = 0)
        {
            var lookup = GetLookup();

            if (lookup.TryGetValue(expression, out var entry))
            {
                string path = entry.GetTexPathAtTick(tick);
                if (!string.IsNullOrEmpty(path)) return path;
            }

            // 回退到 Neutral
            if (expression != ExpressionType.Neutral
                && lookup.TryGetValue(ExpressionType.Neutral, out var neutralEntry))
            {
                string neutralPath = neutralEntry.GetTexPathAtTick(tick);
                if (!string.IsNullOrEmpty(neutralPath)) return neutralPath;
            }

            return string.Empty;
        }

        /// <summary>获取指定表情的配置对象（用于帧动画 Tick 计算）</summary>
        public ExpressionTexPath? GetExpression(ExpressionType expression)
        {
            var lookup = GetLookup();
            if (lookup.TryGetValue(expression, out var entry)) return entry;
            if (expression != ExpressionType.Neutral
                && lookup.TryGetValue(ExpressionType.Neutral, out var neutral)) return neutral;
            return null;
        }

        /// <summary>
        /// 设置或更新指定表情的静态贴图路径，并清空该表情的帧动画序列（切换为静态模式）。
        /// 向后兼容接口，新代码可使用 <see cref="AddFrame"/> 追加帧。
        /// </summary>
        public void SetTexPath(ExpressionType expression, string texPath)
        {
            var existing = expressions.Find(e => e.expression == expression);
            if (existing != null)
            {
                existing.texPath = texPath;
                existing.frames.Clear(); // 切换为静态模式
            }
            else
            {
                expressions.Add(new ExpressionTexPath { expression = expression, texPath = texPath });
            }
            InvalidateLookup();
        }

        /// <summary>
        /// 向指定表情追加一帧（帧动画模式）。
        /// 若表情当前为静态贴图，自动将原贴图作为第一帧迁移后追加新帧。
        /// </summary>
        public void AddFrame(ExpressionType expression, string texPath, int durationTicks = 6)
        {
            var existing = expressions.Find(e => e.expression == expression);
            if (existing == null)
            {
                existing = new ExpressionTexPath { expression = expression };
                expressions.Add(existing);
            }

            // 静态贴图 → 帧动画升级：将原 texPath 作为第一帧
            if (!string.IsNullOrEmpty(existing.texPath) && existing.frames.Count == 0)
            {
                existing.frames.Add(new ExpressionFrame { texPath = existing.texPath, durationTicks = durationTicks });
                existing.texPath = string.Empty;
            }

            existing.frames.Add(new ExpressionFrame { texPath = texPath, durationTicks = durationTicks });
            InvalidateLookup();
        }

        /// <summary>移除指定表情的某一帧，若移除后无帧则自动切换回静态模式（texPath 置空）</summary>
        public void RemoveFrame(ExpressionType expression, int frameIndex)
        {
            var existing = expressions.Find(e => e.expression == expression);
            if (existing == null || frameIndex < 0 || frameIndex >= existing.frames.Count) return;

            existing.frames.RemoveAt(frameIndex);
            if (existing.frames.Count == 0)
            {
                existing.texPath = string.Empty; // 无帧则清空
            }
            InvalidateLookup();
        }

        /// <summary>是否配置了任何表情贴图</summary>
        public bool HasAnyExpression()
            => expressions.Exists(e =>
                !string.IsNullOrEmpty(e.texPath)
                || (e.frames != null && e.frames.Count > 0));

        public PawnFaceConfig Clone()
        {
            var clone = new PawnFaceConfig { enabled = this.enabled };
            foreach (var exp in this.expressions)
            {
                var clonedExp = new ExpressionTexPath
                {
                    expression = exp.expression,
                    texPath    = exp.texPath
                };
                if (exp.frames != null)
                {
                    foreach (var f in exp.frames)
                        clonedExp.frames.Add(new ExpressionFrame { texPath = f.texPath, durationTicks = f.durationTicks });
                }
                clone.expressions.Add(clonedExp);
            }
            // 克隆眼睛方向配置（若存在）
            clone.eyeDirectionConfig = this.eyeDirectionConfig?.Clone();
            // _lookupCache 保持 null，首次使用时懒初始化
            return clone;
        }
    }
}
