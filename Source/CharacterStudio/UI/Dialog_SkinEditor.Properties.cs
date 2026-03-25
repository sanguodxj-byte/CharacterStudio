using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using CharacterStudio.Introspection;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        // ─────────────────────────────────────────────
        // 属性面板
        // ─────────────────────────────────────────────

        private float GetPropertiesContentTop(Rect rect)
        {
            return rect.y + Margin + ButtonHeight + Margin;
        }

        private void DrawPropertiesPanel(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            DrawPropertiesHeader(rect);
            DrawActivePropertiesContent(rect);
        }


    }
}