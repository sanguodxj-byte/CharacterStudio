using System.Collections.Generic;
using CharacterStudio.Core;
using Verse;
using RimWorld;

namespace CharacterStudio.Design
{
    /// <summary>
    /// 第二阶段最小应用计划。
    /// 先统一“将哪个运行时皮肤应用到哪个目标”这件事，
    /// 后续再逐步扩展节点命中、验证结果、重建策略等字段。
    /// </summary>
    public class CharacterApplicationPlan
    {
        public Pawn? targetPawn;
        public PawnSkinDef runtimeSkin = new PawnSkinDef();
        public bool isPreview;
        public bool spawnAsNewPawn;
        public string source = "";
        public bool isValid = true;
        public string statusMessage = "";
        public List<string> warnings = new List<string>();
        public PawnKindDef? spawnPawnKind;
        public ThingDef? spawnRaceDef;
        public Faction? spawnFaction;
        public Map? spawnMap;
        public IntVec3 desiredSpawnCell = IntVec3.Invalid;
        public CharacterSpawnSettings spawnSettings = new CharacterSpawnSettings();

        public CharacterApplicationPlan Clone()
        {
            return new CharacterApplicationPlan
            {
                targetPawn = targetPawn,
                runtimeSkin = runtimeSkin?.Clone() ?? new PawnSkinDef(),
                isPreview = isPreview,
                spawnAsNewPawn = spawnAsNewPawn,
                source = source ?? string.Empty,
                isValid = isValid,
                statusMessage = statusMessage ?? string.Empty,
                warnings = new List<string>(warnings ?? new List<string>()),
                spawnPawnKind = spawnPawnKind,
                spawnRaceDef = spawnRaceDef,
                spawnFaction = spawnFaction,
                spawnMap = spawnMap,
                desiredSpawnCell = desiredSpawnCell,
                spawnSettings = spawnSettings?.Clone() ?? new CharacterSpawnSettings()
            };
        }
    }
}
