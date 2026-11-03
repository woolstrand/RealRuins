# Scattering — Map Generation Hooks

> **AI AGENT NOTE**: If you change how blueprints are placed on maps, add new scatter modes, modify the proximity system, or change POI generation logic, update this file.

## Overview

"Scattering" is how blueprints end up on generated maps. Every path ends in one scatterer, `RuinsScatterer.Scatter`;
what differs is the genstep around it:

1. **Standard scatter** (`GenStep_ScatterRealRuins`) — several ruin chunks on any map built from vanilla `MapCommonBase`
   (patched in by `1.7/Patches/MapGenerator_Patch.xml`).
2. **Site scatter** (`GenStep_RealRuinsSite`, abstract) — one blueprint is the whole map. Subclasses only build a `SitePlan`:
   - `GenStep_ScatterLargeRealRuins` — `AbandonedBase` (Pristine ruins event and quest)
   - `GenStep_ScatterMediumRealRuins` — caravan small ruins (`CaravanSmallRuinsWorldObject`)
   - `GenStep_ScatterPOIRuins` — planetary POIs (`RealRuinsPOIWorldObject`)

**Files:**
- `Source/Classes/Scattering/RuinsScatterer.cs`
- `Source/Classes/Scattering/ScatterOptions.cs`, `ScatterOptionsFactory.cs`, `ScatterContext.cs`, `ScatterState.cs`, `PipelineSettings.cs`
- `Source/Classes/Scattering/GenStep_ScatterRealRuins.cs` (also `GenStep_ScatterLargeRealRuins`, `GenStep_ScatterMediumRealRuins`)
- `Source/Classes/Scattering/GenStep_RealRuinsSite.cs` (`GenStep_RealRuinsSite`, `SitePlan`)
- `Source/Classes/DynamicMapObjects/GenStep_ScatterPOIRuins.cs`

---

## Options, context, state

- `ScatterOptions` — what to generate. User-tunable fields are persisted via `ExposeData()` as the settings template
  (`RealRuins_ModSettings.defaultScatterOptions`); the rest are internal and set per scatterer.
- `ScatterOptionsFactory` — the only place where a scatterer's options are built (`ForGenericRuins`, `ForCaravanSmallRuins`,
  `ForAbandonedBase`, `ForPOI`, `ForAnalyzer`). Each call returns a fresh object, never linked to the settings store.
- `PipelineSettings` — global mod options applied the same way everywhere (blacklists, fallback material, force multiplier).
- `ScatterContext` — one per `Scatter` call: a private copy of the options, the settings, a fresh `ScatterState`, an
  optional `preloadedBlueprint` and `wealthAllowance` (-1 = no limit, see Ruins wealth per map).
- `ScatterState` — data produced by the run (`roomMap`, `uncoveredCost`, `transferredCost`, `placementRect`), read by force
  generators and the wealth budget.

---

## RuinsScatterer

`Scatter(ResolveParams rp, ScatterContext context, CoverageMap coverage, List<AbstractDefenderForcesGenerator> generators)`.
`rp` is input only (placement hint and faction).

### Pipeline (in order)
1. Blueprint: `context.preloadedBlueprint` if set; else `options.blueprintFileName` if loadable; else
   `BlueprintFinder.FindRandomBlueprintWithParameters()` with the options' minimum area / density / cost, 15 attempts.
2. Load: whole blueprint or `RandomPartCenteredAtRoom()` depending on `shouldLoadPartOnly`; size = `Rand.Range(minRadius, maxRadius)` square.
3. `CutIfExceedsBounds`, `BlueprintPreprocessor.ProcessBlueprint()`, `FindRooms()` (→ `state.roomMap`)
4. `BlueprintTransferUtility`: `RemoveIncompatibleItems()`, `UpdateBlueprintStats(true)`
5. `DeteriorationProcessor.Process()`, `ScavengingProcessor.RaidAndScavenge()`
6. `btu.Transfer(coverage)`
7. `GenerateForces()` for each generator
8. `btu.AddFilthAndRubble()` if `shouldAddFilth`

Steps 6–8 are in independent `try/catch` blocks. The pipeline modifies the blueprint in place, so one loaded blueprint feeds one run.

### Why site blueprints are loaded twice
A site's blueprint is picked when the site is created (event, quest, POI generation) but the map is generated when the player
visits it — maybe days later, after a game restart. Only the file name is saved (`RuinedBaseComp.blueprintFileName`, POI comp);
the blueprint is loaded again during map generation. Saving the blueprint would add megabytes of XML per site to the save,
and keeping it in memory until the visit costs a lot of RAM for one load behind the loading screen. Within one map generation a
blueprint is never loaded twice: `SitePlan.blueprint` / `ScatterContext.preloadedBlueprint` pass it on (used by POIs).

---

## GenStep_ScatterRealRuins

