using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using RimWorld;
using Verse;
using System.IO;
using RimWorld.BaseGen;
using UnityEngine;
using UnityEngine.Video;
using Verse.AI.Group;

namespace RealRuins
{



    class RuinsScatterer {
        private const long ticksInYear = 3600000;
        private static bool[,] cellUsed;

        public static void PrepareCellUsageFor(Map map) {
            cellUsed = new bool[map.Size.x, map.Size.z];
        }

        public static void FinalizeCellUsage() {
            cellUsed = null;
        }

        static private int totalWorkTime = 0;
        private float totalCost = 0;
        private ScatterOptions options;

 
        private int RandomInBounds(int span, int margin, int jitter) {
            if (span > (margin + jitter) * 2) {
                return Rand.Range(margin + jitter, span - margin - jitter);
            } else {
                return Rand.Range(span / 2 - jitter, span / 2 + jitter);
            }
        }

        private IntVec3 SelectRandomCenterInBounds(int width, int height, int radius, int jitter) {
            return new IntVec3(RandomInBounds(width, radius, jitter), 0, RandomInBounds(height, radius, jitter));
        }

        private double Distance(int x, int z, IntVec3 center) {
            return Math.Sqrt(Math.Pow(center.x - x, 2) + Math.Pow(center.z - z, 2));
        }

        private bool PointInsideCircle(int x, int z, IntVec3 center, int radius) {
            return Math.Pow(center.x - x, 2) + Math.Pow(center.z - z, 2) < Math.Pow(radius, 2);
        }

        

        Map map;
        IntVec3 targetPoint;

        int blueprintWidth;
        int blueprintHeight;
        Version blueprintVersion;

        private int snapshotYear;
        private int dateShift;

        int mapOriginX, mapOriginZ; //target coordinates for minX, minZ on the map

        int minRadius;
        int maxRadius;
        int referenceRadiusJitter;

        Faction faction;



        bool canHaveFood = false;

        float deteriorationDegree = 0;
        float scavengersActivity = 0; //depends on how far tile is from villages
        float elapsedTime = 0; //ruins age


