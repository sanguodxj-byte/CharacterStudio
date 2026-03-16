using System;
using System.Collections.Generic;
using CharacterStudio.Abilities;
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
            // 存档重载后重新授予所有 CS 技能
            // （运行时 AbilityDef 不持久化到存档，每次加载都需重建）
            ReGrantAllSkinAbilities();
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

        private static void ReGrantAllSkinAbilities()
        {
            if (Current.Game == null) return;

            int count = 0;
            foreach (var map in Current.Game.Maps)
            {
                var pawns = map?.mapPawns?.AllPawnsSpawned;
                if (pawns == null) continue;

                foreach (var pawn in pawns)
                {
                    if (pawn == null) continue;
                    var comp = pawn.GetComp<CompPawnSkin>();
                    var skin = comp?.ActiveSkin;
                    if (skin == null || skin.abilities == null || skin.abilities.Count == 0) continue;

                    try
                    {
                        CharacterStudio.Abilities.AbilityGrantUtility.GrantSkinAbilitiesToPawn(pawn, skin);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[CharacterStudio] 存档重载重授技能失败 ({pawn.LabelShort}): {ex.Message}");
                    }
                }
            }

            if (count > 0)
                Log.Message($"[CharacterStudio] 存档重载已为 {count} 个 Pawn 重授 CS 技能");
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