### Guards
- Stored blueprint count < 10
- Starting map, if `startWithoutRuins`
- A `RealRuinsPOIWorldObject` or `AbandonedBaseWorldObject` on the tile (the map is a site itself). Caravan small ruins maps
  are **not** skipped on purpose: they get regular chunks around the main ruin.
- Water-covered tile

### Proximity (`enableProximity`)
`CalculateDistanceToNearestSettlement()` flood-fills the world grid up to 16 tiles: non-player settlement = its distance,
site = twice its distance; a settlement or site on the tile itself = 1 (largest chunks); nothing found = 16.
- Distance ≥ 16: 50% chance of no chunks.
- Chunk size: `ScaleCurve(d) = exp(1 / (d/5 + 0.5)) - 0.3`, side kept within 6–60.
- Chunk count: `DensityCurve(max(d, 4)) / countNormalization`, with `DensityCurve(d) = exp(1 / (d/10 + 0.3)) - 0.7`.
  Distances below 4 count as 4: players can't settle next to a settlement and rarely settle closer than 4 tiles.
  A settlement or site on the tile itself (faction base attack, quest site) keeps multiplier 1, so it isn't buried in ruins.
- `countNormalization` (≈2.02) is the average extra coverage (count × chunk area) the curves give over distances 4–16, so the
  covered area averaged over those distances matches the time proximity affected chunk size only. Default settings, 250x250 map:
  ≈1.7× chunks at ≤4 tiles, ≈1× at 8, ≈0.5× at 16.
- Scavenging and deterioration use the raw `DensityCurve(d)` and `ScaleCurve(d)`.

### Chunk count
`(int)(mapArea / 10000) × Rand.Range(1, 2) × user density × count multiplier`, no upper limit: high density settings
produce dense "cities".

Each chunk gets its own `ScatterContext`, a random `RandomNotEdgeCell(10)` position and, with `hostileChance`, animal (80%)
or mechanoid (20%) defenders. The game is paused while scattering.

### Ruins wealth per map
`RealRuins_ModSettings.ruinsWealthPerMap` (default 15,000; slider on the map generation page, 100,000 = no limit) is the
market value all chunks of a map may share. `RuinsWealthBudget` gives each chunk, in order, what is left minus a reserve of
`min(500, limit / chunks / 2)` for every chunk still to come, and is charged with what the chunk actually transferred
(`ScatterState.transferredCost`). `ScavengingProcessor.CutTo` runs after the normal raids when a blueprint is over its
allowance: it takes the most valuable things first (by `OwnCost`, ignoring weight), with the same removal as raids (contents out
of containers, cheap door/wall replacements). The same cut serves the abandoned base and ruined POI cost caps, which dismantle
walls instead (see 03-blueprint-pipeline). Only regular scattered ruins are limited; abandoned bases, POIs and the main ruin
of the caravan event are not.

Options next to the slider:
- `ruinsWealthScaleWithProximity` (default off): the limit is multiplied by the proximity count multiplier, the same factor as the
  chunk count (x1.72 at 4 tiles or closer, x0.49 at 16+), so each chunk keeps about the same share. Off is the lore-friendly
  default: denser ruins near settlements share one limit and are more picked over. Needs `enableProximity`.
- `ruinsWealthIgnoreStructure` (default on): `Tile.IsBasicStructure` (walls, doors and floors worth at most
  `Tile.basicStructureMaxCost` = 50 per tile: granite wall ~8, wooden door ~32; golden walls, silver floors, autodoors still count)
  doesn't count toward the limit and is never cut for it. Without it, dense maps ran out of budget and late chunks were cut to
  bare walls, whose own value still pushed the map over the limit.
- `ruinsWealthCountDamage` (default off): the budget is charged with the full-price estimate (repairing is free, a damaged thing
  often works the same). On: with the live market value of what spawned (`StatPart_Health` lowers it for damage, quality changes it).

What a chunk is charged with is `ScatterState.transferredCost` (`BlueprintTransferUtility.WealthLimitCost`); pawns never count.
The cost cap of abandoned bases / ruined POIs always counts everything.

Measured on 50 sampled maps (Sample ruins wealth debug action, 2026-11-03): median ruins wealth 3k per map, p90 ~10k; before the
limit the maximum was 33k, with the limit 15k. The limit only touched the last chunks of maps already close to it.

## Tech level cap
Settings tab "Tech Level": master toggle `techLimitEnabled`, `techLimitMaxLevel` (Neolithic–Ultra; `TechLevel` is a game enum,
mods can't add levels), `techLimitStrict` and three groups: `techLimitRuins` (`ForGenericRuins`), `techLimitPOIs` (`ForPOI` and
`ForAnalyzer`), `techLimitEvents` (`ForAbandonedBase`, `ForCaravanSmallRuins`). `ScatterOptionsFactory.ApplyTechLimit` copies
them into `ScatterOptions.maxTechLevel` (Undefined = no cap) and `techLimitStrict`.

