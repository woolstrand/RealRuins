# Scattering — Map Generation Hooks

> **AI AGENT NOTE**: If you change how blueprints are placed on maps, add new scatter modes, modify the proximity system, or change POI generation logic, update this file.

## Overview

"Scattering" is how blueprints end up on generated maps. There are two distinct paths:
1. **Standard scatter** (`GenStep_ScatterRealRuins`) — places multiple small ruin chunks on any newly generated map. There actually are several classes intended for both scattering small ruins and scattering event ruins at a whole map scale.
2. **POI scatter** (`GenStep_ScatterPOIRuins`) — places one large blueprint when entering a `RealRuinsPOIWorldObject` or `AbandonedBaseWorldObject`

**Files:**
- `Source/Classes/Scattering/Constants.cs`
- `Source/Classes/Scattering/ScatterOptions.cs`
- `Source/Classes/Scattering/RuinsScatterer.cs`
- `Source/Classes/Scattering/GenStep_ScatterRealRuins.cs` (also contains `GenStep_ScatterLargeRealRuins`)
- `Source/Classes/DynamicMapObjects/GenStep_ScatterPOIRuins.cs`

---

## ScatterOptions

The central configuration object passed through the entire blueprint pipeline. Implements `IExposable` so a "settings template" instance can be persisted.

### Field Categories

**User-configured (persisted via `ExposeData()`):**
- `minRadius`, `maxRadius` — blueprint chunk size range
- `densityMultiplier` — how many chunks to place
- `deteriorationMultiplier` — decay intensity (0 = pristine, 1 = heavy decay)
- `scavengingMultiplier` — looting intensity
- `itemCostLimit` — per-item market value cap (1000 = no limit; sentinel)
- `costCap` — total blueprint value cap (used to limit ruin wealth)
- `hostileChance` — probability of hostile forces being generated
- `disableSpawnItems` — walls/doors only mode
- `wallsDoorsOnly` — alias for above
- `canHaveFood` — whether food items survive
- `shouldKeepDefencesAndPower` — preserve turrets, power
- `doorOwnershipProbability` — faction assignment probability for doors/walls
- `forceFullHitPoints` — spawn things at full HP (for takeover mode)
- `overwritesEverything` — whether to remove existing map objects first

**Runtime state (set during scatter, not persisted):**
- `bottomLeft` — map origin of placement
- `blueprintRect` — actual rect used
- `roomMap` — copy of `wallMap` after `FindRooms()`
- `blueprintFileName` — pre-selected file (quests, POIs); if null, random is chosen
- `overridePosition` — explicit placement position override
- `useGlobalOrigin` — use blueprint's original world coordinates

### Important: `ScatterOptions.Default` and `asIs()`

`ScatterOptions.Default` is a **shared mutable singleton instance**. The `asIs()` factory method **directly mutates `Default`** before returning it — this is a correctness bug if `asIs()` is ever called more than once or concurrently. Always create a copy via `Copy()` before modifying options.

The `Copy()` method performs a **shallow copy**. `roomMap` (2D array) and rect fields are copied by reference.

---

## RuinsScatterer

Static class with a single `Scatter(ScatterOptions options)` static method. Orchestrates the full processing pipeline for one blueprint placement.

### Pipeline (in order)
1. Blueprint resolution: use `options.blueprintFileName` if set; else `BlueprintFinder.FindRandomBlueprintWithParameters()`
2. Load: whole blueprint or `RandomPartCenteredAtRoom()` depending on `shouldLoadPartOnly`; radius = `Rand.Range(minRadius, maxRadius)`
3. `BlueprintPreprocessor.ProcessBlueprint()`
4. `blueprint.FindRooms()` + store `wallMap` in `options.roomMap`
5. Create `BlueprintTransferUtility`
6. `btu.RemoveIncompatibleItems()`
7. `blueprint.UpdateBlueprintStats(includeCost: false)` (fast, no market cost calc)
8. `DeteriorationProcessor.Process()`
9. `ScavengingProcessor.RaidAndScavenge()`
10. `btu.Transfer(coverageMap)`
11. `AbstractDefenderForcesGenerator.GenerateStartingParty()` (each generator)
12. `AbstractDefenderForcesGenerator.GenerateForces()` (each generator)
13. `btu.AddFilthAndRubble()`

**Error handling**: Steps 10–13 are each in independent `try/catch` blocks for partial-success resilience.

**Timing**: `DateTime.UtcNow` comparisons at each stage; logged at `Debug.Extra` level.

---

## GenStep_ScatterRealRuins

Placed in XML map generators (see `1.6/Defs/MapGeneratorDefs/`). Drives standard multi-chunk scatter.

