using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using Verse.AI;
using Verse.AI.Group;
using Verse;

namespace RealRuins {
    class GenStep_ScatterPOIRuins : GenStep {
        private ScatterOptions currentOptions;

        public override int SeedPart {
            get {
                return 74293949;
            }
        }


        public override void Generate(Map map, GenStepParams parms) {
            Find.TickManager.Pause();
            //Debug.Message("Overridden LARGE generate");

            RealRuinsPOIComp poiComp = map.Parent.GetComponent<RealRuinsPOIComp>();
            string filename = SnapshotStoreManager.Instance.SnapshotNameFor(poiComp.blueprintName, poiComp.gameName);
            Debug.Message("Preselected file name is {0}", filename);
            Debug.Message("Location is {0} {1}", poiComp.originX, poiComp.originZ);

            currentOptions = RealRuins_ModSettings.defaultScatterOptions.Copy(); //store as instance variable to keep accessible on subsequent ScatterAt calls

            currentOptions.minRadius = 400;
            currentOptions.maxRadius = 400;
            currentOptions.scavengingMultiplier = 0.1f;
            currentOptions.deteriorationMultiplier = 0.0f;
            currentOptions.hostileChance = 1.0f;


            currentOptions.blueprintFileName = filename;
            currentOptions.costCap = -1;
            currentOptions.startingPartyPoints = -1;
            currentOptions.minimumCostRequired = 0;
            currentOptions.minimumDensityRequired = 0.0f;
            currentOptions.minimumAreaRequired = 0;
            currentOptions.deleteLowQuality = false; //do not delete since we have much higher requirements for base ruins
            currentOptions.shouldKeepDefencesAndPower = true;
            currentOptions.shouldLoadPartOnly = false;
            currentOptions.shouldAddRaidTriggers = Find.Storyteller.difficulty.allowBigThreats;
            currentOptions.claimableBlocks = false;
            currentOptions.enableDeterioration = false;

            
            currentOptions.overridePosition = new IntVec3(poiComp.originX, 0, poiComp.originZ);


            ResolveParams resolveParams = default(ResolveParams);
            BaseGen.globalSettings.map = map;
            resolveParams.SetCustom<ScatterOptions>(Constants.ScatterOptions, currentOptions);
            resolveParams.faction = Find.FactionManager.OfAncientsHostile;
            resolveParams.rect = new CellRect(currentOptions.overridePosition.x, currentOptions.overridePosition.z, map.Size.x, map.Size.z);
            BaseGen.symbolStack.Push("scatterRuins", resolveParams);


            BaseGen.globalSettings.mainRect = resolveParams.rect;

            float uncoveredCost = currentOptions.uncoveredCost;
            if (uncoveredCost < 0) {
                if (Rand.Chance(0.5f)) {
                    uncoveredCost = -uncoveredCost; //adding really small party
                }
            }


            //adding starting party
            //don't doing it via basegen because of uh oh i don't remember, something with pawn location control
/*
            if (uncoveredCost > 0 || currentOptions.startingPartyPoints > 0) {
                float pointsCost = 0;
                if (currentOptions.startingPartyPoints > 0) {
                    pointsCost = currentOptions.startingPartyPoints;
                } else {
                    pointsCost = uncoveredCost / 10.0f;
                    FloatRange defaultPoints = new FloatRange(pointsCost * 0.7f,
                        Math.Min(12000.0f, pointsCost * 2.0f));
                    Debug.Message("Adding starting party. Remaining points: {0}. Used points range: {1}",
                        currentOptions.uncoveredCost, defaultPoints);

                }
                pointsCost *= Find.Storyteller.difficulty.threatScale;
                ScatterStartingParties((int)pointsCost, currentOptions.allowFriendlyRaids, map);

            }
            */
            BaseGen.symbolStack.Push("chargeBatteries", resolveParams);
            BaseGen.symbolStack.Push("refuel", resolveParams);

            BaseGen.Generate();


        }

        private void ScatterStartingParties(int points, bool allowFriendly, Map map) {

            while (points > 0) {
                int pointsUsed = Rand.Range(200, Math.Min(3000, points / 5));

                IntVec3 rootCell = CellFinder.RandomNotEdgeCell(30, map);
                CellFinder.TryFindRandomSpawnCellForPawnNear(rootCell, map, out IntVec3 result);
                if (result.IsValid) {
                    Faction faction = null;
                    if (allowFriendly) {
                        Find.FactionManager.TryGetRandomNonColonyHumanlikeFaction(out faction, false);
                    } else {
                        faction = Find.FactionManager.RandomEnemyFaction();
                    }

                    if (faction == null) faction = Find.FactionManager.AllFactions.RandomElement();

                    SpawnGroup(pointsUsed, new CellRect(result.x - 10, result.z - 10, 20, 20), faction, map);
                    points -= pointsUsed;
                }
            }
        }

        private void SpawnGroup(int points, CellRect locationRect, Faction faction, Map map) {
            PawnGroupMakerParms pawnGroupMakerParms = new PawnGroupMakerParms();
            pawnGroupMakerParms.groupKind = PawnGroupKindDefOf.Combat;
            pawnGroupMakerParms.tile = map.Tile;
            pawnGroupMakerParms.points = points;
            pawnGroupMakerParms.faction = faction;
            pawnGroupMakerParms.generateFightersOnly = true;
            pawnGroupMakerParms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
            pawnGroupMakerParms.forceOneIncap = false;
            pawnGroupMakerParms.seed = Rand.Int;

            List<Pawn> pawns = PawnGroupMakerUtility.GeneratePawns(pawnGroupMakerParms).ToList();
            CellRect rect = locationRect;

            Debug.Message("Rect: {0}, {1} - {2}, {3}", rect.BottomLeft.x, rect.BottomLeft.z, rect.TopRight.x, rect.TopRight.z);

            if (pawns == null) {
                Debug.Message("Pawns list is null");
            }

            foreach (Pawn p in pawns) {

                bool result = CellFinder.TryFindRandomSpawnCellForPawnNear(locationRect.RandomCell, map, out IntVec3 location);

                if (result) {
                    GenSpawn.Spawn(p, location, map, Rot4.Random);
                }
            }

            LordJob lordJob = null;
            //            if (Rand.Chance(0.7f) || ) {
            lordJob = new LordJob_AssaultColony(faction, canKidnap: false, canTimeoutOrFlee: Rand.Chance(0.5f));
            //           } else {
            //               lordJob = new LordJob_Steal();
            //           }

            if (lordJob != null) {
                LordMaker.MakeNewLord(faction, lordJob, map, pawns);
            }
        }
    }
}
