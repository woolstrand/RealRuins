using RimWorld;

using Verse;


namespace RealRuins {
    // Retired in 1.6+: the RealRuins_AbandonedBase quest replaced it. The Quest_PristineRuins def is kept with baseChance 0
    // so saves (storyteller history) and translations still resolve. Firing it by hand (Debug actions -> Do incident)
    // spawns nothing and only points to the quest. Ruins it spawned in older saves live on their own (AbandonedBaseWorldObject).
    public class IncidentWorker_RuinsFound : IncidentWorker {

        protected override bool TryExecuteWorker(IncidentParms parms) {
            Find.LetterStack.ReceiveLetter("RealRuins.PristineRuinsIncidentRetired.Label".Translate(), "RealRuins.PristineRuinsIncidentRetired.Text".Translate(), LetterDefOf.NeutralEvent);
            return true;
        }
    }
}
