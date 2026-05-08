using System;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public class Dialog_Input : Window
    {
        private string title;
        private string inputText;
        private Action<string> onConfirm;

        public override Vector2 InitialSize => new Vector2(400f, 180f);

        public Dialog_Input(string title, string defaultText, Action<string> onConfirm)
        {
            this.title = title;
            this.inputText = defaultText ?? string.Empty;
            this.onConfirm = onConfirm;
            this.forcePause = false;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = 0f;

            Widgets.Label(new Rect(0, y, inRect.width, 24f), title);
            y += 30f;

            inputText = Widgets.TextField(new Rect(0, y, inRect.width, 28f), inputText);
            y += 40f;

            float buttonWidth = 120f;
            float gap = 10f;
            float totalWidth = buttonWidth * 2 + gap;
            float startX = (inRect.width - totalWidth) / 2f;

            if (Widgets.ButtonText(new Rect(startX, y, buttonWidth, 30f), "CS_Studio_Confirm".Translate()))
            {
                onConfirm?.Invoke(inputText);
                Close();
            }

            if (Widgets.ButtonText(new Rect(startX + buttonWidth + gap, y, buttonWidth, 30f), "CS_Studio_Cancel".Translate()))
            {
                Close();
            }
        }
    }
}
