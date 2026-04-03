using System.Collections.Generic;

namespace CharacterStudio.UI
{
    /// <summary>
    /// Skin editor session state.
    ///
    /// First extraction slice intentionally owns only the smallest stable state cluster:
    /// - dirty flag
    /// - primary selected layer index
    /// - selected layer index set
    ///
    /// Additional editor state should be migrated here incrementally in later slices.
    /// </summary>
    public sealed class SkinEditorSession
    {
        public bool IsDirty { get; set; }

        public int SelectedLayerIndex { get; set; } = -1;

        public HashSet<int> SelectedLayerIndices { get; set; } = new HashSet<int>();
    }
}