        private bool LoadRandomXMLSnapshot() {

            int attemptNumber = 0;
            bool result = false;
            bool forceDelete = false;

            while (attemptNumber < 10 && result != true) {

                string snapshotName = SnapshotStoreManager.Instance.RandomSnapshotFilename();
                if (snapshotName == null) {
                    return false;
                }

                try {
                    //result = DoSanityCheckAndLoad(snapshotName);
                    forceDelete = false;
                } catch (Exception e) {
                    Debug.Message("Corrupted file, removing. Error: {0}", e.ToString());
                    forceDelete = true;
                }

                if (!result && (options.deleteLowQuality || forceDelete)) { //remove bad snapshots
                    Debug.Message("DELETING low quality file");
                    File.Delete(snapshotName);
                    string deflatedName = snapshotName + ".xml";
                    if (!File.Exists(deflatedName)) {
                        File.Delete(deflatedName);
                    }
                }

            }

            return result;
        }


/*
        private void AddSpecials() {
            //corpses, blood trails, mines and traps, bugs and bees
            //Pretty low chance to have someone's remainings
            for (int z = minZ; z < maxZ; z++) {
                for (int x = minX; x < maxX; x++) {


                    IntVec3 mapLocation = new IntVec3(x - minX + mapOriginX, 0, z - minZ + mapOriginZ);
                    if (!mapLocation.InBounds(map)) continue;

                    if (Rand.Value < options.decorationChance) {

                        bool canPlace = true;
                        List<Thing> things = map.thingGrid.ThingsListAt(mapLocation);
                        foreach (Thing t in things) {
                            if (t.def.fillPercent > 0.5) canPlace = false;
                        }
                        if (!canPlace) continue;

                        int timeOfDeath = Find.TickManager.TicksGame - (int)(Rand.Value * 100000000);
                        PawnGenerationRequest request = new PawnGenerationRequest(PawnKindDefOf.WildMan, null, PawnGenerationContext.NonPlayer, -1, false, false, false, false, true, false, 20f, false, true, true, false, false, false, false, null, null, null, null, null, null, null, null);
                        Pawn dweller = PawnGenerator.GeneratePawn(request);
                        GenSpawn.Spawn(dweller, mapLocation, map);
                        dweller.Kill(null);
                        CompRottable rottable = dweller.Corpse.TryGetComp<CompRottable>();
                        rottable.RotProgress = rottable.PropsRot.TicksToDessicated;
                        dweller.Corpse.timeOfDeath = timeOfDeath + (int)(Rand.Value * 100000);
                    } else if (wallMap[x, z] > 1 && Rand.Value < options.trapChance) { //spawn inside rooms only
                        ThingDef trapDef = ThingDef.Named("TrippingTrigger");
                        Thing thing = ThingMaker.MakeThing(trapDef);
                        GenSpawn.Spawn(thing, mapLocation, map);
                    }

                }
            }

    
            //enemies
            if (Rand.Chance(options.hostileChance)) {
                CellRect rect = new CellRect(mapOriginX, mapOriginZ, maxX - minX, maxZ - minZ);
                
                if (rect.minX < 15 || rect.minZ < 15 || rect.maxX > map.Size.x - 15 || rect.maxZ > map.Size.z - 15) {
                    return; //do not add enemies if we're on the map edge
                }

                if (!CellFinder.TryFindRandomCellInsideWith(rect, (IntVec3 x) => x.Standable(map) && wallMap[x.x - rect.minX + minX, x.z - rect.minZ + minZ] > 1, out IntVec3 testCell)) {
                    return; //interrupt if there are no closed cells available
                }

                PawnKindDef pawnKindDef = null;

                if (Rand.Chance(0.7f) && !options.shouldAddSignificantResistance) { //no animals in "significant resistance" scenario. Surely animals are not a significant resistance in sane amounts
                    //animals
                    pawnKindDef = map.Biome.AllWildAnimals.RandomElementByWeight((PawnKindDef def) => (def.RaceProps.foodType == FoodTypeFlags.CarnivoreAnimal || def.RaceProps.foodType == FoodTypeFlags.OmnivoreAnimal) ? 1 : 0);
                } else {
                    //mechanoids' kinds are selected for each unit
                }

                float powerMax = rect.Area / 30.0f;
                float powerThreshold = (Math.Abs(Rand.Gaussian(0.5f, 1)) * powerMax) + 1;
                

                Debug.Message("Gathering troops power of {0} (max was {1})", powerThreshold, powerMax);

                float cumulativePower = 0;

                Faction faction = Faction.OfAncientsHostile;
                
                Lord lord = LordMaker.MakeNewLord(lordJob: new LordJob_DefendPoint(rect.CenterCell), faction: faction, map: map, startingPawns: null);
                int tile = map.Tile;

                while (cumulativePower <= powerThreshold) { 

                    PawnKindDef currentPawnKindDef = pawnKindDef;
                    if (currentPawnKindDef == null) {
                        currentPawnKindDef = (from kind in DefDatabase<PawnKindDef>.AllDefsListForReading
                                              where kind.RaceProps.IsMechanoid
                                              select kind).RandomElementByWeight((PawnKindDef kind) => 1f / kind.combatPower);
                    }

                    PawnGenerationRequest request =
                        new PawnGenerationRequest(currentPawnKindDef, faction, PawnGenerationContext.NonPlayer, tile, true, false, false, //allowDead is last
                        false, true, false, 1f,
                        false, true, true, false,
                        false, false, false,
                        null, null, null, null,
                        null, null, null, null);

                    if (CellFinder.TryFindRandomCellInsideWith(rect, (IntVec3 x) => x.Standable(map) && wallMap[x.x - rect.minX + minX, x.z - rect.minZ + minZ] > 1, out IntVec3 cell)) {
                        Pawn pawn = PawnGenerator.GeneratePawn(request);

                        FilthMaker.MakeFilth(cell, map, ThingDefOf.Filth_Blood, 5);
                        GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);

                        lord.AddPawn(pawn);
                        cumulativePower += pawn.kindDef.combatPower;

                        Debug.Message("Adding combat power for {0}, total is {1}", currentPawnKindDef.defName, cumulativePower);
                    } else {
                        break; //no more suitable cells
                    }
                }
            }
        }

    */


        private void RestoreDefencesAndPower() {
            foreach (var thing in map.spawnedThings) {
                if (thing.TryGetComp<CompPowerPlant>() != null || thing.TryGetComp<CompPowerBattery>() != null || (thing.def.building != null && thing.def.building.IsTurret)) {
                    CompBreakdownable bdcomp = thing.TryGetComp<CompBreakdownable>();
                    if (bdcomp != null) {
                        bdcomp.Notify_Repaired();
                    }
                }
            }
        }

