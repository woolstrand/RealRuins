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

## 1. Pawns of missing modded races: `Android4Tier` body / unresolvable pawn kinds

**Seen:** 2026-10-29, Windows, RimWorld 1.7.4990, entering a Pristine Ruins site;
blueprint `EEDE39B6-1AEF-46A0-A306-F6728D0729B3` (216x191, cost ~684k), made with an Androids mod that
is not loaded in the test game.

**Blueprint content:** ~170 pawns (mostly as corpses): 99 `MicroScyther`, 29 `AndroidT1SlaveFactionSpecific`,
14 `AndroidT1RaiderFactionSpecific`, 13 `AndroidT2RaiderFactionSpecific`, 9 `AndroidT3RaiderFactionSpecific`,
plus a few animals. 1469 hediff body-part references carry `<body>Android4Tier</body>`. Android pawns
have first/last names (`<name first="Cole" last="#113" nick="Cole" />`), MicroScythers have only `first`.

**Log, per ruins map (one generation):**

| count | line |
|---|---|
| 1455 | `ERROR [Verse] Could not load reference to Verse.BodyDef named Android4Tier` |
| 101 | `[BlueprintPawnDecoder]: Pawn decoding failed: System.NullReferenceException` |
| 101 | `[BlueprintTransfer]: Failed to spawn item corpse because of System.NullReferenceException` |
| 45 | `ERROR [Verse] Tried to restore null body part.` |

These are really three problems:

### 1a. Missing pawn kind without a triple name → `GeneratePawn(null)`

`PawnRestoreUtility.MakePawnWithRawXml` (`Source/Classes/Scattering/Internal/PawnRestoreUtility.cs`):
if `PawnKindDef` isn't found, it falls back to `Villager` **only** when the name has first+last. Otherwise
`kindDef` stays null and goes straight into `PawnGenerator.GeneratePawn(kindDef, faction)`:

```
Pawn decoding failed: System.NullReferenceException
  at Verse.PawnGenerationRequest.AlwaysDownedLifeStages(String& stages)
  at Verse.PawnGenerationRequest.ValidateAndFix()
  at Verse.PawnGenerationRequest..ctor(PawnKindDef kind, ...)
  at Verse.PawnGenerator.GeneratePawn(PawnKindDef kindDef, Faction faction, Nullable`1 tile)
  at RealRuins.PawnRestoreUtility.MakePawnWithRawXml(String xml)
```

Caught, returns null - pawn discarded, which is the intended outcome, but via an exception. Almost certainly
the 99 `MicroScyther` (single-name, modded) make up most of the 101.

**Fix direction:** `if (kindDef == null) return null;` (with a `Debug.Extra` line) before `GeneratePawn`.
Optionally, for non-humanlike unknown kinds, a generic animal corpse is *not* worth it - discard.

### 1b. Corpse branch doesn't check the inner pawn for null

`BlueprintTransferUtility.MakeThingFromItemTile`, `corpse` branch: after 1a returns null it calls
`Find.WorldPawns.PassToWorld(p)` and then `p.Corpse` unconditionally:

```
Failed to spawn item corpse because of System.NullReferenceException
  at RimWorld.Planet.WorldPawns.PassToWorld(Pawn pawn, PawnDiscardDecideMode discardMode)
  at RealRuins.BlueprintTransferUtility.MakeThingFromItemTile(...)
```

Caught by the outer try - corpse discarded. **Fix direction:** `if (p == null) return null;` right after
creating the inner pawn.

### 1c. Hediffs on a body that doesn't exist → vanilla error spam

Android pawns *do* have a triple name, so 1a falls back to `Villager` and they are generated as humans.
`RestorePawnHealth` then deserializes their hediffs with `ScribeExtractor.SaveableFromNode<Hediff>`.
`ValidateAndFixHediffNode` checks the hediff def, the hediff class and the **part** def (`BodyPartDef`),
but not the part's **body** (`<part><body>Android4Tier</body>...`). Vanilla `Scribe_BodyParts.Look` then
resolves `BodyDef Android4Tier`, fails, and logs a vanilla `ERROR` (not an exception, so nothing catches it):

```
ERROR [Verse] Could not load reference to Verse.BodyDef named Android4Tier
  at Verse.ScribeExtractor.DefFromNode(XmlNode subNode)
  at Verse.ScribeExtractor.BodyPartFromNode(XmlNode node, String label, BodyPartRecord defaultValue)
  at Verse.Scribe_BodyParts.Look(BodyPartRecord& part, String label, BodyPartRecord defaultValue)
  at Verse.Hediff.ExposeData()
  at RealRuins.PawnRestoreUtility.RestorePawnHealth(XmlNode root, Pawn p)
```

The part comes back null; some hediffs then hit `Tried to restore null body part` (45x, from
`Pawn_HealthTracker.RestorePart`). The pawn still spawns (as a human with partial/odd injuries).

**Fix direction (universal):** in `ValidateAndFixHediffNode`, if `part/body` is present and is not the
generated pawn's body (`pawn.RaceProps.body.defName`): rewrite it to the pawn's body if that body has a
part with the same `BodyPartDef` (and a valid index), otherwise skip the hediff. This handles every
modded race that falls back to a vanilla kind, not just androids.

---

## Related, already noted elsewhere

- `CleaningStation` NRE in `MakeThingFromItemTile` (2026-10-29, macOS) - see the skill's `NOTES.md`; not
  analysed yet.
