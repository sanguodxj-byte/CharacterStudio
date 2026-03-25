using System.Collections.Generic;
using System.Linq;
using CharacterStudio.Core;
using RimWorld;
using Verse;

namespace CharacterStudio.UI
{
    public partial class Dialog_SkinEditor
    {
        // ─────────────────────────────────────────────
        // 导入菜单 / 入口
        // ─────────────────────────────────────────────

        private void OnImportFromMap()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("CS_Studio_Import_Source_Map".Translate(), ShowMapPawnImportMenu),
                new FloatMenuOption("CS_Studio_Import_Source_Race".Translate(), OnImportFromRaceList),
                new FloatMenuOption("CS_Studio_Import_Source_ProjectSkin".Translate(), OnImportFromProjectSkins),
                new FloatMenuOption("图层修改工作流", ShowLayerModificationWorkflowMenu)
            };

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowMapPawnImportMenu()
        {
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

            var options = new List<FloatMenuOption>();
            foreach (var pawn in pawns)
            {
                var p = pawn;
                options.Add(new FloatMenuOption(
                    $"{p.LabelShort} ({p.kindDef.label})",
                    () => DoImportFromPawn(p, p, p.def, p.LabelShort, true)
                ));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowLayerModificationWorkflowMenu()
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                ShowStatus("CS_Studio_Err_NoMap".Translate());
                return;
            }

            var pawns = map.mapPawns.AllPawnsSpawned
                .Where(p => p.Drawer?.renderer?.renderTree != null)
                .OrderBy(p => p.def?.label ?? p.def?.defName ?? p.LabelShort)
                .ThenBy(p => p.LabelShort)
                .ToList();

            if (pawns.Count == 0)
            {
                ShowStatus("CS_Studio_Err_NoPawns".Translate());
                return;
            }

            var options = new List<FloatMenuOption>();
            foreach (var pawn in pawns)
            {
                Pawn localPawn = pawn;
                ThingDef? raceDef = localPawn.def;
                string? raceLabel = raceDef?.label;
                string displayRace = string.IsNullOrWhiteSpace(raceLabel)
                    ? raceDef?.defName ?? "UnknownRace"
                    : raceLabel ?? "UnknownRace";

                options.Add(new FloatMenuOption(
                    $"{localPawn.LabelShort} ({displayRace})",
                    () => StartLayerModificationWorkflow(localPawn)));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OnImportFromRaceList()
        {
            var races = MannequinManager.GetAvailableRaces();
            if (races.Length == 0)
            {
                ShowStatus("CS_Studio_Err_NoRaces".Translate());
                return;
            }

            var options = new List<FloatMenuOption>();
            foreach (var race in races)
            {
                var raceLocal = race;
                string displayName = string.IsNullOrWhiteSpace(raceLocal.label) ? raceLocal.defName : raceLocal.label;
                options.Add(new FloatMenuOption(
                    $"{displayName} ({raceLocal.defName})",
                    () => DoImportFromRace(raceLocal)
                ));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OnImportFromProjectSkins()
        {
            PawnSkinDefRegistry.LoadFromConfig();
            var skins = PawnSkinDefRegistry.AllRuntimeDefs
                .Where(skin => skin != null)
                .OrderBy(skin => string.IsNullOrWhiteSpace(skin.label) ? skin.defName : skin.label)
                .ToList();

            if (skins.Count == 0)
            {
                ShowStatus("CS_Studio_Err_NoProjectSkins".Translate());
                return;
            }

            var options = new List<FloatMenuOption>();
            foreach (var skin in skins)
            {
                var skinLocal = skin;
                string displayName = string.IsNullOrWhiteSpace(skinLocal.label) ? skinLocal.defName : skinLocal.label;
                options.Add(new FloatMenuOption(
                    $"{displayName} ({skinLocal.defName})",
                    () => DoImportFromProjectSkin(skinLocal)
                ));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
