using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public class Dialog_CopyableText : Window
    {
        private readonly string title;
        private string text;
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(900f, 720f);

        public Dialog_CopyableText(string title, string text)
        {
            this.title = string.IsNullOrWhiteSpace(title) ? "Info" : title;
            this.text = text ?? string.Empty;

            doCloseX = true;
            doCloseButton = true;
            draggable = true;
            resizeable = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            optionalTitle = this.title;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;

            const float hintHeight = 28f;
            const float buttonHeight = 32f;
            const float gap = 10f;

            Rect hintRect = new Rect(0f, 0f, inRect.width, hintHeight);
            Widgets.Label(hintRect, "可直接在下方文本框中选中并复制内容。");

            float textAreaY = hintRect.yMax + gap;
            float textAreaHeight = inRect.height - textAreaY - gap - buttonHeight;
            Rect textAreaRect = new Rect(0f, textAreaY, inRect.width, textAreaHeight);

            Widgets.DrawBoxSolid(textAreaRect, new Color(0.13f, 0.13f, 0.13f, 1f));
            GUI.color = Color.gray;
            Widgets.DrawBox(textAreaRect, 1);
            GUI.color = Color.white;

            Rect viewRect = new Rect(0f, 0f, textAreaRect.width - 18f, Mathf.Max(textAreaRect.height - 8f, Text.CalcHeight(text, textAreaRect.width - 28f) + 12f));
            Widgets.BeginScrollView(textAreaRect, ref scrollPosition, viewRect);
            text = Widgets.TextArea(new Rect(4f, 4f, viewRect.width - 8f, viewRect.height - 8f), text);
            Widgets.EndScrollView();

            Rect closeButtonRect = new Rect(inRect.width - 120f, inRect.height - buttonHeight, 120f, buttonHeight);
            if (Widgets.ButtonText(closeButtonRect, "关闭"))
            {
                Close();
            }
        }
    }
}
