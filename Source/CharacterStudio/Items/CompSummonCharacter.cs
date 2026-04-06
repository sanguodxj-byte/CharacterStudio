using System;
using System.IO;
using RimWorld;
using CharacterStudio.Core;
using Verse;

namespace CharacterStudio.Items
{
    /// <summary>
    /// 角色召唤组件
    /// 继承自 CompUseEffect，配合 CompUsable 使用
    /// </summary>
    public class CompSummonCharacter : CompUseEffect
    {
        public CompProperties_SummonCharacter Props => (CompProperties_SummonCharacter)props;

        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);

            if (usedBy == null || usedBy.Map == null)
            {
                Log.Error("[CharacterStudio] 召唤失败：使用者或地图为空");
                return;
            }

            if (Props.pawnKind == null)
            {
                Log.Error("[CharacterStudio] 召唤失败：PawnKind 为空");
                return;
            }

            Faction spawnFaction = Props.isHostile
                ? (Faction.OfAncientsHostile ?? Faction.OfPlayer)
                : Faction.OfPlayer;
            Pawn? pawn = CharacterSpawnUtility.GeneratePawn(Props.pawnKind, spawnFaction);
            if (pawn == null)
            {
                Log.Error("[CharacterStudio] 召唤失败：生成 Pawn 失败");
                return;
            }

            if (!Props.isHostile && pawn.Faction != Faction.OfPlayer)
            {
                pawn.SetFaction(Faction.OfPlayer);
            }

            // 生成位置 - 寻找附近可用位置
            IntVec3 loc = usedBy.Position;
            Map map = usedBy.Map;
            CharacterSpawnSettings spawnSettings = new CharacterSpawnSettings
            {
                sourceMapForConditionCheck = map,
                arrivalDefName = Props.arrivalDefName,
                arrivalMode = Props.arrivalMode,
                spawnAnimationDefName = Props.spawnAnimationDefName,
                spawnAnimation = Props.spawnAnimation,
                spawnAnimationScale = Props.spawnAnimationScale,
                spawnEventDefName = Props.spawnEventDefName,
                spawnEvent = Props.spawnEvent
            };

            CharacterSpawnUtility.TryFindSpawnCell(map, usedBy.Position, 5, out loc);
            
            CharacterSpawnUtility.SpawnPawnWithSettings(pawn, map, loc, spawnSettings);

            ApplyExportedCharacterData(pawn);

            CharacterSpawnUtility.SendSpawnEvent(
                spawnSettings,
                pawn,
                map,
                loc,
                "CS_Summon_Success".Translate(pawn.LabelShort),
                "CS_RoleCard_LetterLabel".Translate(pawn.LabelShort));
        }

        private void ApplyExportedCharacterData(Pawn pawn)
        {
            ThingDef? parentDef = parent?.def;
            if (parentDef == null)
            {
                return;
            }

            string summonDefName = parentDef.defName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(summonDefName) || !summonDefName.StartsWith("CS_Item_Summon_", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string safeName = summonDefName.Substring("CS_Item_Summon_".Length);
            string skinDefName = string.IsNullOrWhiteSpace(Props.skinDefName)
                ? $"Skin_{safeName}"
                : Props.skinDefName;
            PawnSkinDef? skin = DefDatabase<PawnSkinDef>.GetNamedSilentFail(skinDefName) ?? PawnSkinDefRegistry.TryGet(skinDefName);

            CharacterDefinition? characterDefinition = TryLoadExportedCharacterDefinition(safeName, Props.characterDefFileName, skin);
            if (characterDefinition != null)
            {
                CharacterDefinitionApplier.ApplyToPawn(pawn, characterDefinition);
            }

            if (skin != null)
            {
                PawnSkinRuntimeUtility.ApplySkinToPawn(pawn, skin.Clone(), fromDefaultRaceBinding: false, previewMode: false, applicationSource: "RoleCardSummon");
            }
        }

        private static CharacterDefinition? TryLoadExportedCharacterDefinition(string safeName, string explicitCharacterFileName, PawnSkinDef? skin)
        {
            string fileName = string.IsNullOrWhiteSpace(explicitCharacterFileName)
                ? $"{safeName}_Character.xml"
                : explicitCharacterFileName;

            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                string filePath = Path.Combine(mod.RootDir, "Defs", "PawnKindDefs", fileName);
                CharacterDefinition? loaded = CharacterDefinitionXmlUtility.Load(filePath);
                if (loaded != null)
                {
                    loaded.EnsureDefaults(skin?.defName ?? $"Skin_{safeName}", ResolveFallbackRace(loaded, skin), skin?.attributes);
                    return loaded;
                }
            }

            return null;
        }

        private static ThingDef ResolveFallbackRace(CharacterDefinition definition, PawnSkinDef? skin)
        {
            if (!string.IsNullOrWhiteSpace(definition.raceDefName))
            {
                ThingDef? fromDefinition = DefDatabase<ThingDef>.GetNamedSilentFail(definition.raceDefName);
                if (fromDefinition != null)
                {
                    return fromDefinition;
                }
            }

            if (skin?.targetRaces != null)
            {
                foreach (string raceDefName in skin.targetRaces)
                {
                    ThingDef? raceDef = DefDatabase<ThingDef>.GetNamedSilentFail(raceDefName);
                    if (raceDef != null)
                    {
                        return raceDef;
                    }
                }
            }

            return ThingDefOf.Human;
        }
    }

    public enum SummonArrivalMode
    {
        Standing,
        DropPod
    }

    public enum SummonSpawnAnimationMode
    {
        None,
        DustPuff,
        MicroSparks,
        LightningGlow,
        FireGlow,
        Smoke,
        ExplosionEffect
    }

    public enum SummonSpawnEventMode
    {
        None,
        Message,
        PositiveLetter
    }

    public class CompProperties_SummonCharacter : CompProperties_UseEffect
    {
        public PawnKindDef? pawnKind;
        public string skinDefName = string.Empty;
        public string characterDefFileName = string.Empty;
        public bool isHostile = false;
        public string arrivalDefName = "CS_SpawnArrival_Standing";
        public SummonArrivalMode arrivalMode = SummonArrivalMode.Standing;
        public string spawnAnimationDefName = "CS_SpawnAnimation_None";
        public SummonSpawnAnimationMode spawnAnimation = SummonSpawnAnimationMode.None;
        public float spawnAnimationScale = 1f;
        public string spawnEventDefName = "CS_SpawnEvent_Message";
        public SummonSpawnEventMode spawnEvent = SummonSpawnEventMode.Message;

        public CompProperties_SummonCharacter()
        {
            this.compClass = typeof(CompSummonCharacter);
        }
    }
}
