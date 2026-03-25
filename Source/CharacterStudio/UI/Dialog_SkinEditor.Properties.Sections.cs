using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        /// <summary>
        /// 绘制带方向高亮的章节标题
        /// </summary>
        private void DrawDirectionalSectionTitle(ref float y, float width, string title, bool isActive)
        {
            if (isActive)
            {
                Rect bgRect = new Rect(0, y, width, 20);
                Widgets.DrawBoxSolid(bgRect, new Color(0.2f, 0.4f, 0.6f, 0.3f));

                GUI.color = new Color(0.4f, 0.8f, 1f);
                Widgets.Label(new Rect(0, y, 16, 18), "▶");
                GUI.color = Color.white;

                Text.Font = GameFont.Small;
                GUI.color = new Color(0.6f, 0.9f, 1f);
                Widgets.Label(new Rect(16, y, width - 16, 18), title);
                GUI.color = Color.white;
            }
            else
            {
                UIHelper.DrawSectionTitle(ref y, width, title);
                return;
            }

            y += 22;
        }

        /// <summary>
        /// 绘制可折叠的属性区域
        /// </summary>
        private bool DrawCollapsibleSection(ref float y, float width, string title, string sectionKey, bool highlight = false)
        {
            bool isCollapsed = collapsedSections.Contains(sectionKey);

            y += 5f;
            Rect rect = new Rect(0, y, width, 24f);

            if (highlight)
            {
                Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.4f, 0.6f, 0.3f));
            }
            else
            {
                Widgets.DrawLightHighlight(rect);
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = highlight ? new Color(0.6f, 0.9f, 1f) : UIHelper.HeaderColor;

            string icon = isCollapsed ? "▶" : "▼";
            string displayTitle = $"{icon} {title}";

            if (Widgets.ButtonInvisible(rect))
            {
                if (isCollapsed)
                {
                    collapsedSections.Remove(sectionKey);
                }
                else
                {
                    collapsedSections.Add(sectionKey);
                }
            }

            Widgets.Label(rect, displayTitle);

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            y += 28f;

            return !isCollapsed;
        }
    }
}
