using Verse;

namespace CharacterStudio.Core
{
    public sealed class CharacterStudioSettings : ModSettings
    {
        public float portraitTrackRootSizeNear = 18f;
        public float portraitTrackBudgetNear = 999f;
        public float portraitTrackRootSizeMid1 = 24f;
        public float portraitTrackBudgetMid1 = 18f;
        public float portraitTrackRootSizeMid2 = 32f;
        public float portraitTrackBudgetMid2 = 10f;
        public float portraitTrackRootSizeFar = 40f;
        public float portraitTrackBudgetFar = 6f;
        public float portraitTrackBudgetFallback = 0f;
        public float portraitTrackFallbackPriorityThreshold = 10f;
        public float portraitTrackFallbackPriorityBudget = 4f;

        public float draftedPriorityBonus = 8f;
        public float colonistPriorityBonus = 6f;
        public float unstablePriorityBonus = 5f;

        public void ApplyBalancedPreset()
        {
            portraitTrackRootSizeNear = 18f;
            portraitTrackBudgetNear = 999f;
            portraitTrackRootSizeMid1 = 24f;
            portraitTrackBudgetMid1 = 18f;
            portraitTrackRootSizeMid2 = 32f;
            portraitTrackBudgetMid2 = 10f;
            portraitTrackRootSizeFar = 40f;
            portraitTrackBudgetFar = 6f;
            portraitTrackBudgetFallback = 0f;
            portraitTrackFallbackPriorityThreshold = 10f;
            portraitTrackFallbackPriorityBudget = 4f;
            draftedPriorityBonus = 8f;
            colonistPriorityBonus = 6f;
            unstablePriorityBonus = 5f;
        }

        public void ApplyPerformancePreset()
        {
            portraitTrackRootSizeNear = 16f;
            portraitTrackBudgetNear = 999f;
            portraitTrackRootSizeMid1 = 22f;
            portraitTrackBudgetMid1 = 10f;
            portraitTrackRootSizeMid2 = 28f;
            portraitTrackBudgetMid2 = 4f;
            portraitTrackRootSizeFar = 34f;
            portraitTrackBudgetFar = 0f;
            portraitTrackBudgetFallback = 0f;
            portraitTrackFallbackPriorityThreshold = 10f;
            portraitTrackFallbackPriorityBudget = 2f;
            draftedPriorityBonus = 4f;
            colonistPriorityBonus = 2f;
            unstablePriorityBonus = 2f;
        }

        public void ApplyUltraPerformancePreset()
        {
            portraitTrackRootSizeNear = 14f;
            portraitTrackBudgetNear = 16f;
            portraitTrackRootSizeMid1 = 18f;
            portraitTrackBudgetMid1 = 4f;
            portraitTrackRootSizeMid2 = 22f;
            portraitTrackBudgetMid2 = 0f;
            portraitTrackRootSizeFar = 28f;
            portraitTrackBudgetFar = 0f;
            portraitTrackBudgetFallback = 0f;
            portraitTrackFallbackPriorityThreshold = 12f;
            portraitTrackFallbackPriorityBudget = 0f;
            draftedPriorityBonus = 2f;
            colonistPriorityBonus = 1f;
            unstablePriorityBonus = 1f;
        }

        public void ResetToDefaults()
            => ApplyBalancedPreset();

        public override void ExposeData()
        {
            Scribe_Values.Look(ref portraitTrackRootSizeNear, nameof(portraitTrackRootSizeNear), 18f);
            Scribe_Values.Look(ref portraitTrackBudgetNear, nameof(portraitTrackBudgetNear), 999f);
            Scribe_Values.Look(ref portraitTrackRootSizeMid1, nameof(portraitTrackRootSizeMid1), 24f);
            Scribe_Values.Look(ref portraitTrackBudgetMid1, nameof(portraitTrackBudgetMid1), 18f);
            Scribe_Values.Look(ref portraitTrackRootSizeMid2, nameof(portraitTrackRootSizeMid2), 32f);
            Scribe_Values.Look(ref portraitTrackBudgetMid2, nameof(portraitTrackBudgetMid2), 10f);
            Scribe_Values.Look(ref portraitTrackRootSizeFar, nameof(portraitTrackRootSizeFar), 40f);
            Scribe_Values.Look(ref portraitTrackBudgetFar, nameof(portraitTrackBudgetFar), 6f);
            Scribe_Values.Look(ref portraitTrackBudgetFallback, nameof(portraitTrackBudgetFallback), 0f);
            Scribe_Values.Look(ref portraitTrackFallbackPriorityThreshold, nameof(portraitTrackFallbackPriorityThreshold), 10f);
            Scribe_Values.Look(ref portraitTrackFallbackPriorityBudget, nameof(portraitTrackFallbackPriorityBudget), 4f);
            Scribe_Values.Look(ref draftedPriorityBonus, nameof(draftedPriorityBonus), 8f);
            Scribe_Values.Look(ref colonistPriorityBonus, nameof(colonistPriorityBonus), 6f);
            Scribe_Values.Look(ref unstablePriorityBonus, nameof(unstablePriorityBonus), 5f);
        }
    }
}
