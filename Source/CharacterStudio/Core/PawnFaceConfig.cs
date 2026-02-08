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
        Sleeping,   // 睡眠 (闭眼)
        Blink,      // 眨眼
        Dead        // 死亡
    }

    /// <summary>
    /// 面部组件类型
    /// </summary>
    public enum FaceComponentType
    {
        Eyes,
        Mouth,
        Brows
    }

    /// <summary>
    /// 单个面部组件的表情映射
    /// </summary>
    public class FaceComponentMapping
    {
        public FaceComponentType type;
        public List<ExpressionTexPath> expressions = new List<ExpressionTexPath>();

        public string GetTextureFor(ExpressionType expression)
        {
            var match = expressions.Find(e => e.expression == expression);
            return match?.texPath ?? "";
        }
    }

    public class ExpressionTexPath
    {
        public ExpressionType expression;
        public string texPath = "";
    }

    /// <summary>
    /// 完整的面部表情配置
    /// </summary>
    public class PawnFaceConfig
    {
        public bool enabled = false;
        public List<FaceComponentMapping> components = new List<FaceComponentMapping>();

        public PawnFaceConfig()
        {
            // 初始化默认组件
            foreach (FaceComponentType type in Enum.GetValues(typeof(FaceComponentType)))
            {
                components.Add(new FaceComponentMapping { type = type });
            }
        }

        public string GetTexPath(FaceComponentType component, ExpressionType expression)
        {
            return components.Find(c => c.type == component)?.GetTextureFor(expression) ?? "";
        }

        public PawnFaceConfig Clone()
        {
            var clone = new PawnFaceConfig { enabled = this.enabled };
            clone.components.Clear();
            foreach (var comp in this.components)
            {
                var compClone = new FaceComponentMapping { type = comp.type };
                foreach (var exp in comp.expressions)
                {
                    compClone.expressions.Add(new ExpressionTexPath { expression = exp.expression, texPath = exp.texPath });
                }
                clone.components.Add(compClone);
            }
            return clone;
        }
    }
}