        private void AddRaidTriggers() {
            int addedTriggers = 0;
            float ratio = 10;
            float remainingCost = totalCost * (Rand.Value + 0.5f); //cost estimation as seen by other factions
            
            float initialCost = remainingCost;

            int triggersAbsoluteMaximum = 100;

            Debug.Message("Triggers number: {0}. Cost: {1}. Base max points: {2} (absolute max in x2)", 0, remainingCost, 0);


            while (remainingCost > 0) {

                IntVec3 mapLocation = CellRect
                    .CenteredOn(map.Center, (int) (Math.Sqrt(blueprintHeight * blueprintWidth) / 4.0f)).RandomCell;

                
                if (!mapLocation.InBounds(map)) continue;
                
                ThingDef raidTriggerDef = ThingDef.Named("RaidTrigger");
                RaidTrigger trigger = ThingMaker.MakeThing(raidTriggerDef) as RaidTrigger;

                if (options.allowFriendlyRaids) {
                    if (Rand.Chance(0.2f)) {
                        trigger.faction = Find.FactionManager.RandomNonHostileFaction();
                    } else {
                        trigger.faction = Find.FactionManager.RandomEnemyFaction();
                    }
                } else {
                    trigger.faction = Find.FactionManager.RandomEnemyFaction();
                }

                int raidMaxPoints = (int)(remainingCost / ratio);
                trigger.value = Math.Abs(Rand.Gaussian()) * raidMaxPoints + Rand.Value * raidMaxPoints + 250.0f;
                if (trigger.value > 10000) trigger.value = Rand.Range(8000, 11000); //sanity cap. against some beta-poly bases.
                remainingCost -= trigger.value * ratio;
                
                Debug.Message("Added trigger at {0}, {1} for {2} points, remaining cost: {3}", mapLocation.x, mapLocation.z, trigger.value, remainingCost);
                
                GenSpawn.Spawn(trigger, mapLocation, map);
                addedTriggers++;

                options.uncoveredCost = Math.Abs(remainingCost);

                if (addedTriggers > triggersAbsoluteMaximum) {
                    if (remainingCost < initialCost * 0.2f) {
                        if (Rand.Chance(0.1f)) {
                            if (remainingCost > 100000) {
                                remainingCost = Rand.Range(80000, 110000);
                            }
                            return;
                        }
                    }
                }

            }

            

    
        }



        //Deterioration degree is unconditional modifier of destruction applied to the ruins bluepring. Degree of 0.5 means that in average each 2nd block in "central" part will be destroyed.
        //Scavenge threshold is an item price threshold after which the item or terrain is most likely scavenged.
        public void ScatterRuinsAt(IntVec3 loc, Map map, ScatterOptions options) {

            DateTime start = DateTime.Now;

            options.deteriorationMultiplier = 0;

            targetPoint = loc;
            this.map = map;
            this.options = options;


            minRadius = options.minRadius;
            maxRadius = options.maxRadius;
            scavengersActivity = Rand.Value * options.scavengingMultiplier + (options.scavengingMultiplier) / 3;
            elapsedTime = (Rand.Value * options.scavengingMultiplier) * 3 + ((options.scavengingMultiplier > 0.95) ? 3 : 0);
            referenceRadiusJitter = (minRadius + (maxRadius - minRadius) / 2) / 10;

            deteriorationDegree = options.deteriorationMultiplier;

            Debug.Message("Scattering ruins at ({0}, {1}) of radius from {2} to {5}. scavengers activity: {3}, age: {4}", loc.x, loc.z, minRadius, scavengersActivity, elapsedTime, maxRadius);
            //cut and deteriorate:
            // since the original blueprint can be pretty big, you usually don't want to replicate it as is. You need to cut a small piece and make a smooth transition

            //Debug.Message("Loading snapshot...");
            if (!LoadRandomXMLSnapshot()) {
                return; //no valid files to generate ruins.
            }
            /*
                        //Debug.Message("Finding rooms...");
                        if (options.shouldCutBlueprint) {
                            FindRoomsAndConstructIntegrityMaps();
                        } else {
                            UntouchedIntegrityMapConstructor();
                        }


                        //Debug.Message("Processing items...");
                        ProcessItems();
                        //Debug.Message("Deteriorating...");
                        Deteriorate();
                        //Debug.Message("Scavenging...");
                        RaidAndScavenge();
                        //Debug.Message("Transferring blueprint...");
                        TransferBlueprint();
                        //Debug.Message("Adding filth and rubble...");
                        AddFilthAndRubble();
                        //Debug.Message("Adding something special...");
                        AddSpecials();
                        //Debug.Message("Ready");
                        UpdateUsedCells();

                        if (options.shouldKeepDefencesAndPower) {
                            RestoreDefencesAndPower();
                        }

                        if (options.shouldAddRaidTriggers) {
                            AddRaidTriggers();
                        }

                        options.roomMap = wallMap;
                        options.bottomLeft = new IntVec3(mapOriginX, 0, mapOriginZ);
                        options.blueprintRect = new CellRect(mapOriginX, mapOriginZ, maxX - minX, maxZ - minZ);

            //            RoofCollapseCellsFinder.RemoveBulkCollapsingRoofs(new List<IntVec3>{ targetPoint }, map);
                        RoofCollapseCellsFinder.CheckCollapseFlyingRoofs(new CellRect(minX, minZ, maxX - minX, maxZ - minZ), map, true);

                        TimeSpan span = DateTime.Now - start;
                        totalWorkTime += (int)span.TotalMilliseconds;
                        Debug.Message("Added ruins for {0} seconds, total: {1} msec", span.TotalSeconds, totalWorkTime);
                        */
            throw new Exception("ruins scatterer is out of order!");
        }
    }
}
