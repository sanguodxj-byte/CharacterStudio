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
        // 简单的颜色选择器对话框
        public class Dialog_SimpleColorPicker : Window
        {
            private Color color;
            private Action<Color> onColorChanged;
            
            public override Vector2 InitialSize => new Vector2(300, 200);
            
            public Dialog_SimpleColorPicker(Color initial, Action<Color> callback)
            {
                color = initial;
                onColorChanged = callback;
                doCloseX = true;
                draggable = true;
            }
            
            public override void DoWindowContents(Rect inRect)
            {
                float y = 0;
                float width = inRect.width;
                
                // 预览
                Widgets.DrawBoxSolid(new Rect(0, y, width, 30), color);
                y += 40;
                
                // RGB Sliders
                float r = color.r;
                DrawSlider(ref y, width, "R", ref r);
                if (Math.Abs(r - color.r) > 0.001f) { color.r = r; onColorChanged(color); }
                
                float g = color.g;
                DrawSlider(ref y, width, "G", ref g);
                if (Math.Abs(g - color.g) > 0.001f) { color.g = g; onColorChanged(color); }
                
                float b = color.b;
                DrawSlider(ref y, width, "B", ref b);
                if (Math.Abs(b - color.b) > 0.001f) { color.b = b; onColorChanged(color); }
                
                float a = color.a;
                DrawSlider(ref y, width, "A", ref a);
                if (Math.Abs(a - color.a) > 0.001f) { color.a = a; onColorChanged(color); }
            }
            
            private void DrawSlider(ref float y, float width, string label, ref float val)
            {
                Widgets.Label(new Rect(0, y, 20, 24), label);
                val = Widgets.HorizontalSlider(new Rect(25, y + 6, width - 60, 16), val, 0f, 1f);
                Widgets.Label(new Rect(width - 30, y, 30, 24), val.ToString("F2"));
                y += 30;
            }
        }

        public const float RowHeight = 28f;
        public const float VerticalPadding = 4f;
        public const float LabelWidth = 100f;

        private static int sliderControlId;

        // 颜色常量
        public static readonly Color HeaderColor = new Color(0.78f, 0.86f, 1f);
        public static readonly Color SubtleColor = new Color(0.72f, 0.75f, 0.82f);
        public static readonly Color BorderColor = new Color(0.20f, 0.24f, 0.32f, 0.95f);
        public static readonly Color AlternatingRowColor = new Color(0.32f, 0.38f, 0.52f, 0.08f);
        public static readonly Color PanelFillColor = new Color(0.10f, 0.12f, 0.17f, 0.92f);
        public static readonly Color PanelFillSoftColor = new Color(0.15f, 0.18f, 0.25f, 0.82f);
        public static readonly Color AccentColor = new Color(0.37f, 0.62f, 1f, 1f);
        public static readonly Color AccentSoftColor = new Color(0.37f, 0.62f, 1f, 0.18f);
        public static readonly Color ActiveTabColor = new Color(0.24f, 0.31f, 0.46f, 0.95f);
        public static readonly Color HoverOutlineColor = new Color(0.70f, 0.82f, 1f, 0.35f);

        /// <summary>
        /// 绘制带有标题和分隔线的章节标题
        /// </summary>
        public static void DrawSectionTitle(ref float y, float width, string title)
        {
            y += 6f;
            Rect rect = new Rect(0, y, width, 26f);
            Widgets.DrawBoxSolid(rect, PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), AccentSoftColor);
            Widgets.DrawHighlightIfMouseover(rect);

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = HeaderColor;
            Widgets.Label(new Rect(rect.x + 8f, rect.y, rect.width - 16f, rect.height), title);
            GUI.color = AccentColor;
            Widgets.Label(new Rect(rect.x + rect.width - 44f, rect.y, 36f, rect.height), "◆");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            y += 30f;
        }

        public static bool DrawBrowseButton(Rect rect, Action onClick, string label = "…")
        {
            if (DrawSelectionButton(ExpandClickableRect(rect, 3f), label))
            {
                onClick?.Invoke();
                return true;
            }

            return false;
        }

        public static bool DrawDangerButton(Rect rect, string label = "×", string? tooltip = null, Action? onClick = null)
        {
            Rect actualRect = ExpandClickableRect(rect, 4f);
            Widgets.DrawBoxSolid(actualRect, new Color(0.42f, 0.12f, 0.12f, 0.90f));
            Widgets.DrawBoxSolid(new Rect(actualRect.x, actualRect.yMax - 2f, actualRect.width, 2f), new Color(1f, 0.35f, 0.35f, 0.85f));
            GUI.color = Mouse.IsOver(actualRect) ? new Color(1f, 0.72f, 0.72f, 0.95f) : new Color(0.72f, 0.28f, 0.28f, 0.95f);
            Widgets.DrawBox(actualRect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(actualRect, label);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            if (!string.IsNullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(actualRect, tooltip);
            }

            if (Widgets.ButtonInvisible(actualRect))
            {
                onClick?.Invoke();
                return true;
            }

            return false;
        }

        public static Rect ExpandClickableRect(Rect rect, float padding)
        {
            return new Rect(rect.x - padding, rect.y - padding, rect.width + padding * 2f, rect.height + padding * 2f);
        }

        /// <summary>
        /// 绘制交替行背景
        /// </summary>
        public static void DrawAlternatingRowBackground(Rect rect, int index)
        {
            Widgets.DrawBoxSolid(rect, index % 2 == 0 ? PanelFillColor : AlternatingRowColor);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(1f, 1f, 1f, 0.035f));
        }
        /// <summary>
        /// 绘制属性标签（只读）
        /// </summary>
        public static void DrawPropertyLabel(ref float y, float width, string label, string value, float labelWidth = LabelWidth)
        {
            Rect rect = new Rect(0, y, width, RowHeight);
            
            Text.Font = GameFont.Small;
            float actualLabelWidth = Mathf.Max(labelWidth, Text.CalcSize(label + ":").x + 10f);
            
            Widgets.Label(new Rect(rect.x, rect.y, actualLabelWidth, 24), label + ":");
            Widgets.Label(new Rect(rect.x + actualLabelWidth, rect.y, rect.width - actualLabelWidth, 24), value ?? "");
            y += RowHeight;
        }

        /// <summary>
        /// 绘制标准文本输入字段
        /// </summary>
        public static void DrawPropertyField(ref float y, float width, string label, ref string value, string? tooltip = null, float labelWidth = LabelWidth)
        {
            Rect rect = new Rect(0, y, width, RowHeight);
            
            // 计算标签实际宽度并适当调整，避免重叠
            Text.Font = GameFont.Small;
            float actualLabelWidth = Mathf.Max(labelWidth, Text.CalcSize(label).x + 10f);
            
            Widgets.Label(new Rect(rect.x, rect.y, actualLabelWidth, 24), label);
            
            string newValue = Widgets.TextField(new Rect(rect.x + actualLabelWidth, rect.y, rect.width - actualLabelWidth, 24), value ?? "");
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
        public static void DrawPropertyFieldWithButton(ref float y, float width, string label, string value, Action onButtonClick, string buttonText = "…", float labelWidth = LabelWidth)
        {
            Rect rect = new Rect(0, y, width, RowHeight);
            
            Text.Font = GameFont.Small;
            float actualLabelWidth = Mathf.Max(labelWidth, Text.CalcSize(label).x + 10f);
            
            Widgets.Label(new Rect(rect.x, rect.y, actualLabelWidth, 24), label);
            
            Text.Font = GameFont.Small;
            float btnWidth = Mathf.Max(32f, Text.CalcSize(buttonText).x + 18f);
            float fieldWidth = rect.width - actualLabelWidth - btnWidth - 5;
            
            string displayValue = string.IsNullOrWhiteSpace(value)
                ? "CS_Studio_None".Translate().ToString()
                : value;
            Widgets.Label(new Rect(rect.x + actualLabelWidth, rect.y, fieldWidth, 24), displayValue);
            DrawBrowseButton(new Rect(rect.x + actualLabelWidth + fieldWidth + 5, rect.y, btnWidth, 24), onButtonClick, buttonText);

            y += RowHeight;
        }

        public static bool DrawSelectionButton(Rect rect, string label)
        {
            Widgets.DrawBoxSolid(rect, PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), AccentSoftColor);
            GUI.color = Mouse.IsOver(rect) ? HoverOutlineColor : BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = HeaderColor;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            return Widgets.ButtonInvisible(rect);
        }

        /// <summary>
        /// 绘制滑块字段（带有文本输入框以便精细调整）
        /// </summary>
        public static void DrawPropertySlider(ref float y, float width, string label, ref float value, float min, float max, string format = "F2", float labelWidth = LabelWidth)
        {
            Rect rect = new Rect(0, y, width, RowHeight);
            
            Text.Font = GameFont.Small;
            float actualLabelWidth = Mathf.Max(labelWidth, Text.CalcSize(label).x + 10f);
            
            Widgets.Label(new Rect(rect.x, rect.y, actualLabelWidth, 24), label);
            
            float inputWidth = 64f;
            float sliderWidth = rect.width - actualLabelWidth - inputWidth - 5f;
            
            float newValue = Widgets.HorizontalSlider(new Rect(rect.x + actualLabelWidth, rect.y + 4, sliderWidth, 16), value, min, max);
            newValue = Mathf.Clamp(newValue, min, max);

            string numericFormat = string.IsNullOrWhiteSpace(format) ? "F2" : format;
            int decimals = GetDecimalsFromNumericFormat(numericFormat);
            string controlName = $"CS_SliderNumeric_{sliderControlId++}_{Mathf.RoundToInt(rect.y)}_{Mathf.RoundToInt(rect.x)}";
            string buffer = value.ToString(numericFormat);
            Rect inputRect = new Rect(rect.x + actualLabelWidth + sliderWidth + 5, rect.y, inputWidth, 24);

            GUI.SetNextControlName(controlName);
            Widgets.TextFieldNumeric<float>(inputRect, ref newValue, ref buffer, min, max);

            if (GUI.GetNameOfFocusedControl() == controlName && Event.current.type == EventType.KeyDown)
            {
                float step = GetNumericStepForFormat(decimals);
                if (Event.current.shift)
                {
                    step *= 10f;
                }
                if (Event.current.control)
                {
                    step *= 0.1f;
                }

                if (Event.current.keyCode == KeyCode.UpArrow)
                {
                    newValue = QuantizeFloat(Mathf.Clamp(newValue + step, min, max), decimals);
                    Event.current.Use();
                }
                else if (Event.current.keyCode == KeyCode.DownArrow)
                {
                    newValue = QuantizeFloat(Mathf.Clamp(newValue - step, min, max), decimals);
                    Event.current.Use();
                }
            }

            newValue = Mathf.Clamp(newValue, min, max);

            if (Math.Abs(newValue - value) > 0.0001f)
            {
                value = newValue;
            }

            y += RowHeight;
        }

        private static int GetDecimalsFromNumericFormat(string format)
        {
            if (string.IsNullOrWhiteSpace(format) || format.Length < 2)
            {
                return 2;
            }

            char prefix = char.ToUpperInvariant(format[0]);
            if (prefix != 'F' && prefix != 'N')
            {
                return 2;
            }

            if (int.TryParse(format.Substring(1), out int decimals))
            {
                return Mathf.Clamp(decimals, 0, 6);
            }

            return 2;
        }

        private static float GetNumericStepForFormat(int decimals)
        {
            decimals = Mathf.Clamp(decimals, 0, 6);
            return Mathf.Pow(10f, -decimals);
        }

        private static float QuantizeFloat(float value, int decimals)
        {
            float multiplier = Mathf.Pow(10f, Mathf.Clamp(decimals, 0, 6));
            return Mathf.Round(value * multiplier) / multiplier;
        }

        /// <summary>
        /// 绘制数值输入字段
        /// </summary>
        public static void DrawNumericField<T>(ref float y, float width, string label, ref T value, float min = 0, float max = 1000000, float labelWidth = LabelWidth) where T : struct
        {
            Rect rect = new Rect(0, y, width, RowHeight);
            
            Text.Font = GameFont.Small;
            float actualLabelWidth = Mathf.Max(labelWidth, Text.CalcSize(label).x + 10f);
            
            Widgets.Label(new Rect(rect.x, rect.y, actualLabelWidth, 24), label);
            
            string buffer = value.ToString();
            Widgets.TextFieldNumeric<T>(new Rect(rect.x + actualLabelWidth, rect.y, rect.width - actualLabelWidth, 24), ref value, ref buffer, min, max);
            
            y += RowHeight;
        }

        /// <summary>
        /// 绘制下拉选择框
        /// </summary>
        public static void DrawPropertyDropdown<T>(ref float y, float width, string label, T currentValue, IEnumerable<T> options, Func<T, string> labelMaker, Action<T> onSelect, float labelWidth = LabelWidth)
        {
            Rect rect = new Rect(0, y, width, RowHeight);
            
            Text.Font = GameFont.Small;
            float actualLabelWidth = Mathf.Max(labelWidth, Text.CalcSize(label).x + 10f);
            
            Widgets.Label(new Rect(rect.x, rect.y, actualLabelWidth, 24), label);
            
            if (DrawSelectionButton(new Rect(rect.x + actualLabelWidth, rect.y, rect.width - actualLabelWidth, 24), labelMaker(currentValue)))
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
        /// 绘制颜色选择字段
        /// </summary>
        public static void DrawPropertyColor(ref float y, float width, string label, Color value, Action<Color> onColorChanged, float labelWidth = LabelWidth)
        {
            Rect rect = new Rect(0, y, width, RowHeight);
            
            Text.Font = GameFont.Small;
            float actualLabelWidth = Mathf.Max(labelWidth, Text.CalcSize(label).x + 10f);
            
            Widgets.Label(new Rect(rect.x, rect.y, actualLabelWidth, 24), label);
            
            Rect colorRect = new Rect(rect.x + actualLabelWidth, rect.y + 2, 40, 20);
            
            // 绘制当前颜色
            Widgets.DrawBoxSolid(colorRect, value);
            Widgets.DrawBox(colorRect, 1);
            
            // 点击打开颜色选择器
            if (Widgets.ButtonInvisible(colorRect))
            {
                Find.WindowStack.Add(new Dialog_SimpleColorPicker(value, onColorChanged));
            }
            
            // 显示 RGB 文本
            string colorText = "CS_Studio_UI_ColorValueRGB".Translate(value.r.ToString("F2"), value.g.ToString("F2"), value.b.ToString("F2"));
            Widgets.Label(new Rect(colorRect.xMax + 10, rect.y, width - colorRect.xMax - 10, 24), colorText);

            y += RowHeight;
        }

        /// <summary>
        /// 绘制复选框行
        /// </summary>
        public static void DrawPropertyCheckbox(ref float y, float width, string label, ref bool value, string? tooltip = null, float labelWidth = LabelWidth)
        {
            Rect rect = new Rect(0, y, width, RowHeight);
            
            Text.Font = GameFont.Small;
            float actualLabelWidth = Mathf.Max(labelWidth, Text.CalcSize(label).x + 10f);
            Rect toggleRect = new Rect(rect.x + actualLabelWidth, rect.y, Mathf.Max(44f, rect.width - actualLabelWidth), 24f);

            Widgets.Label(new Rect(rect.x, rect.y, actualLabelWidth, 24), label);
            Widgets.DrawBoxSolid(toggleRect, value ? ActiveTabColor : PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(toggleRect.x, toggleRect.yMax - 2f, toggleRect.width, 2f), value ? AccentColor : AccentSoftColor);
            GUI.color = Mouse.IsOver(toggleRect) ? HoverOutlineColor : BorderColor;
            Widgets.DrawBox(toggleRect, 1);
            GUI.color = Color.white;

            Rect checkboxRect = new Rect(toggleRect.x + 6f, toggleRect.y + 2f, 20f, 20f);
            Widgets.Checkbox(new Vector2(checkboxRect.x, checkboxRect.y), ref value, 20f, false);
            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            GUI.color = value ? HeaderColor : SubtleColor;
            Widgets.Label(new Rect(toggleRect.x + 30f, toggleRect.y + 1f, toggleRect.width - 34f, toggleRect.height - 2f), value ? "CS_Studio_UI_On".Translate() : "CS_Studio_UI_Off".Translate());
            GUI.color = Color.white;
            Text.Font = oldFont;

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
            Color fill = isActive ? ActiveTabColor : PanelFillSoftColor;
            Widgets.DrawBoxSolid(rect, fill);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), isActive ? AccentColor : new Color(1f, 1f, 1f, 0.05f));

            if (Mouse.IsOver(rect))
            {
                Widgets.DrawBoxSolid(rect, isActive ? new Color(1f, 1f, 1f, 0.03f) : new Color(1f, 1f, 1f, 0.05f));
                GUI.color = HoverOutlineColor;
                Widgets.DrawBox(rect, 1);
                GUI.color = Color.white;
            }
            else if (isActive)
            {
                GUI.color = AccentSoftColor;
                Widgets.DrawBox(rect, 1);
                GUI.color = Color.white;
            }

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = isActive ? Color.white : SubtleColor;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            return Widgets.ButtonInvisible(rect) && !isActive;
        }
    }
}