### `Generate(Map map, GenStepParams parms)`

**Guards:**
- Skip if stored blueprint count < 10
- Skip starting tile (if applicable)
- Skip if a `RealRuinsPOIWorldObject` or `AbandonedBaseWorldObject` already exists at this world tile

**Proximity system** (`CalculateDistanceToNearestSettlement()`):
- BFS flood-fill on world grid, limited to 16 tiles
- Finds minimum distance to nearest enemy settlement or site
- Settlement distance used as-is; site distance is halved (half the influence)
- Results in density and scale multipliers via exponential curves:
  - `densityMultiplier = exp(1 / (dist/10 + 0.3)) - 0.7`
  - `scaleMultiplier = exp(1 / (dist/5 + 0.5)) - 0.3`
- 50% chance of zero ruins if distance ≥ 16

**Chunk placement:**
- Count: proportional to `(mapArea / someBase) × totalDensity × Rand.Range(1,2)`
- Each chunk: random position via `CellFinder.RandomNotEdgeCell(10, map)`
- Forces: 80/20 split — `CitizenForcesGeneration` vs `MilitaryForcesGenerator` per `hostileChance`
- `densityMultiplier` cap: `while (densityMultiplier * maxRadius > 800) densityMultiplier *= 0.9` — developer-acknowledged hack (comment: "WHAT? Why not 800/radius?"); could be a single division

**Tick management:**
- Pauses game before scatter to prevent the game running during slow generation
- `shouldUnpause` flag tracks whether game was already paused before scatter

### `GenStep_ScatterLargeRealRuins`
For `AbandonedBase` maps. Same class file. Places a single large blueprint (no multiple chunks). Sets `minRadius`/`maxRadius` = 400 to load whole blueprint.

---

## GenStep_ScatterPOIRuins

For POI entry maps. The most configuration-rich GenStep.

### Context Detection
Checks `PlanetaryRuinsInitData.shared.startingPOI` first (new-game settle flow). Otherwise uses the world object at the current tile.

### Settle Mode Behavior

| Mode | Faction | Deterioration | HP | Filth |
|------|---------|---------------|-----|-------|
| `normal` (ruined) | null / neutral | enabled + random costCap | reduced | yes |
| `takeover` | player | disabled | full HP | no |
| `attack` | hostile | disabled | full HP | no |

### Option Configuration
- `minRadius` = `maxRadius` = 400 (load whole blueprint, no sub-part)
- Scavenging multiplier = 0 for active faction bases
- `doorOwnershipProbability = 1.0` for active bases (all doors assigned to faction)
- Random `costCap` = Gaussian around `width × height` for ruined mode
- Random `itemCostLimit` = Gaussian around 100 for ruined mode

### Forces Selection (`GeneratorsForBlueprint()`)
- Ruins / no faction: 20% each for animals, mechanoids, or hostile citizen raiders
- Military POI: `MilitaryForcesGenerator(militaryPower)` always; also citizen if cost high enough
- Civilian POI: `CitizenForcesGeneration(bedsCount)` + optional `MilitaryForcesGenerator(3)` for expensive bases

### Turret Manning (`ManTurrets()`)
Pushes N `"pawn"` symbols onto `BaseGen.symbolStack` with `LordJob_ManTurrets`. Count = `mannableCount × 1.25 + 1`.

### Post-Scatter BaseGen Stack
Pushes `chargeBatteries`, `ensureCanHoldRoof`, `refuel` symbol resolvers, then calls `BaseGen.Generate()`. Note: BaseGen stack is LIFO, so these run in reverse order of push.

### Cleanup
Calls `PlanetaryRuinsInitData.shared.Cleanup()` after map generation to clear global init state.

---

## Constants.cs

Defines three string keys for `ResolveParams.GetCustom/SetCustom`:
- `"realruins.scatteroptions"`
- `"realruins.coveragemap"`
- `"realruins.forcesgenerators"`

**These are effectively dead code.** `ResolveParams.GetCustom/SetCustom` is broken and the `ResolveParams` struct is sealed — these keys were from an abandoned approach. Options are now passed directly.

---

## XML Def Registration

Scatter gen steps are registered in XML defs:
- `1.6/Defs/MapGeneratorDefs/MapGenerators_RealRuins.xml` — defines the custom map generator defs
- `1.6/Defs/MapGeneratorDefs/Ruins_EncounterMapGenerator.xml` — encounter map generator
- `1.6/Defs/MapGeneratorDefs/Rules.xml` — symbol resolution rules for BaseGen
- `1.6/Patches/MapGenerator_Patch.xml` — patches vanilla map generators to include scatter gen steps
