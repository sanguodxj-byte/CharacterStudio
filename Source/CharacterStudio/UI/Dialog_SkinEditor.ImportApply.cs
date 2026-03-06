using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CharacterStudio.Core;
using CharacterStudio.Exporter;
using RimWorld;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private void OnSaveSkin()
        {
            // 验证必填字段
            if (string.IsNullOrEmpty(workingSkin.defName))
            {
                ShowStatus("CS_Studio_Err_DefNameEmpty".Translate());
                return;
            }

            if (!UIHelper.IsValidDefName(workingSkin.defName))
            {
                ShowStatus("CS_Studio_Err_DefNameInvalid".Translate());
                return;
            }

            if (string.IsNullOrEmpty(workingSkin.label))
            {
                workingSkin.label = workingSkin.defName;
            }

            // 保存到配置目录下的 CharacterStudio/Skins 文件夹
            string exportDir = Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "Skins");
            string fileName = workingSkin.defName + ".xml";
            string filePath = Path.Combine(exportDir, fileName);

            try
            {
                SkinSaver.SaveSkinDef(workingSkin, filePath);

                // 注册到运行时 DefDatabase，确保无需重启即可在菜单中出现
                var registered = PawnSkinDefRegistry.RegisterOrReplace(workingSkin.Clone());
                workingSkin = registered;

                // 如有目标 Pawn，保存时立即应用并触发渲染刷新
                if (targetPawn != null)
                {
                    if (PawnSkinRuntimeUtility.ApplySkinToPawn(targetPawn, registered))
                    {
                        Messages.Message(
                            "CS_Appearance_Applied".Translate(registered.label ?? registered.defName, targetPawn.LabelShort),
                            MessageTypeDefOf.PositiveEvent,
                            false
                        );
                    }
                }

                ShowStatus("CS_Studio_Msg_SaveSuccess".Translate());
                Messages.Message($"已保存至: {filePath}", MessageTypeDefOf.PositiveEvent, false);
                isDirty = false;
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 保存失败: {ex}");
                ShowStatus("CS_Studio_Err_SaveFailed".Translate());
            }
        }

        private void OnApplyToTargetPawn()
        {
            if (targetPawn == null)
            {
                ShowStatus("请先从地图导入一个角色作为应用目标");
                return;
            }

            try
            {
                // 应用时使用克隆，避免运行时引用与编辑态对象互相污染
                var runtimeSkin = workingSkin.Clone();
                if (PawnSkinRuntimeUtility.ApplySkinToPawn(targetPawn, runtimeSkin))
                {
                    // 显式触发一次隐藏节点刷新，确保编辑器“应用”语义稳定
                    CharacterStudio.Rendering.Patch_PawnRenderTree.RefreshHiddenNodes(targetPawn);
                    RefreshRenderTree();
                    ShowStatus($"已应用到 {targetPawn.LabelShort}");
                    Messages.Message(
                        "CS_Appearance_Applied".Translate(runtimeSkin.label ?? runtimeSkin.defName, targetPawn.LabelShort),
                        MessageTypeDefOf.PositiveEvent,
                        false
                    );
                }
                else
                {
                    ShowStatus("应用失败：目标角色缺少皮肤组件");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用到目标角色失败: {ex}");
                ShowStatus("应用失败，请查看日志");
            }
        }

        private void OnImportFromMap()
        {
            // 获取地图上所有可选的 Pawn
            var map = Find.CurrentMap;
            if (map == null)
            {
                ShowStatus("CS_Studio_Err_NoMap".Translate());
                return;
            }

            var pawns = map.mapPawns.AllPawnsSpawned
                .Where(p => p.RaceProps.Humanlike && p.Drawer?.renderer?.renderTree != null)
                .ToList();

            if (pawns.Count == 0)
            {
                ShowStatus("CS_Studio_Err_NoPawns".Translate());
                return;
            }

            // 显示选择菜单
            var options = new List<FloatMenuOption>();
            foreach (var pawn in pawns)
            {
                var p = pawn; // 捕获变量
                options.Add(new FloatMenuOption(
                    $"{p.LabelShort} ({p.kindDef.label})",
                    () => DoImportFromPawn(p)
                ));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DoImportFromPawn(Pawn pawn)
        {
            var importResult = VanillaImportUtility.ImportFromPawnWithPaths(pawn);
            var layers = importResult.layers;
            var sourcePaths = importResult.sourcePaths;
            var sourceTags = importResult.sourceTags;

            bool shouldHideVanillaBody = sourceTags.Any(tag =>
                    !string.IsNullOrEmpty(tag) && tag.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0)
                || layers.Any(layer =>
                    !string.IsNullOrEmpty(layer.anchorTag) && layer.anchorTag.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0);

            bool shouldHideVanillaHead = sourceTags.Any(tag =>
                    !string.IsNullOrEmpty(tag) && tag.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
                || layers.Any(layer =>
                    !string.IsNullOrEmpty(layer.anchorTag) && layer.anchorTag.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0);

            if (layers.Count == 0)
            {
                ShowStatus("CS_Studio_Err_ImportFailed".Translate(pawn.LabelShort));
                return;
            }

            // 同步人偶的种族，确保预览使用正确的种族模型
            // 这对于 HAR 等自定义种族尤为重要
            if (mannequin != null && pawn.def != null)
            {
                mannequin.SetRace(pawn.def);
                mannequin.CopyAppearanceFrom(pawn);
                Log.Message($"[CharacterStudio] 已将人偶种族同步为 {pawn.def.defName} 并复制外观");
            }

            // 询问是否清空现有图层
            if (workingSkin.layers.Count > 0)
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption("CS_Studio_Ctx_ClearAndImport".Translate(), () =>
                    {
                        workingSkin.layers.Clear();
                        workingSkin.hiddenPaths.Clear();
                        workingSkin.hiddenTags.Clear();
                        foreach (var layer in layers)
                        {
                            workingSkin.layers.Add(layer);
                        }
                        // 隐藏对应的原生节点，避免与导入的图层重叠
                        foreach (var path in sourcePaths)
                        {
                            if (!string.IsNullOrEmpty(path) && !workingSkin.hiddenPaths.Contains(path))
                            {
                                workingSkin.hiddenPaths.Add(path);
                            }
                        }
                        // 使用标签作为回退隐藏机制（当路径匹配失败时）
                        foreach (var tag in sourceTags)
                        {
                            if (!string.IsNullOrEmpty(tag) && !workingSkin.hiddenTags.Contains(tag))
                            {
                                workingSkin.hiddenTags.Add(tag);
                            }
                        }
                        if (shouldHideVanillaBody)
                        {
                            workingSkin.hideVanillaBody = true;
                        }
                        if (shouldHideVanillaHead)
                        {
                            workingSkin.hideVanillaHead = true;
                        }
                        selectedLayerIndex = 0;
                        targetPawn = pawn;
                        isDirty = true;
                        RefreshPreview();
                        // 刷新渲染树快照，以便在树视图中显示新注入的节点
                        RefreshRenderTree();
                        ShowStatus($"已导入并绑定目标角色: {pawn.LabelShort}");
                    }),
                    new FloatMenuOption("CS_Studio_Ctx_AppendImport".Translate(), () =>
                    {
                        foreach (var layer in layers)
                        {
                            workingSkin.layers.Add(layer);
                        }
                        // 隐藏对应的原生节点，避免与导入的图层重叠
                        foreach (var path in sourcePaths)
                        {
                            if (!string.IsNullOrEmpty(path) && !workingSkin.hiddenPaths.Contains(path))
                            {
                                workingSkin.hiddenPaths.Add(path);
                            }
                        }
                        // 使用标签作为回退隐藏机制（当路径匹配失败时）
                        foreach (var tag in sourceTags)
                        {
                            if (!string.IsNullOrEmpty(tag) && !workingSkin.hiddenTags.Contains(tag))
                            {
                                workingSkin.hiddenTags.Add(tag);
                            }
                        }
                        if (shouldHideVanillaBody)
                        {
                            workingSkin.hideVanillaBody = true;
                        }
                        if (shouldHideVanillaHead)
                        {
                            workingSkin.hideVanillaHead = true;
                        }
                        selectedLayerIndex = workingSkin.layers.Count - layers.Count;
                        targetPawn = pawn;
                        isDirty = true;
                        RefreshPreview();
                        // 刷新渲染树快照
                        RefreshRenderTree();
                        ShowStatus($"已追加导入并绑定目标角色: {pawn.LabelShort}");
                    }),
                    new FloatMenuOption("CS_Studio_Btn_Cancel".Translate(), () => { })
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }
            else
            {
                // 直接导入
                foreach (var layer in layers)
                {
                    workingSkin.layers.Add(layer);
                }
                // 隐藏对应的原生节点，避免与导入的图层重叠
                foreach (var path in sourcePaths)
                {
                    if (!string.IsNullOrEmpty(path) && !workingSkin.hiddenPaths.Contains(path))
                    {
                        workingSkin.hiddenPaths.Add(path);
                    }
                }
                // 使用标签作为回退隐藏机制（当路径匹配失败时）
                foreach (var tag in sourceTags)
                {
                    if (!string.IsNullOrEmpty(tag) && !workingSkin.hiddenTags.Contains(tag))
                    {
                        workingSkin.hiddenTags.Add(tag);
                    }
                }
                if (shouldHideVanillaBody)
                {
                    workingSkin.hideVanillaBody = true;
                }
                if (shouldHideVanillaHead)
                {
                    workingSkin.hideVanillaHead = true;
                }
                selectedLayerIndex = 0;
                targetPawn = pawn;
                isDirty = true;
                RefreshPreview();
                // 刷新渲染树快照
                RefreshRenderTree();
                ShowStatus($"已导入并绑定目标角色: {pawn.LabelShort}");
            }
        }

        // ─────────────────────────────────────────────
        // Mannequin 管理
        // ─────────────────────────────────────────────

        private void InitializeMannequin()
        {
            try
            {
                mannequin = new MannequinManager();
                mannequin.Initialize();

                if (targetPawn != null)
                {
                    mannequin.SetRace(targetPawn.def);
                    mannequin.CopyAppearanceFrom(targetPawn);
                }

                RefreshPreview();
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 初始化人偶失败: {ex}");
                ShowStatus("CS_Studio_Err_MannequinFailed".Translate());
            }
        }

        private void CleanupMannequin()
        {
            mannequin?.Cleanup();
            mannequin = null;
        }

        private void RefreshPreview()
        {
            mannequin?.ApplySkin(workingSkin);
        }

        private void ShowStatus(string message)
        {
            statusMessage = message;
            statusMessageTime = 3f;
        }
    }
}
