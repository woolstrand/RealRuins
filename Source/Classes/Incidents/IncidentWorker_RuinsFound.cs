using RimWorld;
using RimWorld.Planet;

using Verse;

using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;


namespace RealRuins {
    // is not used anymore. will be kept for some time and removed later
    public class IncidentWorker_RuinsFound_OBSOLETE : IncidentWorker {

        protected override bool CanFireNowSub(IncidentParms parms) {
            if (!base.CanFireNowSub(parms)) {
                return false;
            }

            if (!SnapshotStoreManager.Instance.CanFireLargeEvent()) {
                return false;
            }

            return Find.FactionManager.RandomNonHostileFaction(false, false, false, TechLevel.Undefined) != null;
        }

        protected override bool TryExecuteWorker(IncidentParms parms) {
            Debug.Log(Debug.Event, "Starting incident worker for ruins major event");
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

            AbandonedBaseWorldObject worldObject = (AbandonedBaseWorldObject)WorldObjectMaker.MakeWorldObject(DefDatabase<WorldObjectDef>.GetNamed("AbandonedBase"));
            worldObject.Tile = tile;
            worldObject.SetFaction(null);
            Find.WorldObjects.Add(worldObject);
            Debug.Log(Debug.Event, "Created world object");

            string filename = null;
            Blueprint bp = BlueprintFinder.FindRandomBlueprintWithParameters(out filename, 6400, 0.01f, (int)Math.Min(30000, RealRuins_ModSettings.ruinsCostCap), maxAttemptsCount: 50);
            if (bp != null) {
                Debug.Log(Debug.Event, "Found suitable blueprint at path {0}", filename);
            } else {
                Debug.Warning(Debug.Event, "Could not found suitable blueprint!");
                return false;
            }

            RuinedBaseComp comp = worldObject.GetComponent<RuinedBaseComp>();
            if (comp == null) {
                Debug.Error(Debug.Event, "RuinedBase component is null during abandoned base event creation.");
                return false;
            } else {
                comp.blueprintFileName = filename;
                // Here we have to determine starting value. Ruins value will decrease over time (lore: scavenged by other factions)
                // However, we do not want it to be initially larger than total wealth cap.

                float costCap = RealRuins_ModSettings.ruinsCostCap;
                float startingCap = Math.Min(costCap, bp.totalCost);
                if (startingCap > int.MaxValue) {
                    startingCap = int.MaxValue; //not sure why StartScavenging takes int as input, but don't want to change it now.
                }

                Debug.Log(Debug.Event, "Initial cost set to {0} (blueprint cost {1}, settings cap {2}", startingCap, bp.totalCost, costCap);
                comp.StartScavenging((int)startingCap);
                // Start scavenging here means "store initial cap and begin counting down".
                // Actual scavenging will happen during mapgen step.
            }

            var lifetime = (int)(Math.Pow(worldObject.GetComponent<RuinedBaseComp>().currentCapCost / 1000, 0.41) * 1.1);
            string letterText = GetLetterText(faction, lifetime);
            Find.LetterStack.ReceiveLetter(def.letterLabel, letterText, def.letterDef, worldObject, faction, null);
            Debug.Log(Debug.Event, "Event preparations completed, blueprint is ready and stored, letter sent.");
            return true;
        }

        private bool TryFindTile(out int tile) {
            IntRange ruinsRange = new IntRange(5, 30);
            return TileFinder.TryFindNewSiteTile(out tile, ruinsRange.min, ruinsRange.max, false, TileFinderMode.Random, -1, false);
        }


        private string GetLetterText(Faction alliedFaction, int timeoutDays) {
            string text = string.Format(def.letterText, alliedFaction.leader.LabelShort, alliedFaction.def.leaderTitle, alliedFaction.Name, timeoutDays).CapitalizeFirst();
            return text;
        }
    }
}