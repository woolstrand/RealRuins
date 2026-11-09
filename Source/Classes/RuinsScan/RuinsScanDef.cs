using RimWorld;
using UnityEngine;
using Verse;

namespace RealRuins {
    // One way of searching for ruins (1.7/Defs/RuinsScan/RuinsScan.xml): searching old records at research benches,
    // radio location at the comms console and scanners. Buildings pick theirs in CompProperties_RuinsScanner.scanDef.
    // label is the toggle label, description its tooltip ({0} min skill, {1} and {2} mean days at max and min skill).
    public class RuinsScanDef : Def {
        public JobDef job;
        public WorkGiverDef workGiver;
        public QuestScriptDef questScript;

        // Message on discovery: {PAWN} the pawn, {BUILDING} the building label
        [MustTranslate]
        public string foundMessage;

        public int minIntellectualSkill = 6;
        // Mean scanning time before a discovery: mtbDaysAtMinSkill for a pawn at minIntellectualSkill,
        // going down linearly to mtbDaysAtMaxSkill at maxSkill. Days of actual scanning work, not calendar days.
        public float mtbDaysAtMinSkill = 5f;
        public float mtbDaysAtMaxSkill = 2f;
        public int maxSkill = 20;
        public float xpPerTick = 0.1f;

        // Blueprint requirements, same as the random pristine ruins quest
        public int minimumWealth = 50000;
        public int minimumArea = 6000;
        public int maxBlueprintAttempts = 50;

        public float MtbDaysFor(Pawn pawn) {
            int level = pawn.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0;
            float t = Mathf.InverseLerp(minIntellectualSkill, maxSkill, level);
            return Mathf.Lerp(mtbDaysAtMinSkill, mtbDaysAtMaxSkill, t);
        }

        public AcceptanceReport PawnCanScan(Pawn pawn) {
            SkillRecord skill = pawn.skills?.GetSkill(SkillDefOf.Intellectual);
            if (skill == null || skill.TotallyDisabled) {
                return "RealRuins_RuinsScan_IncapableOfIntellectual".Translate(pawn.Named("PAWN"));
            }
            if (skill.Level < minIntellectualSkill) {
                return "RealRuins_RuinsScan_SkillTooLow".Translate(minIntellectualSkill);
            }
            return true;
        }

        public override System.Collections.Generic.IEnumerable<string> ConfigErrors() {
            foreach (string error in base.ConfigErrors()) {
                yield return error;
            }
            if (job == null || workGiver == null || questScript == null) {
                yield return "job, workGiver and questScript are required";
            }
        }
    }
}
