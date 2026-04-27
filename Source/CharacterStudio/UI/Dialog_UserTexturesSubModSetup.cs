using System;
using CharacterStudio.Exporter;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public sealed class Dialog_UserTexturesSubModSetup : Window
    {
        private readonly Action? onConfirmed;

        public override Vector2 InitialSize => new Vector2(680f, 340f);

        public Dialog_UserTexturesSubModSetup(Action? onConfirmed = null)
        {
            this.onConfirmed = onConfirmed;
            doCloseX = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            UIHelper.DrawDialogFrame(inRect, this);

            Rect shellRect = new Rect(0f, 0f, inRect.width, inRect.height);
            Widgets.DrawBoxSolid(shellRect, UIHelper.PanelFillColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(shellRect, 1);
            GUI.color = Color.white;

            float y = 8f;
            float width = inRect.width;
            float contentWidth = width - 24f;
            float contentX = 12f;

            UIHelper.DrawSectionTitle(ref y, width, "用户纹理子Mod设置");

            float btnWidth = 300f;
            float btnHeight = 42f;
            float btnY = inRect.height - btnHeight - 12f;
            float infoTop = y + 4f;
            float infoBottom = btnY - 12f;
            float infoHeight = Mathf.Max(120f, infoBottom - infoTop);

            string targetPath = TextureInternalizer.GetUserTexturesModPath();
            string description = "首次使用建议创建专用用户纹理子Mod。\n\n创建后请将贴图放入以下目录：\n" +
                                 targetPath + "\\Textures\n\n" +
                                 "当前代码路径处理整体可接受中文目录/文件名，但为了兼容运行时资源加载，建议命名保持稳定、避免过度特殊字符。\n\n" +
                                 "如果测试时出现纹理消失，或 FPS/TPS 显著下降，通常说明当前仍在使用外部纹理路径进行运行时加载。此时建议将纹理整理到用户子Mod目录后重新启动游戏，让贴图进入游戏管理内存，再继续测试。";
            Rect infoRect = new Rect(contentX, infoTop, contentWidth, infoHeight);
            Widgets.DrawBoxSolid(infoRect, UIHelper.AccentSoftColor);
            GUI.color = UIHelper.BorderColor;
            Widgets.DrawBox(infoRect, 1);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Widgets.Label(infoRect.ContractedBy(10f), description);

            float startX = (inRect.width - btnWidth) / 2f;

            if (UIHelper.DrawToolbarButton(new Rect(startX, btnY, btnWidth, btnHeight), "创建用户纹理子Mod", accent: true))
            {
                try
                {
                    TextureInternalizer.EnsureUserTexturesSubModExists();
                    Messages.Message("已创建用户纹理子Mod，请将贴图放入其 Textures 目录后再进行编辑。", MessageTypeDefOf.PositiveEvent, false);
                    onConfirmed?.Invoke();
                    Close();
                }
                catch (Exception ex)
                {
                    Log.Error($"[CharacterStudio] 创建用户纹理子Mod失败: {ex}");
                    Messages.Message("创建用户纹理子Mod失败，请检查日志。", MessageTypeDefOf.RejectInput, false);
                }
            }
        }
    }
}
