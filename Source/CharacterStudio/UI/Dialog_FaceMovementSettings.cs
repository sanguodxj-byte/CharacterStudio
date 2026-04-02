using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public class Dialog_FaceMovementSettings : Window
    {
        private readonly Dialog_SkinEditor owner;
        private Vector2 scrollPos;
        private float viewHeight = 1600f;

        public override Vector2 InitialSize => new Vector2(560f, 760f);

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
            DrawDialogSummary(ref y, width);
            owner.DrawFaceMovementDialogContents(ref y, width);
            owner.DrawFaceRuntimeTuningDialogContents(ref y, width);
            y += 2f;
            owner.DrawSelectedLayerMovementDialogContents(ref y, width);

            viewHeight = Mathf.Max(y + 12f, contentRect.height - 4f);
            Widgets.EndScrollView();

            float btnWidth = 100f;
            float btnY = inRect.height - 34f;
            if (Widgets.ButtonText(new Rect(inRect.width / 2f - btnWidth / 2f, btnY, btnWidth, 28f), "CS_Studio_Btn_OK".Translate()))
            {
                Close();
            }
        }

        private void DrawDialogSummary(ref float y, float width)
        {
            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_Face_MovementDialog_Title".Translate());

            Text.Font = GameFont.Tiny;
            float bannerHeight = Mathf.Max(42f, Text.CalcHeight("CS_Studio_Face_MovementDialog_Summary".Translate(), width - 16f) + 12f);
            Rect bannerRect = new Rect(0f, y, width, bannerHeight);
            Widgets.DrawBoxSolid(bannerRect, UIHelper.PanelFillSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(bannerRect, 1);
            GUI.color = UIHelper.SubtleColor;
            Widgets.Label(new Rect(bannerRect.x + 8f, bannerRect.y + 4f, bannerRect.width - 16f, bannerRect.height - 8f), "CS_Studio_Face_MovementDialog_Summary".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += bannerHeight + 6f;
        }
    }
}
