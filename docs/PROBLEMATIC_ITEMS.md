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

## Related, already noted elsewhere

- `CleaningStation` NRE in `MakeThingFromItemTile` (2026-10-29, macOS) - see the skill's `NOTES.md`; not
  analysed yet.
