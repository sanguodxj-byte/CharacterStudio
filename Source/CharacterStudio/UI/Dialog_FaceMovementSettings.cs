using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace CharacterStudio.UI
{
    public class Dialog_FaceMovementSettings : Window
    {
        private readonly Dialog_SkinEditor owner;
        private Vector2 scrollPos;
        private float viewHeight = 1200f;
        private readonly HashSet<string> expandedSections = new HashSet<string>
        {
            "GlobalAmplitude",
            "UpperLid",
            "LowerLid"
        };

        public override Vector2 InitialSize => new Vector2(540f, 700f);

        public Dialog_FaceMovementSettings(Dialog_SkinEditor owner)
        {
            this.owner = owner;
            doCloseX = true;
            doCloseButton = false;
            draggable = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            forcePause = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect titleRect = new Rect(0f, 0f, inRect.width, 30f);
            Widgets.DrawBoxSolid(titleRect, UIHelper.PanelFillSoftColor);
            Widgets.DrawBoxSolid(new Rect(titleRect.x, titleRect.yMax - 2f, titleRect.width, 2f), UIHelper.AccentSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(titleRect, 1);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = UIHelper.HeaderColor;
            Widgets.Label(new Rect(titleRect.x + 8f, titleRect.y, titleRect.width - 16f, titleRect.height), "CS_Studio_Face_MovementDialog_Title".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            float contentY = titleRect.yMax + 8f;
            Rect contentRect = new Rect(0f, contentY, inRect.width, inRect.height - contentY - 42f);
            Widgets.DrawBoxSolid(contentRect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(contentRect, 1);
            GUI.color = Color.white;

            Rect viewRect = new Rect(0f, 0f, contentRect.width - 20f, viewHeight);
            Widgets.BeginScrollView(contentRect.ContractedBy(2f), ref scrollPos, viewRect);

            float y = 0f;
            float width = viewRect.width;
            owner.DrawFaceRuntimeTuningDialogContents(ref y, width, IsSectionExpanded, ToggleSectionExpanded);

            viewHeight = Mathf.Max(y + 12f, contentRect.height - 4f);
            Widgets.EndScrollView();

            float btnWidth = 100f;
            float btnY = inRect.height - 34f;
            if (Widgets.ButtonText(new Rect(inRect.width / 2f - btnWidth / 2f, btnY, btnWidth, 28f), "CS_Studio_Btn_OK".Translate()))
            {
                Close();
            }
        }

        private bool IsSectionExpanded(string sectionKey)
        {
            return expandedSections.Contains(sectionKey ?? string.Empty);
        }

        private void ToggleSectionExpanded(string sectionKey)
        {
            if (string.IsNullOrWhiteSpace(sectionKey))
                return;

            if (!expandedSections.Add(sectionKey))
                expandedSections.Remove(sectionKey);
        }
    }
}
