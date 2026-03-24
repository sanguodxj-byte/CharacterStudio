using System;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    internal class Dialog_AbilityXmlImport : Window
    {
        private string xmlPath;
        private readonly Action<string, bool> onImport;

        public override Vector2 InitialSize => new Vector2(720f, 220f);

        public Dialog_AbilityXmlImport(string initialPath, Action<string, bool> onImport)
        {
            this.xmlPath = initialPath ?? string.Empty;
            this.onImport = onImport;
            doCloseX = true;
            doCloseButton = false;
            draggable = true;
            resizeable = false;
            forcePause = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "CS_Studio_Ability_ImportXmlTitle".Translate());
            Text.Font = GameFont.Small;

            Widgets.Label(new Rect(0f, 38f, inRect.width, 44f), "CS_Studio_Ability_ImportXmlHint".Translate());

            Widgets.Label(new Rect(0f, 86f, 110f, 24f), "CS_Studio_Ability_ImportXmlPath".Translate());
            xmlPath = Widgets.TextField(new Rect(112f, 86f, inRect.width - 112f, 24f), xmlPath ?? string.Empty);

            float buttonY = inRect.height - 34f;
            float buttonWidth = (inRect.width - 10f) / 3f;

            if (Widgets.ButtonText(new Rect(0f, buttonY, buttonWidth, 30f), "CS_Studio_Ability_ImportReplace".Translate()))
            {
                onImport?.Invoke(xmlPath, true);
                Close();
            }

            if (Widgets.ButtonText(new Rect(buttonWidth + 5f, buttonY, buttonWidth, 30f), "CS_Studio_Ability_ImportAppend".Translate()))
            {
                onImport?.Invoke(xmlPath, false);
                Close();
            }

            if (Widgets.ButtonText(new Rect((buttonWidth + 5f) * 2f, buttonY, buttonWidth, 30f), "CS_Studio_Btn_Cancel".Translate()))
            {
                Close();
            }
        }
    }
}
