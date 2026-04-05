using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace CharacterStudio.Core
{
    public static class CharacterDefinitionApplier
    {
        public static void ApplyToPawn(Pawn pawn, CharacterDefinition? definition)
        {
            if (pawn == null || definition == null)
            {
                return;
            }

            ApplyBasicIdentity(pawn, definition);
            ApplyBackstories(pawn, definition);
            ApplyTraits(pawn, definition);
            ApplySkills(pawn, definition);
            ApplyStartingApparel(pawn, definition);
        }

        private static void ApplyBasicIdentity(Pawn pawn, CharacterDefinition definition)
        {
            if (pawn.ageTracker != null)
            {
                pawn.ageTracker.AgeBiologicalTicks = (long)Math.Max(0f, definition.biologicalAge * 3600000f);
                pawn.ageTracker.AgeChronologicalTicks = (long)Math.Max(pawn.ageTracker.AgeBiologicalTicks, definition.chronologicalAge * 3600000f);
            }

            if (!string.IsNullOrWhiteSpace(definition.displayName))
            {
                string cleanName = definition.displayName.Trim();
                pawn.Name = new NameSingle(cleanName);
            }

            if (pawn.story != null)
            {
                if (definition.gender != Gender.None)
                {
                    pawn.gender = definition.gender;
                }

                if (!string.IsNullOrWhiteSpace(definition.bodyTypeDefName))
                {
                    BodyTypeDef? bodyType = DefDatabase<BodyTypeDef>.GetNamedSilentFail(definition.bodyTypeDefName);
                    if (bodyType != null)
                    {
                        pawn.story.bodyType = bodyType;
                    }
                }

                if (!string.IsNullOrWhiteSpace(definition.headTypeDefName))
                {
                    HeadTypeDef? headType = DefDatabase<HeadTypeDef>.GetNamedSilentFail(definition.headTypeDefName);
                    if (headType != null)
                    {
                        pawn.story.headType = headType;
                    }
                }

                if (!string.IsNullOrWhiteSpace(definition.hairDefName))
                {
                    HairDef? hairDef = DefDatabase<HairDef>.GetNamedSilentFail(definition.hairDefName);
                    if (hairDef != null)
                    {
                        pawn.story.hairDef = hairDef;
                    }
                }
            }

            if (pawn.genes != null && !string.IsNullOrWhiteSpace(definition.xenotypeDefName))
            {
                XenotypeDef? xenotype = DefDatabase<XenotypeDef>.GetNamedSilentFail(definition.xenotypeDefName);
                if (xenotype != null)
                {
                    pawn.genes.SetXenotype(xenotype);
                }
            }
        }

        private static void ApplyBackstories(Pawn pawn, CharacterDefinition definition)
        {
            if (pawn.story == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(definition.childhoodBackstoryDefName))
            {
                BackstoryDef? childhood = DefDatabase<BackstoryDef>.GetNamedSilentFail(definition.childhoodBackstoryDefName);
                if (childhood != null)
                {
                    pawn.story.Childhood = childhood;
                }
            }

            if (!string.IsNullOrWhiteSpace(definition.adulthoodBackstoryDefName))
            {
                BackstoryDef? adulthood = DefDatabase<BackstoryDef>.GetNamedSilentFail(definition.adulthoodBackstoryDefName);
                if (adulthood != null)
                {
                    pawn.story.Adulthood = adulthood;
                }
            }
        }

        private static void ApplyTraits(Pawn pawn, CharacterDefinition definition)
        {
            if (pawn.story?.traits == null)
            {
                return;
            }

            pawn.story.traits.allTraits.RemoveAll(static trait => trait != null);
            foreach (string traitDefName in definition.traitDefNames ?? new List<string>())
            {
                TraitDef? traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(traitDefName);
                if (traitDef != null)
                {
                    try
                    {
                        pawn.story.traits.GainTrait(new Trait(traitDef));
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[CharacterStudio] 应用 Trait 失败: {traitDefName} ({ex.Message})");
                    }
                }
            }
        }

        private static void ApplySkills(Pawn pawn, CharacterDefinition definition)
        {
            if (pawn.skills == null || definition.skills == null)
            {
                return;
            }

            foreach (CharacterSkillEntry entry in definition.skills)
            {
                SkillDef? skillDef = DefDatabase<SkillDef>.GetNamedSilentFail(entry.skillDefName);
                if (skillDef == null)
                {
                    continue;
                }

                SkillRecord? record = pawn.skills.GetSkill(skillDef);
                if (record == null)
                {
                    continue;
                }

                record.Level = Math.Max(0, Math.Min(20, entry.level));
                record.passion = entry.passion;
            }
        }

        private static void ApplyStartingApparel(Pawn pawn, CharacterDefinition definition)
        {
            if (pawn.apparel == null || definition.startingApparelDefNames == null)
            {
                return;
            }

            List<Apparel> currentApparel = pawn.apparel.WornApparel?.ToList() ?? new List<Apparel>();
            foreach (Apparel apparel in currentApparel)
            {
                pawn.apparel.TryDrop(apparel, out _, pawn.PositionHeld, forbid: false);
            }

            foreach (string apparelDefName in definition.startingApparelDefNames)
            {
                ThingDef? apparelDef = DefDatabase<ThingDef>.GetNamedSilentFail(apparelDefName);
                if (apparelDef == null || !typeof(Apparel).IsAssignableFrom(apparelDef.thingClass))
                {
                    continue;
                }

                if (ThingMaker.MakeThing(apparelDef) is Apparel apparel)
                {
                    pawn.apparel.Wear(apparel, dropReplacedApparel: false, locked: false);
                }
            }
        }
    }
}
