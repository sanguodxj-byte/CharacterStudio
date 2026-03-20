using System;
using System.Collections.Generic;
using CharacterStudio.Core;

namespace CharacterStudio.Design
{
    /// <summary>
    /// 编辑器内部使用的角色设计文档。
    /// 当前阶段先作为 PawnSkinDef 之上的编辑容器，
    /// 为后续完全切换到“文档 -> 编译产物”工作流建立边界。
    /// </summary>
    public class CharacterDesignDocument
    {
        public string designId = Guid.NewGuid().ToString("N");
        public string title = "";
        public string description = "";
        public string author = "";
        public string version = "1.0.0";
        public string preferredPreviewRaceDefName = "";
        public string preferredTargetRaceDefName = "";
        public string sourceSkinDefName = "";
        public string lastSavedFilePath = "";
        public List<string> importedSources = new List<string>();
        public List<CharacterNodeRule> nodeRules = new List<CharacterNodeRule>();
        public PawnSkinDef runtimeSkin = new PawnSkinDef();

        public CharacterDesignDocument Clone()
        {
            return new CharacterDesignDocument
            {
                designId = designId,
                title = title,
                description = description,
                author = author,
                version = version,
                preferredPreviewRaceDefName = preferredPreviewRaceDefName,
                preferredTargetRaceDefName = preferredTargetRaceDefName,
                sourceSkinDefName = sourceSkinDefName,
                lastSavedFilePath = lastSavedFilePath,
                importedSources = new List<string>(importedSources ?? new List<string>()),
                nodeRules = new List<CharacterNodeRule>((nodeRules ?? new List<CharacterNodeRule>()).ConvertAll(rule => rule?.Clone() ?? new CharacterNodeRule())),
                runtimeSkin = runtimeSkin?.Clone() ?? new PawnSkinDef()
            };
        }

        public void SyncMetadataFromRuntimeSkin()
        {
            runtimeSkin ??= new PawnSkinDef();
            title = runtimeSkin.label ?? runtimeSkin.defName ?? "";
            description = runtimeSkin.description ?? "";
            author = runtimeSkin.author ?? "";
            version = string.IsNullOrWhiteSpace(runtimeSkin.version) ? "1.0.0" : runtimeSkin.version;
            sourceSkinDefName = runtimeSkin.defName ?? "";
        }
    }
}
