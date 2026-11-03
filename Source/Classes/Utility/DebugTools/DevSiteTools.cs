using System;
using System.Collections.Generic;
using System.Linq;

using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealRuins {
    // A world object which the dev Explore and Reset buttons work on (planetary POI, pristine ruins).
    interface IDevExplorableSite {
        // What a caravan arriving at the site does (generates the map and enters it)
        CaravanArrivalAction DevArrivalAction();
        // The site as it is now, restored by DevRestoreState after Reset
        object DevSaveState();
        // saved is null if the site was entered without Explore: then the site goes back to its state before arrival as far as known
        void DevRestoreState(object saved);
    }

    // Dev mode buttons on ruins sites: Explore enters the site at once with a random caravan, Reset removes the map and everything
    // the player has there and puts the site back as it was before Explore. Saves keep no trace of them.
    static class DevSiteTools {
        // Site state before Explore, by world object ID. Not saved: after a reload Reset falls back to DevRestoreState(null).
        private static readonly Dictionary<int, object> savedStates = new Dictionary<int, object>();

        public static IEnumerable<Gizmo> Gizmos(MapParent site) {
            if (!Prefs.DevMode || !(site is IDevExplorableSite explorable)) yield break;

            Command_Action explore = new Command_Action {
                defaultLabel = "DEV: Explore",
                defaultDesc = "Enter the site at once with a random caravan, generating the map.",
                action = () => Explore(site, explorable),
            };
            if (site.HasMap) explore.Disable("The site has a map already. Reset it first.");
            yield return explore;

            yield return new Command_Action {
                defaultLabel = "DEV: Reset",
                defaultDesc = "Remove the site map, every player pawn on it and player caravans on its tile, and put the site back as it was before Explore.",
                action = () => Reset(site, explorable),
            };
        }

        private static void Explore(MapParent site, IDevExplorableSite explorable) {
            savedStates[site.ID] = explorable.DevSaveState();
            List<Pawn> pawns = RandomCaravanPawns();
            Caravan caravan = CaravanMaker.MakeCaravan(pawns, Faction.OfPlayer, site.Tile, addToWorldPawnsIfNotAlready: true);
            explorable.DevArrivalAction().Arrived(caravan);
            //the arrival generates the map in a long event, this one runs after it
            LongEventHandler.QueueLongEvent(() => {
                Pawn pawn = pawns.FirstOrDefault(p => p.Spawned);
                if (pawn != null) CameraJumper.TryJump(pawn);
            }, "GeneratingMapForNewEncounter", false, null);
        }

        // Like vanilla's Spawn random caravan, without animals and inventory: armed colonists only
        private static List<Pawn> RandomCaravanPawns() {
            List<ThingDef> weapons = DefDatabase<ThingDef>.AllDefs.Where(def => def.IsWeapon && !def.weaponTags.NullOrEmpty()
                && (def.weaponTags.Contains("SimpleGun") || def.weaponTags.Contains("IndustrialGunAdvanced") || def.weaponTags.Contains("SpacerGun")
                    || def.weaponTags.Contains("MedievalMeleeAdvanced") || def.weaponTags.Contains("NeolithicRangedDecent"))).ToList();
            List<Pawn> pawns = new List<Pawn>();
            int count = Rand.RangeInclusive(3, 6);
            for (int i = 0; i < count; i++) {
                Pawn pawn = PawnGenerator.GeneratePawn(Faction.OfPlayer.def.basicMemberKind, Faction.OfPlayer);
                if (!pawn.WorkTagIsDisabled(WorkTags.Violent) && weapons.TryRandomElement(out ThingDef weapon)) {
                    pawn.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(weapon, GenStuff.RandomStuffFor(weapon)));
                }
                pawns.Add(pawn);
            }
            return pawns;
        }

        private static void Reset(MapParent site, IDevExplorableSite explorable) {
            foreach (Caravan caravan in Find.WorldObjects.Caravans.Where(c => c.Tile == site.Tile && c.Faction == Faction.OfPlayer).ToList()) {
                List<Pawn> caravanPawns = caravan.PawnsListForReading.ToList();
                caravan.RemoveAllPawns();
                foreach (Pawn pawn in caravanPawns) pawn.Destroy();
                if (!caravan.Destroyed) caravan.Destroy();
            }

            if (site.HasMap) {
                Map map = site.Map;
                //player pawns would be passed to the world as lost colonists
                foreach (Pawn pawn in map.mapPawns.PawnsInFaction(Faction.OfPlayer).ToList()) {
                    pawn.Destroy();
                }
                Current.Game.DeinitAndRemoveMap(map, false);
            }

            savedStates.TryGetValue(site.ID, out object saved);
            savedStates.Remove(site.ID);
            explorable.DevRestoreState(saved);
            CameraJumper.TryJump(site);
            Messages.Message("RealRuins: " + site.LabelCap + " reset" + (saved == null ? " (not entered with Explore: reverted as far as known)" : ""), site, MessageTypeDefOf.NeutralEvent, historical: false);
        }
    }
}
