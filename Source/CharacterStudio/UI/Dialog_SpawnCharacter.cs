using System;
using CharacterStudio.Core;
using CharacterStudio.Items;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.UI
{
    public sealed class Dialog_SpawnCharacter : Window
    {
        private readonly Action<CharacterSpawnSettings> onConfirm;
        private SummonArrivalMode arrivalMode;
        private SummonSpawnEventMode spawnEvent;
        private SummonSpawnAnimationMode spawnAnimation;
        private float spawnAnimationScale;

        public override Vector2 InitialSize => new Vector2(460f, 320f);

        public Dialog_SpawnCharacter(CharacterSpawnSettings? initialSettings, Action<CharacterSpawnSettings> onConfirm)
        {
            this.onConfirm = onConfirm ?? throw new ArgumentNullException(nameof(onConfirm));
            CharacterSpawnSettings settings = initialSettings?.Clone() ?? new CharacterSpawnSettings();
            arrivalMode = settings.arrivalMode;
            spawnEvent = settings.spawnEvent;
            spawnAnimation = settings.spawnAnimation;
            spawnAnimationScale = settings.spawnAnimationScale;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Rect shellRect = new Rect(0f, 0f, inRect.width, inRect.height);
            Rect titleRect = UIHelper.DrawPanelShell(shellRect, "CS_Studio_SpawnNewPawnTitle".Translate(), 0f);

            float y = titleRect.yMax + 8f;
            float width = inRect.width;

            UIHelper.DrawSectionTitle(ref y, width, "CS_Studio_SpawnNewPawnSection".Translate());
            UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Export_RoleCardArrival".Translate(), arrivalMode,
                (SummonArrivalMode[])Enum.GetValues(typeof(SummonArrivalMode)),
                mode => $"CS_Studio_Export_RoleCardArrival_{mode}".Translate(),
                val => arrivalMode = val);
            UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Export_RoleCardEvent".Translate(), spawnEvent,
                (SummonSpawnEventMode[])Enum.GetValues(typeof(SummonSpawnEventMode)),
                mode => $"CS_Studio_Export_RoleCardEvent_{mode}".Translate(),
                val => spawnEvent = val);
            UIHelper.DrawPropertyDropdown(ref y, width, "CS_Studio_Export_RoleCardAnimation".Translate(), spawnAnimation,
                (SummonSpawnAnimationMode[])Enum.GetValues(typeof(SummonSpawnAnimationMode)),
                mode => $"CS_Studio_Export_RoleCardAnimation_{mode}".Translate(),
                val => spawnAnimation = val);

            if (spawnAnimation != SummonSpawnAnimationMode.None)
            {
                UIHelper.DrawPropertySlider(ref y, width, "CS_Studio_Export_RoleCardAnimationScale".Translate(), ref spawnAnimationScale, 0.1f, 5f, "F2");
            }

            float btnWidth = 120f;
            float btnY = inRect.height - 40f;
            if (UIHelper.DrawToolbarButton(new Rect(inRect.width / 2f - btnWidth - 8f, btnY, btnWidth, 28f), "CS_Studio_SpawnNewPawnConfirm".Translate(), accent: true))
            {
                onConfirm(new CharacterSpawnSettings
                {
                    arrivalMode = arrivalMode,
                    spawnEvent = spawnEvent,
                    spawnAnimation = spawnAnimation,
                    spawnAnimationScale = spawnAnimationScale
                });
                Close();
            }

            if (UIHelper.DrawToolbarButton(new Rect(inRect.width / 2f + 8f, btnY, btnWidth, 28f), "CS_Studio_Btn_Cancel".Translate()))
            {
                Close();
            }
        }
    }
}
