using System;

using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RealRuins {
    //Values are stored in the mod settings as RealRuins_ModSettings.caravanReformType (int): keep the numbers.
    enum CaravanReformMode {
        Automatic = 0, //reform is sent as regular caravan forming: pawns haul the loot and leave via the edge, also under fire
        Instant = 1,   //vanilla instant reform, only when no hostiles are around
        Manual = 2,    //no reforming until the raids are over; the map edge is the only way out meanwhile
    }

    // Everything the caravan reform mode decides on a pristine ruins (abandoned base) map. Used by the reform gizmo
    // (FormCaravanFromRuinsComp), the CanFormOrReformCaravanNow patch and the reform dialog patch.
    static class CaravanReformPolicy {
        public static CaravanReformMode Mode {
            get {
                int value = RealRuins_ModSettings.caravanReformType;
                return Enum.IsDefined(typeof(CaravanReformMode), value) ? (CaravanReformMode)value : CaravanReformMode.Automatic;
            }
        }

        // Manual mode: reforming is off while raids may still come (RuinedBaseComp.mapExitLocked).
        public static bool ReformLocked(MapParent site) {
            return Mode == CaravanReformMode.Manual && (site?.GetComponent<RuinedBaseComp>()?.mapExitLocked ?? false);
        }

        // Instant mode: vanilla instant reforming can't happen with hostiles around.
        public static bool ReformBlockedByHostiles(Map map) {
            return Mode == CaravanReformMode.Instant && GenHostility.AnyHostileActiveThreatToPlayer(map);
        }

        // Automatic and manual modes send the reform dialog as regular caravan forming instead of reforming instantly.
        public static bool ReformSendsFormingCaravan => Mode != CaravanReformMode.Instant;
    }
}
