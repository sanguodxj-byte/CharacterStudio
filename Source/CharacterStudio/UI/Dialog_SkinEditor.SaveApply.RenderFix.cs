using System;
using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using CharacterStudio.Introspection;
using RimWorld;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        private void OnApplyRenderFixPatchToTargetPawn()
        {
            if (targetPawn == null)
            {
                ShowStatus("CS_Studio_Msg_TargetPawnRequired".Translate());
                return;
            }

            try
            {
                CharacterRenderFixPatch patch = BuildRenderFixPatchFromCurrentState(targetPawn);
                if (patch.hideNodePaths.Count == 0 && patch.orderOverrides.Count == 0)
                {
                    ShowStatus("当前没有可保存的显示修正".Translate());
                    return;
                }

                RenderFixPatchRegistry.SavePatch(patch);
                CharacterStudio.Rendering.Patch_PawnRenderTree.RefreshHiddenNodes(targetPawn);
                CharacterStudio.Rendering.Patch_PawnRenderTree.ForceRebuildRenderTree(targetPawn);
                RefreshPreview();
                RefreshRenderTree();

                ShowStatus($"已应用显示修正补丁：{patch.label}");
                Messages.Message(
                    $"已为 {targetPawn.LabelShort} 的种族保存显示修正补丁：{patch.label}",
                    MessageTypeDefOf.PositiveEvent,
                    false);
            }
            catch (Exception ex)
            {
                Log.Error($"[CharacterStudio] 应用显示修正补丁失败: {ex}");
                ShowStatus("显示修正补丁应用失败，请检查日志");
            }
        }

        private List<RenderNodeOrderOverride> BuildRenderFixOrderOverridesFromSelection()
        {
            if (layerModificationWorkflowActive && workingRenderFixPatch?.orderOverrides != null)
            {
                return workingRenderFixPatch.orderOverrides
                    .Where(entry => entry != null)
                    .Select(entry => entry.Clone())
                    .ToList();
            }

            return new List<RenderNodeOrderOverride>();
        }

        private static Dictionary<string, RenderNodeSnapshot> FlattenSnapshotByPath(RenderNodeSnapshot root)
        {
            Dictionary<string, RenderNodeSnapshot> result = new Dictionary<string, RenderNodeSnapshot>(StringComparer.OrdinalIgnoreCase);
            void Visit(RenderNodeSnapshot? node)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.uniqueNodePath))
                {
                    return;
                }

                result[node.uniqueNodePath] = node;
                if (node.children == null)
                {
                    return;
                }

                foreach (RenderNodeSnapshot child in node.children)
                {
                    Visit(child);
                }
            }

            Visit(root);
            return result;
        }
    }
}