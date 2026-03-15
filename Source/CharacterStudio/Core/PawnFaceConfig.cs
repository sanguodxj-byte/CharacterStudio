using System;
using System.Collections.Generic;
using Verse;
using UnityEngine;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 表情类型枚举
    /// </summary>
    public enum ExpressionType
    {
        Neutral,    // 平静
        Happy,      // 快乐
        Sad,        // 悲伤
        Angry,      // 愤怒
        Scared,     // 恐惧（逃跑/恐慌精神状态）
        Pain,       // 痛苦（倒地）
        Shock,      // 震惊（保留，可由外部触发器设置）
        Tired,      // 疲劳（休息值极低）
        Eating,     // 进食
        Sleeping,   // 睡眠（闭眼）
        Blink,      // 眨眼
        Dead        // 死亡
    }

    /// <summary>
    /// 单条表情 -> 头部贴图路径映射
    /// texPath 指向一张完整的头部贴图（包含对应表情的五官）
    /// 支持绝对路径（外部文件）和游戏内相对路径
    /// </summary>
    public class ExpressionTexPath
    {
        public ExpressionType expression;
        public string texPath = "";
    }

    /// <summary>
    /// 完整的面部表情配置
    ///
    /// 设计思路：通过切换整张头部贴图来实现表情变化。
    /// 不再操控眼睛、嘴巴、眉毛等子节点（这些节点在启用 Head 槽位时会被隐藏）。
    /// 表情贴图应包含该表情下完整的面部五官。
    ///
    /// 性能优化：内部维护 Dictionary 缓存，GetTexPath 为 O(1) 查找。
    /// 写操作（SetTexPath）同时更新列表与字典保持一致。
    /// </summary>
    public class PawnFaceConfig
    {
        public bool enabled = false;

        /// <summary>各表情对应的头部贴图路径列表（XML 序列化用）</summary>
        public List<ExpressionTexPath> expressions = new List<ExpressionTexPath>();

        // Dictionary 缓存，首次 GetTexPath 时懒初始化
        private Dictionary<ExpressionType, string>? _lookupCache;

        private Dictionary<ExpressionType, string> GetLookup()
        {
            if (_lookupCache != null) return _lookupCache;

            _lookupCache = new Dictionary<ExpressionType, string>();
            foreach (var e in expressions)
            {
                if (!string.IsNullOrEmpty(e.texPath))
                    _lookupCache[e.expression] = e.texPath;
            }
            return _lookupCache;
        }

        private void InvalidateLookup() => _lookupCache = null;

        /// <summary>
        /// 获取指定表情的贴图路径（O(1)）。
        /// 未找到时回退到 Neutral；Neutral 也未配置则返回空字符串（原版渲染保持不变）。
        /// </summary>
        public string GetTexPath(ExpressionType expression)
        {
            var lookup = GetLookup();

            if (lookup.TryGetValue(expression, out string path) && !string.IsNullOrEmpty(path))
                return path;

            // 回退到 Neutral
            if (expression != ExpressionType.Neutral
                && lookup.TryGetValue(ExpressionType.Neutral, out string neutralPath)
                && !string.IsNullOrEmpty(neutralPath))
                return neutralPath;

            return string.Empty;
        }

        /// <summary>设置或更新指定表情的贴图路径</summary>
        public void SetTexPath(ExpressionType expression, string texPath)
        {
            var existing = expressions.Find(e => e.expression == expression);
            if (existing != null)
                existing.texPath = texPath;
            else
                expressions.Add(new ExpressionTexPath { expression = expression, texPath = texPath });

            // 使缓存失效，下次 GetTexPath 时重建
            InvalidateLookup();
        }

        /// <summary>是否配置了任何表情贴图</summary>
        public bool HasAnyExpression()
            => expressions.Exists(e => !string.IsNullOrEmpty(e.texPath));

        public PawnFaceConfig Clone()
        {
            var clone = new PawnFaceConfig { enabled = this.enabled };
            foreach (var exp in this.expressions)
            {
                clone.expressions.Add(new ExpressionTexPath
                {
                    expression = exp.expression,
                    texPath    = exp.texPath
                });
            }
            // clone 的 _lookupCache 保持 null，首次使用时懒初始化
            return clone;
        }
    }
}
