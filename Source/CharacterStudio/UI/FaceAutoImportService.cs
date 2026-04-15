using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterStudio.Core;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 自动导入检测服务。
    /// 从文件名模式检测面部部件、表情、方向等语义信息。
    /// 从 Dialog_SkinEditor.Face.AutoImport.cs 提取，供编辑器和未来的批处理工具复用。
    /// </summary>
    public static class FaceAutoImportService
    {
        public static readonly Dictionary<string, ExpressionType> FullFaceExpressionFileAliases =
            new Dictionary<string, ExpressionType>(StringComparer.OrdinalIgnoreCase)
            {
                { "Base", ExpressionType.Neutral },
                { "Neutral", ExpressionType.Neutral },
                { "Idle", ExpressionType.Neutral },
                { "Default", ExpressionType.Neutral },
                { "Sleep", ExpressionType.Sleeping },
                { "Sleeping", ExpressionType.Sleeping },
                { "Wink", ExpressionType.Wink },
                { "Combat", ExpressionType.WaitCombat },
                { "Melee", ExpressionType.AttackMelee },
                { "Ranged", ExpressionType.AttackRanged },
            };

        public static readonly Dictionary<string, LayeredFacePartType> LayeredFacePartFileAliases =
            new Dictionary<string, LayeredFacePartType>(StringComparer.OrdinalIgnoreCase)
            {
                { "Base", LayeredFacePartType.Base },
                { "Brow", LayeredFacePartType.Brow },
                { "Brows", LayeredFacePartType.Brow },
                { "Eye", LayeredFacePartType.ReplacementEye },
                { "Eyes", LayeredFacePartType.ReplacementEye },
                { "Sclera", LayeredFacePartType.Sclera },
                { "EyeWhite", LayeredFacePartType.Sclera },
                { "EyeWhites", LayeredFacePartType.Sclera },
                { "Pupil", LayeredFacePartType.Pupil },
                { "Pupils", LayeredFacePartType.Pupil },
                { "UpperLid", LayeredFacePartType.UpperLid },
                { "LidUpper", LayeredFacePartType.UpperLid },
                { "UpperLids", LayeredFacePartType.UpperLid },
                { "LowerLid", LayeredFacePartType.LowerLid },
                { "LidLower", LayeredFacePartType.LowerLid },
                { "LowerLids", LayeredFacePartType.LowerLid },
                { "ReplacementEye", LayeredFacePartType.ReplacementEye },
                { "ReplacementEyes", LayeredFacePartType.ReplacementEye },
                { "Mouth", LayeredFacePartType.Mouth },
                { "Hair", LayeredFacePartType.Hair },
                { "OverlayTop", LayeredFacePartType.OverlayTop },
            };

        public static readonly HashSet<string> DirectionalVariantTokens =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "north", "south", "east", "west", "left", "right", "up", "down", "center",
            };

        public static readonly HashSet<string> ViewDirectionalVariantTokens =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "north", "south", "east", "west", "up", "down", "center",
            };

        /// <summary>
        /// 从文件名 token 检测表情类型。
        /// </summary>
        public static bool TryResolveExpressionType(string token, out ExpressionType expression)
        {
            return FullFaceExpressionFileAliases.TryGetValue(token ?? string.Empty, out expression);
        }

        /// <summary>
        /// 从文件名 token 检测面部部件类型。
        /// </summary>
        public static bool TryResolvePartType(string token, out LayeredFacePartType partType)
        {
            return LayeredFacePartFileAliases.TryGetValue(token ?? string.Empty, out partType);
        }

        /// <summary>
        /// 判断 token 是否为方向变体标识。
        /// </summary>
        public static bool IsDirectionalVariant(string token)
        {
            return DirectionalVariantTokens.Contains(token);
        }

        /// <summary>
        /// 判断 token 是否为视角方向标识。
        /// </summary>
        public static bool IsViewDirectionalVariant(string token)
        {
            return ViewDirectionalVariantTokens.Contains(token);
        }

        /// <summary>
        /// 将文件名拆分为语义 token 列表（去除扩展名、分隔符）。
        /// </summary>
        public static string[] TokenizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return Array.Empty<string>();
            }

            string name = Path.GetFileNameWithoutExtension(fileName) ?? fileName;
            return name.Split(new[] { '_', '-', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