`TechLevelFilter` does the work in `BlueprintPreprocessor.ProcessBlueprint`, so the analyzer sees the same filtered blueprint as
the scatterer and costs are estimated after the filter. A def's tech level (cached) is the highest of: its own `techLevel`, its
`researchPrerequisites`, its `recipeMaker` research, the research of the easiest standalone recipe making it (modded fabricated
materials, chemfuel), and the own `techLevel` of its `costList` ingredients (one level deep, which catches buildings made of
components). The stuff counts too. Undefined everywhere means it always stays: mined or found materials (plasteel, sky steel).
- Items above the cap are removed; walls and doors are replaced with a granite wall / wooden door so rooms stay closed.
- Contents of containers are filtered at any depth (an emptied minified thing is not spawned); terrain too.
- Restored pawns skip apparel above the cap (`PawnRestoreUtility`); pawn races themselves are not filtered.
- Limited strategy: a thing `n` levels above the cap stays with chance `TechLevelFilter.limitedKeepChance ^ n` (0.1). It is a roll
  on each pass, so the analyzer and the visit see different results; nothing about it is stored.
- POI factions (`RealRuinsPOIFactory.CommitPOI`): with the POI group on, the faction's tech level is capped too (limited strategy:
  except with `limitedKeepChance`), and the type's minimum faction tech level is lowered to the cap.
- Not covered: defender forces (mechanoids in regular ruins, raid parties, battle royale) come from the world's factions as they are.

---

## GenStep_RealRuinsSite

Common flow for one-blueprint maps:
1. Pause, `MakePlan(map)` (subclass). A null plan skips generation.
2. BaseGen globals (`map`, `mainRect = plan.rect`), `PushExtraSymbols(map, rp, plan)` (virtual, subclass)
3. `RuinsScatterer.Scatter` — always, even without generators
4. Push `chargeBatteries`, `ensureCanHoldRoof` (unless disabled), `refuel`; `BaseGen.Generate()` (LIFO: extra symbols resolve last)
5. `GenerateStartingParty()` for each generator — done outside BaseGen on purpose (pawn placement issues)
6. `Finish(map)` (subclass), also on failure

### `SitePlan`
`options`, `faction`, `rect`, optional `blueprint` (preloaded), `generators` (null/empty = no defenders),
`ensureCanHoldRoof`, `turretOperators` (POI). Data only; behavior lives in the genstep's virtual methods
(`MakePlan`, `PushExtraSymbols`, `Finish`).

### `GenStep_ScatterLargeRealRuins` (abandoned base)
Blueprint name, cost cap and raiders activity from `RuinedBaseComp`; `ForAbandonedBase` options; faction ancients hostile;
`BattleRoyaleForcesGenerator` unless the force multiplier is 0.

### `GenStep_ScatterMediumRealRuins` (caravan small ruins)
Random blueprint via `ForCaravanSmallRuins` (min cost 25k, area 4000); no `ensureCanHoldRoof`. In `PushExtraSymbols`, with
`0.5 × threatScale` chance pushes a `pawnGroup` raid of a random enemy faction (`|gaussian| × 500 × threatScale` points).

### `GenStep_ScatterPOIRuins` (planetary POI)
Context: `PlanetaryRuinsInitData.shared.startingPOI` (new-game settle flow) or the map parent's `RealRuinsPOIComp`.
Loads the blueprint itself (options depend on its size/origin) and passes it in the plan.

| Mode | Faction | Deterioration | HP | Filth |
|------|---------|---------------|-----|-------|
| `normal` (ruined) | null / neutral | enabled + random costCap | reduced | yes |
| `takeover` | player | disabled | full HP | no |
| `attack` | hostile | disabled | full HP | no |

Forces (`GeneratorsForBlueprint()`): ruins / no faction — 20% each for animals, mechanoids or hostile citizens; military POI —
`MilitaryForcesGenerator(militaryPower)`; civilian POI — `CitizenForcesGeneration(bedsCount)` + `MilitaryForcesGenerator(3)` for
expensive bases. Hostile factions also get turret operators (`plan.turretOperators = mannableCount × 1.25 + 1`, pushed in `PushExtraSymbols` as
`pawn` symbols with `LordJob_ManTurrets`).
`Finish` clears `PlanetaryRuinsInitData.shared`.

---

## XML Def Registration

- `1.7/Defs/MapGeneratorDefs/MapGenerators_RealRuins.xml` — genstep defs (`ScatterRealRuins`, `ScatterMediumRealRuins`,
  `ScatterBaseRuins`, `ScatterPOIRuins`) and the `RealRuinsPOI` / `RealRuinsAbandonedBase` map generators
- `1.7/Defs/MapGeneratorDefs/Ruins_EncounterMapGenerator.xml` — `CaravanRuinsMapGenerator`
- `1.7/Defs/MapGeneratorDefs/Rules.xml` — symbol resolution rules for BaseGen
- `1.7/Patches/MapGenerator_Patch.xml` — adds `ScatterRealRuins` to vanilla `MapCommonBase`
