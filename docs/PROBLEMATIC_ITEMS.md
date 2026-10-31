# Problematic items

This file collects **every discovered spawn problem**: things from blueprints that fail to spawn,
spawn wrong, or spam the log while being restored. Add an entry whenever a test session (see
`.claude/skills/run-realruins/`) or a user report turns up a new one.

**Only open problems live here.** When a problem is fixed (and verified), delete its entry - don't mark
it fixed. The fix's history belongs in the commit message and git log, not in this file. If a fix covers
only part of an entry (e.g. one of several sub-problems), delete just that part.

## Spawn philosophy

Blueprints come from other players' bases, built with any mix of mods, any game version. The goal of the
mod is **not** to spawn everything - it is to spawn **as much as possible, in a universal way, gracefully
ignoring errors**:

- If something can be spawned, it is spawned.
- If it can't, it is discarded - or replaced with something generic (a fallback material, a generic pawn
  kind, a plain wall) when that keeps the ruin recognizable.
- One bad item, pawn, hediff or cell must never break the rest of the ruin, the map generation, or the
  game. Failures are caught as close to the failing piece as possible.
- Prefer a silent check (`GetNamedSilentFail`, null checks) before handing data to vanilla code, so missing
  mod content is skipped *quietly* instead of throwing or flooding the log with vanilla errors.
- Universal over special-cased: fix the class of problem (missing def, missing body, missing class), not
  one mod's defName. Per-def blacklists are a last resort.

## Entry format

Per problem: what was seen, where in the code it fails, what the player gets now, and a direction for a
universal fix. Include the blueprint id when known - blueprints live in the local cache
(`RealRuins/<GUID>.bp`, gzipped XML).

---

## 2. Missing modded wall replaced by `Wall` keeps a non-stuff material

**Seen:** 2026-10-30, Windows, RimWorld 1.7.4990, entering a Pristine Ruins site; blueprint
`ECC47DFE-044C-4542-91D9-488EF507D622` (250x214), made with Vanilla Factions Expanded - Medieval, not loaded in the test game.

**Blueprint content:** 11 `<item def="VFEM_CobblestoneWall" stuffDef="ChunkGranite" actsAsWall="1" />`.

**Log, per ruins map:** 11x `ERROR [Verse] MakeThing error: Tried to make Wall from ChunkGranite which is not a stuff. Assigning default.`
from `ThingMaker.MakeThing` ← `BlueprintTransferUtility.MakeThingFromItemTile`.

**Where:** `BlueprintLoader` replaces an unavailable wall-like item with vanilla `Wall` but keeps its `stuffDef`.
`MakeThingFromItemTile` only checks that the stuff def exists, not that it is a valid stuff for the thing, so vanilla logs an
error and picks its default. The wall still spawns (vanilla default material).

**Fix direction (universal):** in `MakeThingFromItemTile`, treat a stuff the thing can't be made of
(`!stuff.IsStuff || !stuff.stuffProps.CanMake(thing)`) like a missing one: walls → `BlocksGranite`, others →
`GenStuff.DefaultStuffFor`. Fix applied 2026-10-30, not verified in-game yet.

---

## 3. Bookcase contents added before the bookcase is spawned

**Seen:** 2026-10-30, Windows, RimWorld 1.7.4990, new game on a planetary POI (seed `writer`, attack mode).

**Log:** `[BlueprintTransfer]: Failed to spawn item BookcaseSmall because of System.NullReferenceException` at
`Building_Bookcase.Notify_ItemAdded` ← `ThingOwner.TryAdd` ← `BlueprintTransferUtility.MakeThingFromItemTile`.

**Where:** `MakeThingFromItemTile` puts the blueprint's inner items into the container while the bookcase is not yet
spawned. `Building_Bookcase.Notify_ItemAdded` uses `MapHeld.listerHaulables`, and `MapHeld` is null for an unspawned
thing. The whole bookcase is discarded.

**Fix direction (universal):** fill containers after the container is spawned. Or catch a failing inner `TryAdd` per item
and keep the container, so one bad content item doesn't drop the whole thing.

---

