using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace CharacterStudio.Core
{
    /// <summary>
    /// 游戏级初始化：确保地图 Pawn 都拥有 CompPawnSkin 以支持运行时应用外观
    /// </summary>
    public class PawnSkinBootstrapComponent : GameComponent
    {
        public PawnSkinBootstrapComponent(Game game)
        {
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            EnsureAllMapsPawnsHaveSkinComp();
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            EnsureAllMapsPawnsHaveSkinComp();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            EnsureAllMapsPawnsHaveSkinComp();
        }

        private void EnsureAllMapsPawnsHaveSkinComp()
        {
            if (Current.Game == null) return;

            int added = 0;
            foreach (var map in Current.Game.Maps)
            {
                var pawns = map?.mapPawns?.AllPawnsSpawned;
                if (pawns == null) continue;

                foreach (var pawn in pawns)
                {
                    if (pawn == null) continue;
                    if (pawn.GetComp<CompPawnSkin>() != null) continue;

                    AddCompToPawn(pawn);
                    added++;
                }
            }

            if (added > 0)
            {
                Log.Message($"[CharacterStudio] 已为现存地图 Pawn 补全 CompPawnSkin: {added}");
            }
        }

        private static void AddCompToPawn(Pawn pawn)
        {
            var compsField = AccessTools.Field(typeof(ThingWithComps), "comps");
            if (compsField == null) return;

            var comps = compsField.GetValue(pawn) as List<ThingComp>;
            if (comps == null)
            {
                comps = new List<ThingComp>();
                compsField.SetValue(pawn, comps);
            }

            foreach (var thingComp in comps)
            {
                if (thingComp is CompPawnSkin) return;
            }

            var comp = new CompPawnSkin
            {
                parent = pawn
            };
            comps.Add(comp);
            comp.Initialize(new CompProperties_PawnSkin());
            comp.PostSpawnSetup(pawn.Spawned);
        }
    }
}
