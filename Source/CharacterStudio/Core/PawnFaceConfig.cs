using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using UnityEngine;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 表情工作流模式
    /// FullFaceSwap = 传统整脸换图
    /// LayeredDynamic = 分层资源配置（眉眼嘴/覆盖层等）
    /// </summary>
    public enum FaceWorkflowMode
    {
        FullFaceSwap,
        LayeredDynamic
    }

    /// <summary>
    /// 分层面部部件类型
    /// </summary>
    public enum LayeredFacePartType
    {
        Base,
        Brow,
        Eye,
        Pupil,
        UpperLid,
        LowerLid,
        Mouth,
        Blush,
        Sweat,
        Tear,
        Overlay
    }

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
    /// 分层面部部件配置
    /// path 命名遵循资源规范，由代码在运行时决定如何使用。
    /// </summary>
    public class LayeredFacePartConfig
    {
        public LayeredFacePartType partType = LayeredFacePartType.Base;
        public ExpressionType expression = ExpressionType.Neutral;
        public string texPath = string.Empty;
        public bool enabled = true;

        /// <summary>
        /// 当 partType = Overlay 时，用于区分多个 Overlay 条目。
        /// 旧数据为空时会在运行时被视为默认 "Overlay"。
        /// </summary>
        public string overlayId = string.Empty;

        /// <summary>
        /// 当 partType = Overlay 时，表示编辑器内排序顺序。数值越小越先绘制。
        /// </summary>
        public int overlayOrder = 0;

        /// <summary>
        /// 仅用于少量兜底修正；正常工作流应由代码自动定位和移动。
        /// </summary>
        public Vector2 anchorCorrection = Vector2.zero;

        public LayeredFacePartConfig Clone()
        {
            return new LayeredFacePartConfig
            {
                partType = this.partType,
                expression = this.expression,
                texPath = this.texPath,
                enabled = this.enabled,
                overlayId = this.overlayId,
                overlayOrder = this.overlayOrder,
                anchorCorrection = this.anchorCorrection
            };
        }
    }

    /// <summary>
    /// 完整的面部表情配置
    ///
    /// 设计思路：
    /// 1. FullFaceSwap：通过切换整张头部贴图（或帧序列）实现表情变化；
    /// 2. LayeredDynamic：通过命名驱动的分层资源记录部件素材，为后续动态眉眼嘴/覆盖层工作流提供数据基础。
    ///
    /// 性能优化：内部维护 Dictionary 缓存，GetTexPath 为 O(1) 查找。
    /// </summary>
    public class PawnFaceConfig
    {
        public bool enabled = false;

        /// <summary>表情工作流模式：整脸换图 / 分层动态</summary>
        public FaceWorkflowMode workflowMode = FaceWorkflowMode.FullFaceSwap;

        /// <summary>分层模式的资源根目录（可选）</summary>
        public string layeredSourceRoot = string.Empty;

        /// <summary>分层模式识别到的部件资源列表</summary>
        public List<LayeredFacePartConfig> layeredParts = new List<LayeredFacePartConfig>();

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

        private static bool IsOverlayPart(LayeredFacePartType partType)
        {
            return partType == LayeredFacePartType.Overlay;
        }

        private static string NormalizeOverlayId(string? overlayId)
        {
            return string.IsNullOrWhiteSpace(overlayId) ? "Overlay" : overlayId!.Trim();
        }

        private static bool MatchesOverlayId(LayeredFacePartConfig? part, string overlayId)
        {
            if (part == null)
                return false;

            return string.Equals(
                NormalizeOverlayId(part.overlayId),
                NormalizeOverlayId(overlayId),
                StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerable<LayeredFacePartConfig> EnumerateLayeredParts(
            LayeredFacePartType partType,
            string? overlayId = null,
            bool includeAllOverlayGroups = false)
        {
            if (layeredParts == null || layeredParts.Count == 0)
                return Enumerable.Empty<LayeredFacePartConfig>();

            IEnumerable<LayeredFacePartConfig> query = layeredParts.Where(p => p != null && p.partType == partType);

            if (IsOverlayPart(partType) && !includeAllOverlayGroups)
            {
                string normalizedOverlayId = NormalizeOverlayId(overlayId);
                query = query.Where(p => MatchesOverlayId(p, normalizedOverlayId));
            }

            if (IsOverlayPart(partType))
            {
                query = query
                    .OrderBy(p => p.overlayOrder)
                    .ThenBy(p => NormalizeOverlayId(p.overlayId), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.expression);
            }
            else
            {
                query = query.OrderBy(p => p.expression);
            }

            return query;
        }

        private LayeredFacePartConfig? GetLayeredPartConfigInternal(
            LayeredFacePartType partType,
            ExpressionType expression,
            string? overlayId = null,
            bool includeAllOverlayGroups = false)
        {
            if (layeredParts == null || layeredParts.Count == 0)
                return null;

            LayeredFacePartConfig? exact = EnumerateLayeredParts(partType, overlayId, includeAllOverlayGroups)
                .FirstOrDefault(p =>
                    p.enabled
                    && p.expression == expression
                    && !string.IsNullOrWhiteSpace(p.texPath));

            if (exact != null)
                return exact;

            if (expression != ExpressionType.Neutral)
            {
                LayeredFacePartConfig? neutral = EnumerateLayeredParts(partType, overlayId, includeAllOverlayGroups)
                    .FirstOrDefault(p =>
                        p.enabled
                        && p.expression == ExpressionType.Neutral
                        && !string.IsNullOrWhiteSpace(p.texPath));

                if (neutral != null)
                    return neutral;
            }

            return null;
        }

        public LayeredFacePartConfig? GetLayeredPartConfig(LayeredFacePartType partType, ExpressionType expression)
        {
            return GetLayeredPartConfigInternal(partType, expression, null, includeAllOverlayGroups: true);
        }

        public LayeredFacePartConfig? GetLayeredPartConfig(LayeredFacePartType partType, ExpressionType expression, string overlayId)
        {
            return GetLayeredPartConfigInternal(partType, expression, overlayId, includeAllOverlayGroups: false);
        }

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
        /// 获取分层模式中指定部件类型/表情的贴图路径。
        /// 优先精确表情匹配，失败时回退 Neutral。
        /// Overlay 未指定 overlayId 时，会按 overlayOrder 返回第一个可用条目。
        /// </summary>
        public string GetLayeredPartPath(LayeredFacePartType partType, ExpressionType expression)
        {
            return GetLayeredPartConfigInternal(partType, expression, null, includeAllOverlayGroups: true)?.texPath ?? string.Empty;
        }

        /// <summary>
        /// 获取指定 Overlay 分组中的贴图路径。
        /// </summary>
        public string GetLayeredPartPath(LayeredFacePartType partType, ExpressionType expression, string overlayId)
        {
            return GetLayeredPartConfigInternal(partType, expression, overlayId, includeAllOverlayGroups: false)?.texPath ?? string.Empty;
        }

        /// <summary>获取指定类型的已启用分层部件数量</summary>
        public int CountLayeredParts(LayeredFacePartType partType)
        {
            return EnumerateLayeredParts(partType, null, includeAllOverlayGroups: true).Count(p =>
                p.enabled && !string.IsNullOrWhiteSpace(p.texPath));
        }

        /// <summary>获取指定 Overlay 分组内的已启用分层部件数量</summary>
        public int CountLayeredParts(LayeredFacePartType partType, string overlayId)
        {
            return EnumerateLayeredParts(partType, overlayId, includeAllOverlayGroups: false).Count(p =>
                p.enabled && !string.IsNullOrWhiteSpace(p.texPath));
        }

        /// <summary>
        /// 获取指定分层部件类型的任意可用贴图路径。
        /// 优先返回 Neutral，对未配置 Neutral 的旧数据则回退到首个已启用路径。
        /// Overlay 未指定 overlayId 时，会按 overlayOrder 返回第一个可用条目。
        /// </summary>
        public string GetAnyLayeredPartPath(LayeredFacePartType partType)
        {
            if (layeredParts == null || layeredParts.Count == 0)
                return string.Empty;

            LayeredFacePartConfig? neutral = EnumerateLayeredParts(partType, null, includeAllOverlayGroups: true)
                .FirstOrDefault(p =>
                    p.enabled
                    && p.expression == ExpressionType.Neutral
                    && !string.IsNullOrWhiteSpace(p.texPath));

            if (neutral != null)
                return neutral.texPath ?? string.Empty;

            LayeredFacePartConfig? first = EnumerateLayeredParts(partType, null, includeAllOverlayGroups: true)
                .FirstOrDefault(p =>
                    p.enabled
                    && !string.IsNullOrWhiteSpace(p.texPath));

            return first?.texPath ?? string.Empty;
        }

        /// <summary>
        /// 获取指定 Overlay 分组的任意可用贴图路径。
        /// 优先返回 Neutral，对未配置 Neutral 的旧数据则回退到首个已启用路径。
        /// </summary>
        public string GetAnyLayeredPartPath(LayeredFacePartType partType, string overlayId)
        {
            if (layeredParts == null || layeredParts.Count == 0)
                return string.Empty;

            LayeredFacePartConfig? neutral = EnumerateLayeredParts(partType, overlayId, includeAllOverlayGroups: false)
                .FirstOrDefault(p =>
                    p.enabled
                    && p.expression == ExpressionType.Neutral
                    && !string.IsNullOrWhiteSpace(p.texPath));

            if (neutral != null)
                return neutral.texPath ?? string.Empty;

            LayeredFacePartConfig? first = EnumerateLayeredParts(partType, overlayId, includeAllOverlayGroups: false)
                .FirstOrDefault(p =>
                    p.enabled
                    && !string.IsNullOrWhiteSpace(p.texPath));

            return first?.texPath ?? string.Empty;
        }

        public List<string> GetOrderedOverlayIds()
        {
            if (layeredParts == null || layeredParts.Count == 0)
                return new List<string>();

            return layeredParts
                .Where(p => p != null && p.partType == LayeredFacePartType.Overlay)
                .GroupBy(p => NormalizeOverlayId(p.overlayId), StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Min(p => p.overlayOrder))
                .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Key)
                .ToList();
        }

        public int GetOverlayOrder(string overlayId)
        {
            if (layeredParts == null || layeredParts.Count == 0)
                return 0;

            string normalized = NormalizeOverlayId(overlayId);
            return layeredParts
                .Where(p => p != null
                    && p.partType == LayeredFacePartType.Overlay
                    && MatchesOverlayId(p, normalized))
                .Select(p => p.overlayOrder)
                .DefaultIfEmpty(0)
                .Min();
        }

        public void SetOverlayOrder(string overlayId, int overlayOrder)
        {
            if (layeredParts == null || layeredParts.Count == 0)
                return;

            string normalized = NormalizeOverlayId(overlayId);
            foreach (var part in layeredParts.Where(p =>
                         p != null
                         && p.partType == LayeredFacePartType.Overlay
                         && MatchesOverlayId(p, normalized)))
            {
                part.overlayId = normalized;
                part.overlayOrder = overlayOrder;
            }
        }

        public void NormalizeOverlayOrders()
        {
            List<string> orderedOverlayIds = GetOrderedOverlayIds();
            for (int i = 0; i < orderedOverlayIds.Count; i++)
            {
                SetOverlayOrder(orderedOverlayIds[i], i);
            }
        }

        public void EnsureOverlayEntry(string overlayId)
        {
            layeredParts ??= new List<LayeredFacePartConfig>();

            string normalized = NormalizeOverlayId(overlayId);
            bool exists = layeredParts.Any(p =>
                p != null
                && p.partType == LayeredFacePartType.Overlay
                && MatchesOverlayId(p, normalized));

            if (exists)
                return;

            layeredParts.Add(new LayeredFacePartConfig
            {
                partType = LayeredFacePartType.Overlay,
                expression = ExpressionType.Neutral,
                texPath = string.Empty,
                enabled = false,
                overlayId = normalized,
                overlayOrder = GetOrderedOverlayIds().Count
            });
        }

        public void RemoveOverlayGroup(string overlayId)
        {
            if (layeredParts == null || layeredParts.Count == 0)
                return;

            string normalized = NormalizeOverlayId(overlayId);
            layeredParts.RemoveAll(p =>
                p != null
                && p.partType == LayeredFacePartType.Overlay
                && MatchesOverlayId(p, normalized));

            NormalizeOverlayOrders();
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

        public void SetLayeredPart(LayeredFacePartType partType, ExpressionType expression, string texPath)
        {
            if (IsOverlayPart(partType))
            {
                SetLayeredPart(partType, expression, texPath, "Overlay", 0);
                return;
            }

            layeredParts ??= new List<LayeredFacePartConfig>();
            var existing = layeredParts.FirstOrDefault(p => p.partType == partType && p.expression == expression);
            if (existing != null)
            {
                existing.texPath = texPath ?? string.Empty;
                existing.enabled = !string.IsNullOrWhiteSpace(texPath);
            }
            else
            {
                layeredParts.Add(new LayeredFacePartConfig
                {
                    partType = partType,
                    expression = expression,
                    texPath = texPath ?? string.Empty,
                    enabled = !string.IsNullOrWhiteSpace(texPath)
                });
            }
        }

        public void SetLayeredPart(LayeredFacePartType partType, ExpressionType expression, string texPath, string overlayId, int overlayOrder = 0)
        {
            if (!IsOverlayPart(partType))
            {
                SetLayeredPart(partType, expression, texPath);
                return;
            }

            layeredParts ??= new List<LayeredFacePartConfig>();
            string normalizedOverlayId = NormalizeOverlayId(overlayId);

            var existing = layeredParts.FirstOrDefault(p =>
                p.partType == partType
                && p.expression == expression
                && MatchesOverlayId(p, normalizedOverlayId));

            if (existing != null)
            {
                existing.texPath = texPath ?? string.Empty;
                existing.enabled = !string.IsNullOrWhiteSpace(texPath);
                existing.overlayId = normalizedOverlayId;
                existing.overlayOrder = overlayOrder;
            }
            else
            {
                layeredParts.Add(new LayeredFacePartConfig
                {
                    partType = partType,
                    expression = expression,
                    texPath = texPath ?? string.Empty,
                    enabled = !string.IsNullOrWhiteSpace(texPath),
                    overlayId = normalizedOverlayId,
                    overlayOrder = overlayOrder
                });
            }
        }

        public void RemoveLayeredPart(LayeredFacePartType partType, ExpressionType expression)
        {
            if (IsOverlayPart(partType))
            {
                RemoveLayeredPart(partType, expression, "Overlay");
                return;
            }

            layeredParts?.RemoveAll(p => p.partType == partType && p.expression == expression);
        }

        public void RemoveLayeredPart(LayeredFacePartType partType, ExpressionType expression, string overlayId)
        {
            if (!IsOverlayPart(partType))
            {
                RemoveLayeredPart(partType, expression);
                return;
            }

            string normalizedOverlayId = NormalizeOverlayId(overlayId);
            layeredParts?.RemoveAll(p =>
                p.partType == partType
                && p.expression == expression
                && MatchesOverlayId(p, normalizedOverlayId));

            NormalizeOverlayOrders();
        }

        /// <summary>是否配置了任何表情贴图</summary>
        public bool HasAnyExpression()
            => expressions.Exists(e =>
                !string.IsNullOrEmpty(e.texPath)
                || (e.frames != null && e.frames.Count > 0));

        public bool HasAnyLayeredPart()
            => layeredParts != null && layeredParts.Exists(p =>
                p != null
                && p.enabled
                && !string.IsNullOrWhiteSpace(p.texPath));

        public PawnFaceConfig Clone()
        {
            var clone = new PawnFaceConfig
            {
                enabled = this.enabled,
                workflowMode = this.workflowMode,
                layeredSourceRoot = this.layeredSourceRoot
            };

            foreach (var exp in this.expressions)
            {
                var clonedExp = new ExpressionTexPath
                {
                    expression = exp.expression,
                    texPath = exp.texPath
                };
                if (exp.frames != null)
                {
                    foreach (var f in exp.frames)
                        clonedExp.frames.Add(new ExpressionFrame { texPath = f.texPath, durationTicks = f.durationTicks });
                }
                clone.expressions.Add(clonedExp);
            }

            if (this.layeredParts != null)
            {
                foreach (var part in this.layeredParts)
                {
                    if (part != null)
                        clone.layeredParts.Add(part.Clone());
                }
            }

            // 克隆眼睛方向配置（若存在）
            clone.eyeDirectionConfig = this.eyeDirectionConfig?.Clone();
            // _lookupCache 保持 null，首次使用时懒初始化
            return clone;
        }
    }
}