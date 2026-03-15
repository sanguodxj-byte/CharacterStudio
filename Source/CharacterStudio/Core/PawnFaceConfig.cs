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
        Scared,     // 恐惧
        Pain,       // 痛苦
        Shock,      // 震惊
        Tired,      // 疲劳
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
    /// 用法示例：
    ///   Neutral  -> head_neutral.png  （平静，含正常五官）
    ///   Sleeping -> head_sleeping.png （睡眠，含闭眼）
    ///   Dead     -> head_dead.png     （死亡状态）
    ///   其余未配置表情 -> 自动回退到 Neutral
    /// </summary>
    public class PawnFaceConfig
    {
        public bool enabled = false;

        /// <summary>各表情对应的头部贴图路径列表</summary>
        public List<ExpressionTexPath> expressions = new List<ExpressionTexPath>();

        /// <summary>
        /// 获取指定表情的贴图路径
        /// 未找到时回退到 Neutral；Neutral 也未配置则返回空字符串（原版渲染保持不变）
        /// </summary>
        public string GetTexPath(ExpressionType expression)
        {
            var match = expressions.Find(e => e.expression == expression);
            if (match != null && !string.IsNullOrEmpty(match.texPath))
                return match.texPath;

            // 回退到 Neutral
            if (expression != ExpressionType.Neutral)
            {
                var neutral = expressions.Find(e => e.expression == ExpressionType.Neutral);
                if (neutral != null && !string.IsNullOrEmpty(neutral.texPath))
                    return neutral.texPath;
            }

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
        }

        /// <summary>是否配置了任何表情贴图</summary>
        public bool HasAnyExpression()
        {
            return expressions.Exists(e => !string.IsNullOrEmpty(e.texPath));
        }

        public PawnFaceConfig Clone()
        {
            var clone = new PawnFaceConfig { enabled = this.enabled };
            foreach (var exp in this.expressions)
            {
                clone.expressions.Add(new ExpressionTexPath
                {
                    expression = exp.expression,
                    texPath = exp.texPath
                });
            }
            return clone;
        }
    }
}
