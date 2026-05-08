using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    /// <summary>
    /// 底部菜单按钮入口。
    /// 点击后直接打开皮肤编辑器，本窗口不显示任何内容。
    /// </summary>
    public class MainTabWindow_CharacterStudio : MainTabWindow
    {
        public override Vector2 RequestedTabSize => new Vector2(0f, 0f);

        public override void PreOpen()
        {
            base.PreOpen();
            Find.WindowStack.Add(new Dialog_SkinEditor());
            Close();
        }

        public override void DoWindowContents(Rect inRect)
        {
        }
    }
}
