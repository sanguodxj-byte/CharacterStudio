using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace CharacterStudio.UI
{
    /// <summary>
    /// Character Studio UI 辅助类，提供统一的样式和控件绘制
    /// </summary>
    public static class UIHelper
    {
        public const float RowHeight = 28f;
        public const float VerticalPadding = 4f;
        public const float LabelWidth = 100f;

        // 颜色常量
        public static readonly Color HeaderColor = new Color(0.7f, 0.8f, 1f);
        public static readonly Color SubtleColor = new Color(0.7f, 0.7f, 0.7f);
        public static readonly Color BorderColor = new Color(0.3f, 0.3f, 0.3f);
        public static readonly Color AlternatingRowColor = new Color(1f, 1f, 1f, 0.05f);

        /// <summary>
        /// 绘制带有标题和分隔线的章节标题
        /// </summary>
        public static void DrawSectionTitle(ref float y, float width, string title)
        {
            y += 5f;
            Rect rect = new Rect(0, y, width, 24f);
            Widgets.DrawLightHighlight(rect);
            
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = HeaderColor;
            Widgets.Label(rect, title);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            
            y += 28f;
        }

        /// <summary>
        /// 绘制交替行背景
        /// </summary>
        public static void DrawAlternatingRowBackground(Rect rect, int index)
        {
            if (index % 2 == 1)
            {
                Widgets.DrawBoxSolid(rect, AlternatingRowColor);
            }
        }
        /// <summary>
        /// 绘制属性标签（只读）
        /// </summary>
        public static void DrawPropertyLabel(ref float y, float width, string label, string value, float labelWidth = LabelWidth)
        {
            Rect rect = new Rect(0, y, width, RowHeight);
            Widgets.Label(new Rect(rect.x, rect.y, labelWidth, 24), label + ":");
            Widgets.Label(new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth, 24), value ?? "");
            y += RowHeight;
        }

        /// <summary>
        /// 绘制标准文本输入字段
        /// </summary>
        public static void DrawPropertyField(ref float y, float width, string label, ref string value, string? tooltip = null, float labelWidth = LabelWidth)
        {
            Rect rect = new Rect(0, y, width, RowHeight);
            Widgets.Label(new Rect(rect.x, rect.y, labelWidth, 24), label);
            
            string newValue = Widgets.TextField(new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth, 24), value ?? "");
            if (newValue != value)
            {
                value = SanitizeInput(newValue, 128);
            }

            if (!string.IsNullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(rect, tooltip);
            }

            y += RowHeight;
        }

        /// <summary>
        /// 绘制带有选择按钮的字段
        /// </summary>
        public static void DrawPropertyFieldWithButton(ref float y, float width, string label, string value, Action onButtonClick, string buttonText = "...", float labelWidth = LabelWidth)
        {
            Rect rect = new Rect(0, y, width, RowHeight);
            Widgets.Label(new Rect(rect.x, rect.y, labelWidth, 24), label);
            
            float btnWidth = 30f;
            float fieldWidth = rect.width - labelWidth - btnWidth - 5;
            
            Widgets.Label(new Rect(rect.x + labelWidth, rect.y, fieldWidth, 24), value ?? "(无)");
            if (Widgets.ButtonText(new Rect(rect.x + labelWidth + fieldWidth + 5, rect.y, btnWidth, 24), buttonText))
            {
                onButtonClick?.Invoke();
            }

            y += RowHeight;
        }

        /// <summary>
        /// 绘制滑块字段
        /// </summary>
        public static void DrawPropertySlider(ref float y, float width, string label, ref float value, float min, float max, string format = "F2", float labelWidth = LabelWidth)
        {
            Rect rect = new Rect(0, y, width, RowHeight);
            Widgets.Label(new Rect(rect.x, rect.y, labelWidth, 24), label);
            
            float sliderWidth = rect.width - labelWidth - 50;
            float newValue = Widgets.HorizontalSlider(new Rect(rect.x + labelWidth, rect.y + 4, sliderWidth, 16), value, min, max);
            Widgets.Label(new Rect(rect.x + labelWidth + sliderWidth + 5, rect.y, 45, 24), newValue.ToString(format));
            
            if (Math.Abs(newValue - value) > 0.0001f)
            {
                value = newValue;
            }

            y += RowHeight;
        }

        /// <summary>
        /// 绘制数值输入字段
        /// </summary>
        public static void DrawNumericField<T>(ref float y, float width, string label, ref T value, float min = 0, float max = 1000000, float labelWidth = LabelWidth) where T : struct
        {
            Rect rect = new Rect(0, y, width, RowHeight);
            Widgets.Label(new Rect(rect.x, rect.y, labelWidth, 24), label);
            
            string buffer = value.ToString();
            Widgets.TextFieldNumeric<T>(new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth, 24), ref value, ref buffer, min, max);
            
            y += RowHeight;
        }

        /// <summary>
        /// 绘制下拉选择框
        /// </summary>
        public static void DrawPropertyDropdown<T>(ref float y, float width, string label, T currentValue, IEnumerable<T> options, Func<T, string> labelMaker, Action<T> onSelect, float labelWidth = LabelWidth)
        {
            Rect rect = new Rect(0, y, width, RowHeight);
            Widgets.Label(new Rect(rect.x, rect.y, labelWidth, 24), label);
            
            if (Widgets.ButtonText(new Rect(rect.x + labelWidth, rect.y, rect.width - labelWidth, 24), labelMaker(currentValue)))
            {
                List<FloatMenuOption> menuOptions = new List<FloatMenuOption>();
                foreach (var option in options)
                {
                    T localOpt = option;
                    menuOptions.Add(new FloatMenuOption(labelMaker(localOpt), () => onSelect(localOpt)));
                }
                Find.WindowStack.Add(new FloatMenu(menuOptions));
            }

            y += RowHeight;
        }

        /// <summary>
        /// 绘制复选框行
        /// </summary>
        public static void DrawPropertyCheckbox(ref float y, float width, string label, ref bool value, string tooltip = null)
        {
            Rect rect = new Rect(0, y, width, RowHeight);
            Widgets.CheckboxLabeled(rect, label, ref value);
            
            if (!string.IsNullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(rect, tooltip);
            }
            
            y += RowHeight;
        }

        /// <summary>
        /// 文本清理
        /// </summary>
        public static string SanitizeInput(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input)) return "";
            if (input.Length > maxLength) input = input.Substring(0, maxLength);
            
            var sanitized = new System.Text.StringBuilder();
            foreach (char c in input)
            {
                if (!char.IsControl(c) || c == '\n' || c == '\r' || c == '\t')
                {
                    sanitized.Append(c);
                }
            }
            return sanitized.ToString();
        }

        /// <summary>
        /// 验证 DefName
        /// </summary>
        public static bool IsValidDefName(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return false;
            return defName.All(c => char.IsLetterOrDigit(c) || c == '_');
        }

        /// <summary>
        /// 绘制选项卡按钮
        /// </summary>
        public static bool DrawTabButton(Rect rect, string label, bool isActive)
        {
            if (isActive)
            {
                Widgets.DrawHighlight(rect);
            }

            bool clicked = Widgets.ButtonText(rect, label, drawBackground: !isActive);
            return clicked && !isActive;
        }
    }
}
