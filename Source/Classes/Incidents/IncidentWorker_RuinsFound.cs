using RimWorld;
using RimWorld.Planet;

using Verse;

using System.Collections.Generic;
using System.Linq;


namespace RealRuins {
    public class IncidentWorker_RuinsFound : IncidentWorker {

        protected override bool CanFireNowSub(IncidentParms parms) {
            if (!base.CanFireNowSub(parms)) {
                return false;
            }

            if (!SnapshotStoreManager.Instance.CanFireLargeEvent()) {
                return false;
            }

            int tile;
            Faction faction;

            return Find.FactionManager.RandomNonHostileFaction(false, false, false, TechLevel.Undefined) != null && TryFindTile(out tile) && SiteMakerHelper.TryFindRandomFactionFor(DefDatabase<SiteCoreDef>.GetNamed("RuinedBaseSite"), null, out faction, true, null);
        }

        protected override bool TryExecuteWorker(IncidentParms parms) {
            Faction faction = parms.faction;
            if (faction == null) {
                faction = Find.FactionManager.RandomNonHostileFaction(false, false, false, TechLevel.Undefined);
            }
            if (faction == null) {
                return false;
            }
            if (!TryFindTile(out int tile)) {
                return false;
            }
            if (!SiteMakerHelper.TryFindSiteParams_SingleSitePart(DefDatabase<SiteCoreDef>.GetNamed("RuinedBaseSite"), (string)null, out SitePartDef sitePart, out Faction faction2, null, true, null)) {
                return false;
            }

            int randomInRange = SiteTuning.QuestSiteTimeoutDaysRange.RandomInRange;
            Site site = CreateSite(tile, sitePart, randomInRange, faction2);


            string letterText = GetLetterText(faction, site.GetComponent<TimeoutComp>().TicksLeft / 60000);
            Find.LetterStack.ReceiveLetter(def.letterLabel, letterText, def.letterDef, site, faction, null);
            return true;
        }

        private bool TryFindTile(out int tile) {
            IntRange itemStashQuestSiteDistanceRange = new IntRange(5, 30);
            return TileFinder.TryFindNewSiteTile(out tile, itemStashQuestSiteDistanceRange.min, itemStashQuestSiteDistanceRange.max, false, true, -1);
        }

        public static Site CreateSite(int tile, SitePartDef sitePart, int days, Faction siteFaction) {
            Site site = SiteMaker.MakeSite(DefDatabase<SiteCoreDef>.GetNamed("RuinedBaseSite"), sitePart, tile, siteFaction, true, null);
            site.sitePartsKnown = true;
            site.GetComponent<TimeoutComp>().StartTimeout(days * 60000);

            string filename = null;
            BlueprintFinder.FindRandomBlueprintWithParameters(out filename, 6400, 0.1f, 100000);

            Debug.Message("Trying to gt comp");
            RuinedBaseComp comp = site.GetComponent<RuinedBaseComp>();
            if (comp == null) {
                Debug.Message("Component is null");
            } else {
                comp.blueprintFileName = filename;
            }

            Debug.Message("Found blueprint with name {0} and stored", filename);
            Find.WorldObjects.Add(site);
            return site;
        }

        private string GetLetterText(Faction alliedFaction, int timeoutDays) {
            string text = string.Format(def.letterText, alliedFaction.leader.LabelShort, alliedFaction.def.leaderTitle, alliedFaction.Name, timeoutDays).CapitalizeFirst();
            return text;
        }
    }
}