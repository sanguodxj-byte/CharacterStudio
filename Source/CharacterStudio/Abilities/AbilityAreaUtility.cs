using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace CharacterStudio.Abilities
{
    /// <summary>
    /// 技能范围 / 目标辅助工具。
    /// 统一 CharacterStudio 对 areaShape / areaCenter / irregular pattern 的运行时展开逻辑，
    /// 供 runtime ability 主链路与热键 fallback 共用，避免两套行为不一致。
    /// </summary>
    public static class AbilityAreaUtility
    {
        public static IntVec3 ResolveImpactCenter(Pawn caster, ModularAbilityDef ability, LocalTargetInfo target)
        {
            if (caster == null)
            {
                return IntVec3.Invalid;
            }

            AbilityTargetType normalizedTarget = ModularAbilityDefExtensions.NormalizeTargetType(ability);
            if (normalizedTarget == AbilityTargetType.Self)
            {
                return caster.Position;
            }

            if (target.IsValid)
            {
                return target.Cell;
            }

            return caster.Position + caster.Rotation.FacingCell;
        }

        public static IEnumerable<IntVec3> BuildResolvedTargetCells(Pawn caster, ModularAbilityDef ability, LocalTargetInfo target)
        {
            Map? map = caster?.Map;
            if (caster == null || ability == null || map == null)
            {
                return Enumerable.Empty<IntVec3>();
            }

            IntVec3 impactCenter = ResolveImpactCenter(caster, ability, target);
            if (!impactCenter.IsValid)
            {
                return Enumerable.Empty<IntVec3>();
            }

            if (!ability.useRadius)
            {
                return impactCenter.InBounds(map)
                    ? new[] { impactCenter }
                    : Enumerable.Empty<IntVec3>();
            }

            return BuildAbilityAreaCells(caster, ability, impactCenter);
        }

        public static IEnumerable<IntVec3> BuildAbilityAreaCells(Pawn caster, ModularAbilityDef ability, IntVec3 impactCenter)
        {
            Map? map = caster?.Map;
            if (caster == null || ability == null || map == null)
            {
                return Enumerable.Empty<IntVec3>();
            }

            IntVec3 anchor = ModularAbilityDefExtensions.NormalizeAreaCenter(ability) == AbilityAreaCenter.Self
                ? caster.Position
                : impactCenter;
            IntVec3 forward = GetFacingDirection(caster.Position, impactCenter, caster.Rotation.FacingCell);
            IntVec3 right = GetRight(forward);
            AbilityAreaShape shape = ModularAbilityDefExtensions.NormalizeAreaShape(ability);
            float rawRadius = shape == AbilityAreaShape.Irregular
                ? Mathf.Max(0f, ability.radius)
                : Mathf.Max(ability.radius, 1f);
            int discreteRadius = Mathf.Max(1, Mathf.CeilToInt(rawRadius));
            var cells = new HashSet<IntVec3>();

            switch (shape)
            {
                case AbilityAreaShape.Line:
                    for (int step = 0; step <= discreteRadius; step++)
                    {
                        cells.Add(anchor + forward * step);
                    }
                    break;
                case AbilityAreaShape.Cone:
                    for (int distance = 0; distance <= discreteRadius; distance++)
                    {
                        IntVec3 rowCenter = anchor + forward * distance;
                        int halfWidth = distance;
                        for (int side = -halfWidth; side <= halfWidth; side++)
                        {
                            cells.Add(rowCenter + right * side);
                        }
                    }
                    break;
                case AbilityAreaShape.Cross:
                    cells.Add(anchor);
                    for (int step = 1; step <= discreteRadius; step++)
                    {
                        cells.Add(anchor + forward * step);
                        cells.Add(anchor - forward * step);
                        cells.Add(anchor + right * step);
                        cells.Add(anchor - right * step);
                    }
                    break;
                case AbilityAreaShape.Square:
                    for (int forwardOffset = -discreteRadius; forwardOffset <= discreteRadius; forwardOffset++)
                    {
                        for (int sideOffset = -discreteRadius; sideOffset <= discreteRadius; sideOffset++)
                        {
                            cells.Add(anchor + forward * forwardOffset + right * sideOffset);
                        }
                    }
                    break;
                case AbilityAreaShape.Irregular:
                    bool addedIrregular = false;
                    foreach (IntVec3 cell in BuildIrregularPatternCells(anchor, forward, right, ability.irregularAreaPattern))
                    {
                        cells.Add(cell);
                        addedIrregular = true;
                    }

                    if (!addedIrregular)
                    {
                        cells.Add(anchor);
                    }
                    break;
                default:
                    foreach (IntVec3 cell in GenRadial.RadialCellsAround(anchor, rawRadius, true))
                    {
                        cells.Add(cell);
                    }
                    break;
            }

            return cells.Where(c => c.InBounds(map));
        }

        public static IEnumerable<Thing> EnumerateDistinctThingsInCells(Map map, IEnumerable<IntVec3> cells, Thing? excludeThing = null)
        {
            if (map == null || cells == null)
            {
                yield break;
            }

            HashSet<Thing> yielded = new HashSet<Thing>();
            foreach (IntVec3 cell in cells)
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                List<Thing> thingList = cell.GetThingList(map);
                for (int i = 0; i < thingList.Count; i++)
                {
                    Thing thing = thingList[i];
                    if (thing == null || thing == excludeThing || !yielded.Add(thing))
                    {
                        continue;
                    }

                    yield return thing;
                }
            }
        }

        private static IEnumerable<IntVec3> BuildIrregularPatternCells(IntVec3 anchor, IntVec3 forward, IntVec3 right, string? pattern)
        {
            string patternText = pattern ?? string.Empty;
            if (string.IsNullOrWhiteSpace(patternText))
            {
                yield break;
            }

            string normalized = patternText.Replace("\r", string.Empty).Replace("/", "\n");
            string[] rows = normalized
                .Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(row => row.Trim())
                .Where(row => row.Length > 0)
                .ToArray();
            if (rows.Length == 0)
            {
                yield break;
            }

            int rowCenter = rows.Length / 2;
            int maxWidth = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                string rowText = rows[i] ?? string.Empty;
                if (rowText.Length > maxWidth)
                {
                    maxWidth = rowText.Length;
                }
            }

            int colCenter = maxWidth / 2;

            for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                string row = rows[rowIndex] ?? string.Empty;
                for (int colIndex = 0; colIndex < row.Length; colIndex++)
                {
                    if (!IsFilledPatternCell(row[colIndex]))
                    {
                        continue;
                    }

                    int forwardOffset = rowCenter - rowIndex;
                    int sideOffset = colIndex - colCenter;
                    yield return anchor + forward * forwardOffset + right * sideOffset;
                }
            }
        }

        private static bool IsFilledPatternCell(char token)
        {
            return token == '1' || token == 'x' || token == 'X' || token == '#';
        }

        private static IntVec3 GetFacingDirection(IntVec3 origin, IntVec3 target, IntVec3 fallback)
        {
            IntVec3 delta = target - origin;
            if (delta == IntVec3.Zero)
            {
                return fallback;
            }

            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
            {
                return delta.x >= 0 ? IntVec3.East : IntVec3.West;
            }

            return delta.z >= 0 ? IntVec3.North : IntVec3.South;
        }

        private static IntVec3 GetRight(IntVec3 forward)
        {
            if (forward == IntVec3.North)
            {
                return IntVec3.East;
            }
            if (forward == IntVec3.South)
            {
                return IntVec3.West;
            }
            if (forward == IntVec3.East)
            {
                return IntVec3.South;
            }
            return IntVec3.North;
        }
    }
}