## 4. Minified moisture pump ticks while unspawned

**Seen:** 2026-10-30, same session as #3 (as a failed post-spawn tick); 2026-10-31, Windows, RimWorld 1.7.4990 with ~50 mods,
blueprint `E43CD04F-2A28-4E83-A0CF-D099C03D7D3C` (as an error while the ruin is being played).

**Log:** `[BlueprintTransfer]: Exception while tried to perform tick for MinifiedThing of cost 0, retrhrowing...` during
transfer, and during play `ERROR [Verse] Exception ticking MinifiedThing<id> (at ...): NullReferenceException` from
`CompTerrainPumpDry.AffectCell` ← `CompTerrainPump.CompTickRare` ← `ThingWithComps.TickRare` ← `Thing.DoTick` (x11 in 45 s).

**Where:** a minified building made straight from the blueprint was never spawned, so comps that are set up in
`PostSpawnSetup` stay empty. `CompTerrainPump.Working` treats a null `powerComp` as "working", so the inner pump ticks inside
its crate and touches a null map. A pump the player minified was spawned once, so vanilla never hits this.

**Fix direction (universal):** don't let the inner thing of a minified thing tick, or initialise its comps the way a spawn
would. The error repeats on every rare tick while the crate exists, so it is more than a single line of noise.

---

## 6. Rimefeller well heads fail in the post-spawn tick

**Seen:** 2026-10-31, Windows, RimWorld 1.7.4990, entering Pristine Ruins sites with Rimefeller loaded; 7 of 30 random ruins.
Blueprints `D0D50647-393E-47AB-8C9A-BF4ADA5900E7`, `F40ABA1D-624C-4335-AD5E-38328BCC022F`, `D277BC13-A088-42BD-9FAD-84BDF563C9FB`
(`OilWellHead`), `E10CD2B6-D508-4949-9DA3-E733914CFE7F`, `F7808E0B-A7FC-496C-B50A-0401B9740838`,
`E43CD04F-2A28-4E83-A0CF-D099C03D7D3C` (`DeepOilWellHead`).

**Log:** `Exception while tried to perform tick for DeepOilWellHead of cost 54, retrhrowing...` then `WARN ... Failed to spawn
item DeepOilWellHead<id> of cost 54 because of exception System.NullReferenceException` with
`Rimefeller.Building_WellHead.TickRare()` ← `BlueprintTransferUtility.Transfer`. The well head is destroyed right after
spawning; the rest of the map is fine.

**Where:** `Transfer` runs one `DoTick`/`TickLong`/`TickRare` on every spawned thing during map generation. The well head's
`TickRare` needs Rimefeller map state (oil grid / deposits) that isn't there yet, or at all, in a ruin.

**Fix direction:** the tick exists to settle items (rot etc.). Restrict it to items, or on a tick exception keep the building
instead of destroying it - it may tick fine once the map is finalized. Both change behaviour, so decide first.

---

## 7. Rimatomics reactor core fails in `SpawnSetup`

**Seen:** 2026-10-31, Windows, RimWorld 1.7.4990, Rimatomics loaded; blueprints `E6F75BFA-5362-4433-A8D8-3AE39B2AB63C`
(`ReactorCoreA`), `F7808E0B-A7FC-496C-B50A-0401B9740838` and `E43CD04F-2A28-4E83-A0CF-D099C03D7D3C` (`ReactorCoreC`).

**Log:** `WARN ... Failed to spawn item ReactorCoreC<id> of cost 4268 because of exception System.ArgumentOutOfRangeException:
Index was out of range` from `List.get_Item` ← `Rimatomics.Item_NuclearFuel.UpdateFuelRefs(reactorCore)` <-
`reactorCore.RefreshFuelGrid` ← `reactorCore.SpawnSetup` ← `GenSpawn.Spawn` ← `BlueprintTransferUtility.Transfer`.

**Where:** the core is spawned with the fuel restored from the blueprint as its contents; on spawn Rimatomics indexes its
fuel slot list with values the restored fuel carries, and they don't match. The whole core (cost ~4k) is lost.

