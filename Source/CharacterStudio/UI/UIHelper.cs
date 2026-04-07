using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
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

        private static readonly Dictionary<string, string> NumericTextBuffers = new Dictionary<string, string>();
        private const int MaxNumericTextBufferCount = 4096;

        public static void TextFieldNumeric(Rect rect, ref float value, ref string buffer, float min = float.MinValue, float max = float.MaxValue, string? format = null, string? controlKey = null, [CallerMemberName] string callerMemberName = "")
        {
            string key = BuildNumericBufferKey(rect, callerMemberName, controlKey);
            string controlName = $"CS_PermissiveNumeric_{key}";
            bool isFocused = GUI.GetNameOfFocusedControl() == controlName;

            string currentText = string.IsNullOrEmpty(buffer)
                ? FormatFloatForDisplay(value, format, null)
                : buffer;

            if (isFocused)
            {
                currentText = GetOrCreateNumericTextBuffer(key, currentText);
            }
            else
            {
                NumericTextBuffers[key] = currentText;
            }

            GUI.SetNextControlName(controlName);
            string editedText = Widgets.TextField(rect, currentText);
            editedText = SanitizeNumericText(editedText);
            NumericTextBuffers[key] = editedText;

            buffer = editedText;

            if (TryParseFloatText(editedText, out float parsedValue))
            {
                value = parsedValue;
            }

            if (GUI.GetNameOfFocusedControl() != controlName)
            {
                string syncedText = FormatFloatForDisplay(value, format, buffer);
                buffer = syncedText;
                NumericTextBuffers[key] = syncedText;
            }

            TrimNumericTextBufferCacheIfNeeded();
        }

        public static void TextFieldNumeric(Rect rect, ref int value, ref string buffer, int min = int.MinValue, int max = int.MaxValue, string? controlKey = null, [CallerMemberName] string callerMemberName = "")
        {
            string key = BuildNumericBufferKey(rect, callerMemberName, controlKey);
            string controlName = $"CS_PermissiveNumeric_{key}";
            bool isFocused = GUI.GetNameOfFocusedControl() == controlName;

            string currentText = string.IsNullOrEmpty(buffer)
                ? value.ToString(CultureInfo.InvariantCulture)
                : buffer;

            if (isFocused)
            {
                currentText = GetOrCreateNumericTextBuffer(key, currentText);
            }
            else
            {
                NumericTextBuffers[key] = currentText;
            }

            GUI.SetNextControlName(controlName);
            string editedText = Widgets.TextField(rect, currentText);
            editedText = SanitizeNumericText(editedText);
            NumericTextBuffers[key] = editedText;

            buffer = editedText;

            if (TryParseIntText(editedText, out int parsedValue))
            {
                value = parsedValue;
            }

            if (GUI.GetNameOfFocusedControl() != controlName)
            {
                string syncedText = value.ToString(CultureInfo.InvariantCulture);
                buffer = syncedText;
                NumericTextBuffers[key] = syncedText;
            }

            TrimNumericTextBufferCacheIfNeeded();
        }

        public static void TextFieldNumeric(Rect rect, ref double value, ref string buffer, double min = double.MinValue, double max = double.MaxValue, string? format = null, string? controlKey = null, [CallerMemberName] string callerMemberName = "")
        {
            string key = BuildNumericBufferKey(rect, callerMemberName, controlKey);
            string controlName = $"CS_PermissiveNumeric_{key}";
            bool isFocused = GUI.GetNameOfFocusedControl() == controlName;

            string currentText = string.IsNullOrEmpty(buffer)
                ? FormatDoubleForDisplay(value, format, null)
                : buffer;

            if (isFocused)
            {
                currentText = GetOrCreateNumericTextBuffer(key, currentText);
            }
            else
            {
                NumericTextBuffers[key] = currentText;
            }

            GUI.SetNextControlName(controlName);
            string editedText = Widgets.TextField(rect, currentText);
            editedText = SanitizeNumericText(editedText);
            NumericTextBuffers[key] = editedText;

            buffer = editedText;

            if (TryParseDoubleText(editedText, out double parsedValue))
            {
                value = parsedValue;
            }

            if (GUI.GetNameOfFocusedControl() != controlName)
            {
                string syncedText = FormatDoubleForDisplay(value, format, buffer);
                buffer = syncedText;
                NumericTextBuffers[key] = syncedText;
            }

            TrimNumericTextBufferCacheIfNeeded();
        }

        private static string BuildNumericBufferKey(Rect rect, string callerMemberName, string? controlKey)
        {
            string sanitizedControlKey;
            if (string.IsNullOrWhiteSpace(controlKey))
            {
                sanitizedControlKey = string.Empty;
            }
            else
            {
                string nonNullControlKey = controlKey ?? string.Empty;
                sanitizedControlKey = "_" + nonNullControlKey.Replace(' ', '_').Replace(':', '_');
            }

            return $"{callerMemberName}{sanitizedControlKey}_{Mathf.RoundToInt(rect.x)}_{Mathf.RoundToInt(rect.y)}_{Mathf.RoundToInt(rect.width)}_{Mathf.RoundToInt(rect.height)}";
        }

        private static string GetOrCreateNumericTextBuffer(string key, string fallbackValue)
        {
            if (!NumericTextBuffers.TryGetValue(key, out string existingValue))
            {
                existingValue = fallbackValue ?? string.Empty;
                NumericTextBuffers[key] = existingValue;
            }

            return existingValue;
        }

        private static void TrimNumericTextBufferCacheIfNeeded()
        {
            if (NumericTextBuffers.Count > MaxNumericTextBufferCount)
            {
                NumericTextBuffers.Clear();
            }
        }

        public static void ClearNumericFieldFocusAndBuffers()
        {
            GUI.FocusControl(null);
            NumericTextBuffers.Clear();
        }

        private static string SanitizeNumericText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (char.IsDigit(c) || c == '-' || c == '+' || c == '.' || c == ',')
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        private static string NormalizeNumericText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string nonNullValue = value ?? string.Empty;
            return nonNullValue.Trim().Replace(',', '.');
        }

        private static bool TryParseFloatText(string value, out float parsedValue)
        {
            return float.TryParse(NormalizeNumericText(value), NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue);
        }

        private static bool TryParseDoubleText(string value, out double parsedValue)
        {
            return double.TryParse(NormalizeNumericText(value), NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue);
        }

        private static bool TryParseIntText(string value, out int parsedValue)
        {
            return int.TryParse(NormalizeNumericText(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue);
        }

        private static string FormatFloatForDisplay(float value, string? explicitFormat, string? previousBuffer)
        {
            if (!string.IsNullOrWhiteSpace(explicitFormat))
            {
                return value.ToString(explicitFormat, CultureInfo.InvariantCulture);
            }

            if (TryGetFractionDigits(previousBuffer, out int fractionDigits))
            {
                return value.ToString($"F{fractionDigits}", CultureInfo.InvariantCulture);
            }

            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatDoubleForDisplay(double value, string? explicitFormat, string? previousBuffer)
        {
            if (!string.IsNullOrWhiteSpace(explicitFormat))
            {
                return value.ToString(explicitFormat, CultureInfo.InvariantCulture);
            }

            if (TryGetFractionDigits(previousBuffer, out int fractionDigits))
            {
                return value.ToString($"F{fractionDigits}", CultureInfo.InvariantCulture);
            }

            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryGetFractionDigits(string? buffer, out int fractionDigits)
        {
            fractionDigits = 0;
            string normalized = NormalizeNumericText(buffer);
            int separatorIndex = normalized.IndexOf('.');
            if (separatorIndex < 0 || separatorIndex == normalized.Length - 1)
            {
                return false;
            }

            fractionDigits = Mathf.Clamp(normalized.Length - separatorIndex - 1, 0, 6);
            return true;
        }

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

        public static Rect DrawPanelShell(Rect rect, string title, float margin, float titleRightInset = 16f)
        {
            Widgets.DrawBoxSolid(rect, PanelFillColor);
            GUI.color = BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            Rect titleRect = new Rect(rect.x + margin, rect.y + margin, rect.width - margin * 2f, 26f);
            Widgets.DrawBoxSolid(titleRect, PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f), AccentSoftColor);
            GUI.color = BorderColor;
            Widgets.DrawBox(titleRect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = HeaderColor;
            Widgets.Label(new Rect(titleRect.x + 8f, titleRect.y, titleRect.width - titleRightInset, titleRect.height), title);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;
            return titleRect;
        }

        public static Rect DrawContentCard(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, PanelFillSoftColor);
            GUI.color = BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            return rect;
        }

        public static Rect DrawSectionCard(ref float y, float width, string title, float height, bool accent = false)
        {
            Rect outerRect = new Rect(0f, y, width, height);
            Widgets.DrawBoxSolid(outerRect, PanelFillColor);
            GUI.color = BorderColor;
            Widgets.DrawBox(outerRect, 1);
            GUI.color = Color.white;

            Rect titleRect = new Rect(outerRect.x + 6f, outerRect.y + 6f, outerRect.width - 12f, 22f);
            Widgets.DrawBoxSolid(titleRect, accent ? new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.14f) : PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f), accent ? AccentColor : AccentSoftColor);
            GUI.color = BorderColor;
            Widgets.DrawBox(titleRect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = HeaderColor;
            Widgets.Label(new Rect(titleRect.x + 8f, titleRect.y, titleRect.width - 16f, titleRect.height), title);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            y += height + 8f;
            return new Rect(outerRect.x + 8f, outerRect.y + 34f, outerRect.width - 16f, outerRect.height - 42f);
        }

        public static bool DrawToolbarButton(Rect rect, string label, bool accent = false, string? tooltip = null)
        {
            Widgets.DrawBoxSolid(rect, accent ? ActiveTabColor : PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), accent ? AccentColor : new Color(1f, 1f, 1f, 0.05f));
            GUI.color = Mouse.IsOver(rect) ? HoverOutlineColor : BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            GameFont oldFont = Text.Font;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = accent ? Color.white : HeaderColor;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = oldFont;

            if (!string.IsNullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(rect, tooltip);
            }

            return Widgets.ButtonInvisible(rect);
        }

        public static void DrawInfoBanner(ref float y, float width, string text, bool accent = false)
        {
            Text.Font = GameFont.Tiny;
            float textHeight = Text.CalcHeight(text, Mathf.Max(40f, width - 16f));
            float bannerHeight = Mathf.Max(34f, textHeight + 12f);
            Rect bannerRect = new Rect(0f, y, width, bannerHeight);
            Widgets.DrawBoxSolid(
                bannerRect,
                accent
                    ? new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.12f)
                    : PanelFillSoftColor);
            GUI.color = BorderColor;
            Widgets.DrawBox(bannerRect, 1);
            GUI.color = accent ? HeaderColor : SubtleColor;
            Widgets.Label(new Rect(bannerRect.x + 8f, bannerRect.y + 4f, bannerRect.width - 16f, bannerRect.height - 8f), text);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += bannerHeight + 6f;
        }

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

            string numericFormat = string.IsNullOrWhiteSpace(format) ? "F2" : format;
            int decimals = GetDecimalsFromNumericFormat(numericFormat);

            float sliderValueBefore = Mathf.Clamp(value, min, max);
            float sliderValue = Widgets.HorizontalSlider(new Rect(rect.x + actualLabelWidth, rect.y + 4, sliderWidth, 16), sliderValueBefore, min, max);
            if (Math.Abs(sliderValue - sliderValueBefore) > 0.0001f)
            {
                value = QuantizeFloat(sliderValue, decimals);
            }

            string buffer = value.ToString(numericFormat, CultureInfo.InvariantCulture);
            Rect inputRect = new Rect(rect.x + actualLabelWidth + sliderWidth + 5, rect.y, inputWidth, 24);

            float numericValueBefore = value;
            TextFieldNumeric(inputRect, ref value, ref buffer, min, max, numericFormat, label);
            if (Math.Abs(value - numericValueBefore) > 0.0001f)
            {
                value = QuantizeFloat(value, decimals);
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

            Rect inputRect = new Rect(rect.x + actualLabelWidth, rect.y, rect.width - actualLabelWidth, 24);
            if (typeof(T) == typeof(int))
            {
                int intValue = (int)(object)value;
                string buffer = intValue.ToString(CultureInfo.InvariantCulture);
                TextFieldNumeric(inputRect, ref intValue, ref buffer, Mathf.RoundToInt(min), Mathf.RoundToInt(max), label);
                value = (T)(object)intValue;
            }
            else if (typeof(T) == typeof(float))
            {
                float floatValue = (float)(object)value;
                string buffer = floatValue.ToString(CultureInfo.InvariantCulture);
                TextFieldNumeric(inputRect, ref floatValue, ref buffer, min, max, null, label);
                value = (T)(object)floatValue;
            }
            else if (typeof(T) == typeof(double))
            {
                double doubleValue = (double)(object)value;
                string buffer = doubleValue.ToString(CultureInfo.InvariantCulture);
                TextFieldNumeric(inputRect, ref doubleValue, ref buffer, min, max, null, label);
                value = (T)(object)doubleValue;
            }
            else
            {
                string buffer = value.ToString();
                string edited = Widgets.TextField(inputRect, buffer ?? string.Empty);
                if (TryConvertNumericValue(edited, out T convertedValue))
                {
                    value = convertedValue;
                }
            }

            y += RowHeight;
        }

        private static bool TryConvertNumericValue<T>(string input, out T convertedValue) where T : struct
        {
            try
            {
                object boxedValue = Convert.ChangeType(NormalizeNumericText(input), typeof(T), CultureInfo.InvariantCulture);
                if (boxedValue is T typedValue)
                {
                    convertedValue = typedValue;
                    return true;
                }
            }
            catch
            {
            }

            convertedValue = default;
            return false;
        }

        /// <summary>
        /// 绘制下拉选择框
        /// </summary>
        public static void DrawPropertyDropdown<T>(ref float y, float width, string label, T currentValue, IEnumerable<T> options, Func<T, string> labelMaker, Action<T> onSelect, float labelWidth = LabelWidth, string? tooltip = null)
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

            if (!string.IsNullOrEmpty(tooltip))
            {
                TooltipHandler.TipRegion(rect, tooltip);
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
