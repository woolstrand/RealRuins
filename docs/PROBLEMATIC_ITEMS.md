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

## 4. MinifiedThing fails in the post-spawn tick

**Seen:** 2026-10-30, same session as #3.

**Log:** `[BlueprintTransfer]: Exception while tried to perform tick for MinifiedThing of cost 0, retrhrowing...` followed
by `Failed to spawn item MinifiedThing5391 of cost 0 because of exception Object reference not set to an instance of an object.`
from `BlueprintTransferUtility.Transfer`. No inner stack trace is logged, so the failing member is unknown. "Cost 0"
suggests the minified thing has no (valid) inner thing.

**Fix direction:** log the inner exception's stack, then check minified things with a missing or invalid inner thing before
spawning them and skip them quietly.

---

## 5. Restored pawn gets an injury on a part that can't be hit

**Seen:** 2026-10-30, Windows, new game on a planetary POI (seed `persecution`, attack mode).

**Log:** `ERROR [Verse] Added injury to Waist but it should be impossible to hit it. pawn=Eminent dinfo=null` from
`Hediff_Injury.PostAdd` ← `Pawn_HealthTracker.AddHediff` ← `PawnRestoreUtility.RestorePawnHealth`.

**Where:** `RestorePawnHealth` re-adds the blueprint's injuries to whatever part they name. Vanilla flags injuries on parts
whose body part def can't be hit (Waist is a "virtual" part). The pawn spawns; the error is log noise.

**Fix direction (universal):** before adding an injury, skip it when the target part can't be hit (check the vanilla
condition `Hediff_Injury.PostAdd` uses), or move it to the parent part.

---

## 6. Rimefeller `OilWellHead` fails in the post-spawn tick

**Seen:** 2026-10-31, Windows, RimWorld 1.7.4990, entering a Pristine Ruins site; blueprint
`D0D50647-393E-47AB-8C9A-BF4ADA5900E7` (Rimefeller loaded). Same pattern as #4.

**Log:** `Exception while tried to perform tick for OilWellHead of cost 54, retrhrowing...` then `WARN ... Failed to spawn
item OilWellHead<id> of cost 54 because of exception Object reference not set to an instance of an object.` from
`BlueprintTransferUtility.Transfer`. The well head is destroyed right after spawning and the rest of the map is fine.
Likely the well head expects a well/oil deposit or Rimefeller map component state that a ruin doesn't have.

**Fix direction:** same as #4 - log the inner stack first. If post-spawn ticking of buildings regularly fails for modded
content, consider whether the tick is needed at all for non-item things (it exists to settle items, e.g. rot).

---

## Related, already noted elsewhere

- `CleaningStation` NRE in `MakeThingFromItemTile` (2026-10-29, macOS) - see the skill's `NOTES.md`; not
  analysed yet.
