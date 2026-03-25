using System;
using System.IO;
using CharacterStudio.Core;
using CharacterStudio.Design;
using CharacterStudio.Exporter;
using RimWorld;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        // ─────────────────────────────────────────────
        // 保存 / 应用
        // ─────────────────────────────────────────────

        private void OnSaveSkin()
        {
            if (workingSkin == null)
            {
                Log.Error("[CharacterStudio] 保存失败：workingSkin 为空");
                ShowStatus("CS_Studio_Err_SaveFailed".Translate());
                return;
            }

            workingSkin.defName = workingSkin.defName?.Trim() ?? string.Empty;
            workingSkin.label = workingSkin.label?.Trim() ?? string.Empty;

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

            string exportDir = Path.Combine(GenFilePaths.ConfigFolderPath, "CharacterStudio", "Skins");
            string fileName = workingSkin.defName + ".xml";
            string filePath = Path.Combine(exportDir, fileName);

            try
            {
                Directory.CreateDirectory(exportDir);

                var savePlanSkin = BuildRuntimeSkinForExecution();
                if (savePlanSkin == null)
                {
                    Log.Error("[CharacterStudio] 保存失败：未能构建运行时皮肤");
                    ShowStatus("CS_Studio_Err_SaveFailed".Translate());
                    return;
                }

                SkinSaver.SaveSkinDef(savePlanSkin, filePath);

                var registered = PawnSkinDefRegistry.RegisterOrReplace(savePlanSkin.Clone());
                string? preferredRaceDefName = registered.targetRaces != null && registered.targetRaces.Count > 0
                    ? registered.targetRaces[0]
                    : null;
                ReplaceWorkingSkin(registered, registered.defName, preferredRaceDefName, syncAbilities: false);

                if (registered.applyAsDefaultForTargetRaces)
                {
                    PawnSkinBootstrapComponent.ApplyDefaultSkinsToCurrentGame();
                }

                string finalStatus = "CS_Studio_Msg_SaveSuccess".Translate();
                if (targetPawn != null)
                {
                    var applyPlan = BuildApplicationPlan(targetPawn, false, "SaveAndApplyToTargetPawn");
                    applyPlan.runtimeSkin = registered.Clone();
                    if (CharacterApplicationExecutor.Execute(applyPlan))
                    {
                        finalStatus = applyPlan.statusMessage.Translate(targetPawn.LabelShort);
                        Messages.Message(
                            "CS_Appearance_Applied".Translate(registered.label ?? registered.defName, targetPawn.LabelShort),
                            MessageTypeDefOf.PositiveEvent,
                            false
                        );

                        if (applyPlan.warnings != null && applyPlan.warnings.Count > 0)
                        {
                            Messages.Message(applyPlan.warnings[0].Translate(), MessageTypeDefOf.CautionInput, false);
                        }
                    }
                    else
                    {
                        string applyStatusKey = string.IsNullOrWhiteSpace(applyPlan.statusMessage)
                            ? "CS_Studio_Err_ApplyFailedCheckLog"
                            : applyPlan.statusMessage;
                        finalStatus = applyStatusKey.Translate();
                        Log.Warning($"[CharacterStudio] 保存后自动应用失败: source={applyPlan.source}, pawn={targetPawn.LabelShort}, status={applyPlan.statusMessage ?? "<empty>"}");
                    }
                }

                ShowStatus(finalStatus);
                Messages.Message("CS_Studio_Msg_SavePath".Translate(filePath), MessageTypeDefOf.PositiveEvent, false);
                isDirty = false;
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 保存失败: {ex}");

                string statusMessage = SkinSaver.GetSaveFailureMessageKey(ex) == "CS_Studio_Err_SaveFailedDiskFull"
                    ? "保存失败：磁盘空间不足、写入配额已满，或目标配置目录不可再写入"
                    : "CS_Studio_Err_SaveFailed".Translate();
                ShowStatus(statusMessage);
            }
        }

        private void OnApplyToTargetPawn()
        {
            if (targetPawn == null)
            {
                ShowStatus("CS_Studio_Msg_TargetPawnRequired".Translate());
                return;
            }

            OnApplySkinToTargetPawn();
        }

        private void OnApplySkinToTargetPawn()
        {
            if (targetPawn == null)
            {
                ShowStatus("CS_Studio_Msg_TargetPawnRequired".Translate());
                return;
            }

            if (workingSkin == null)
            {
                Log.Error("[CharacterStudio] 应用失败：workingSkin 为空");
                ShowStatus("CS_Studio_Err_ApplyFailedCheckLog".Translate());
                return;
            }

            try
            {
                var applyPlan = BuildApplicationPlan(targetPawn, false, "ApplyToTargetPawn");
                var runtimeSkin = applyPlan.runtimeSkin;
                if (CharacterApplicationExecutor.Execute(applyPlan))
                {
                    RefreshRenderTree();
                    ShowStatus(applyPlan.statusMessage.Translate(targetPawn.LabelShort));
                    Messages.Message(
                        "CS_Appearance_Applied".Translate(runtimeSkin.label ?? runtimeSkin.defName, targetPawn.LabelShort),
                        MessageTypeDefOf.PositiveEvent,
                        false
                    );

                    if (applyPlan.warnings != null && applyPlan.warnings.Count > 0)
                    {
                        Messages.Message(applyPlan.warnings[0].Translate(), MessageTypeDefOf.CautionInput, false);
                    }
                }
                else
                {
                    string statusKey = string.IsNullOrWhiteSpace(applyPlan.statusMessage)
                        ? "CS_Studio_Err_ApplyFailedCheckLog"
                        : applyPlan.statusMessage;
                    ShowStatus(statusKey.Translate());
                    Log.Warning($"[CharacterStudio] 应用到目标角色失败: source={applyPlan.source}, pawn={targetPawn.LabelShort}, status={applyPlan.statusMessage ?? "<empty>"}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用到目标角色失败: {ex}");
                ShowStatus("CS_Studio_Err_ApplyFailedCheckLog".Translate());
            }
        }
    }
}
