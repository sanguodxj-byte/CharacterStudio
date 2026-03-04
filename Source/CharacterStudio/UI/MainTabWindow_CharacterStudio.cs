using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 主界面底部按钮对应的主标签窗口。
    /// 该窗口作为启动器，负责打开皮肤编辑器并引导用户操作。
    /// </summary>
    public class MainTabWindow_CharacterStudio : MainTabWindow
    {
        private const float ButtonWidth = 260f;
        private const float ButtonHeight = 40f;

        public override Vector2 RequestedTabSize => new Vector2(700f, 260f);

        public override void PreOpen()
        {
            base.PreOpen();
            // 每次从主按钮进入时，直接拉起编辑器，保持与旧入口一致的单击体验。
            Find.WindowStack.Add(new Dialog_SkinEditor());
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 40f), "CS_Studio_EditorTitle".Translate());
            Text.Font = GameFont.Small;

            var descRect = new Rect(inRect.x, inRect.y + 48f, inRect.width, 60f);
            Widgets.Label(descRect, "CS_Studio_EditorSubtitle".Translate());

            var btnRect = new Rect(
                inRect.center.x - ButtonWidth * 0.5f,
                inRect.yMax - ButtonHeight - 16f,
                ButtonWidth,
                ButtonHeight);

            if (Widgets.ButtonText(btnRect, "CS_Studio_OpenEditor".Translate()))
            {
                Find.WindowStack.Add(new Dialog_SkinEditor());
            }
        }
    }
}