**Fix direction (universal):** the same as #3 - when spawning a container with restored contents fails, retry it empty
(contents dropped or placed next to it), so the container itself survives.

---

## 8. Dubs Bad Hygiene pipe overlaps a DBH building's pipe cell

**Seen:** 2026-10-31, Windows, RimWorld 1.7.4990, Dubs Bad Hygiene loaded; blueprint `E4790701-7001-4170-B68A-FAA50C7D60DB`
(reproduced in single-file mode, always the same map cell).

**Log:** `ERROR [Verse] More than 1 pipe comp in the same cell at (107, 0, 87), things with pipes cannot overlap!` from
`DubsBadHygiene.HygienePipeMapComp.RegisterPipe` ← `CompPipe.PostSpawnSetup` ← `Building_Pipe.SpawnSetup` <-
`GenSpawn.Spawn` ← `BlueprintTransferUtility.Transfer`.

**Where:** not a duplicate pipe: the blueprint has no cell with two pipes, and skipping a building when the same def already
stands in the cell did not help (tried, reverted). The other pipe comp belongs to a (multi-cell) DBH building whose footprint
covers a pipe cell - allowed by the DBH version the blueprint was made with, rejected by the current one. Both things spawn;
it's log noise.

**Fix direction:** none generic and cheap found. If it matters: before spawning, check the occupied cells for a thing with a
comp of the same type (generic over comp types, not over DBH) and skip.

---

## 9. DBH fixtures are skipped by the "would destroy a DBH item" guard

**Seen:** 2026-10-31, Windows, RimWorld 1.7.4990, Dubs Bad Hygiene + Vanilla Furniture Expanded loaded; 5 of 30 random ruins.
Blueprints `CD858D3E-7829-4A99-B4FD-7D2D55F3B4B6`, `F40ABA1D-624C-4335-AD5E-38328BCC022F`, `E10CD2B6-D508-4949-9DA3-E733914CFE7F`,
`F7808E0B-A7FC-496C-B50A-0401B9740838`, `C6768B21-4724-4517-9512-2BA1BEED6512`.

**Log:** `WARN ... Failed to spawn item ToiletStuff<id> ... because of exception System.Exception: Can't spawn item because it
will destroy Dubs Bad Hygiene Item and it will lead to app freeze.` - also `WashingMachine`, `ShowerStuff`, `BathtubStuff`,
`BasinStuff`, `KitchenSink`, `HotTub`, `ToiletAdvStuff`, `ShowerAdvStuff`, `VFE_KitchenSinkCabinet`.

**Where:** `BlueprintTransferUtility.Transfer`, the wipe loop before `GenSpawn.Spawn` (added 2020, commit 442441b). The comment
says DBH items freeze the game when *removed* during mapgen, but the check tests the class of the **new** thing
(`thing.def.thingClass`), not of `existingItem`. So every DBH fixture that would wipe anything (a plant, a chunk, filth) is
dropped, while a non-DBH thing that wipes a DBH item is not stopped. It is also a mod-specific special case.

**Fix direction:** check `existingItem` instead (matches the comment), then re-test whether the old freeze comes back. Not
changed during the compatibility run because the original intent can't be confirmed from the code.

---

## 10. Name-based wall detection turns modded items into walls

**Seen:** 2026-10-31, blueprint `E43CD04F-2A28-4E83-A0CF-D099C03D7D3C`: `<item def="SpellOf_ArcaneWallC" stackCount="4" />`
(an item from a magic mod that wasn't loaded) is spawned as a vanilla `Wall`. The stack count error this caused is fixed
(wall/door replacements now reset the stack count); the item still becomes a wall.

**Where:** `Tiles.cs` marks any def whose name contains "wall" as `isWall`; `BlueprintLoader` then replaces it with `Wall`
when the def is missing.

**Fix direction:** trust the blueprint's `actsAsWall` attribute and fall back to the name only for old blueprints without it,
or ignore the name for items that carry a `stackCount` (a real wall never does).

---

## Related, already noted elsewhere

- `CleaningStation` NRE in `MakeThingFromItemTile` (2026-10-29, macOS) - see the skill's `NOTES.md`; not
  analysed yet.
