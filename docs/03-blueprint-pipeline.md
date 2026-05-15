# Blueprint Processing Pipeline

> **AI AGENT NOTE**: If you add new processors, change the processing order, modify the Blueprint data model, or change how things are spawned onto the map, update this file.

## Overview

The blueprint pipeline transforms a raw `.bp` file from disk into placed objects on a RimWorld map. The pipeline is a linear sequence of transformations applied to a `Blueprint` object, culminating in `BlueprintTransferUtility.Transfer()` which writes everything to the actual `Map`.

**Files (all in `Source/Classes/Scattering/Internal/`):**
- `Blueprint.cs` — central data container
- `Tiles.cs` — data model for terrain and item tiles
- `Art.cs` — artwork metadata
- `BakedLogEntry.cs` — combat log entry for ruins pawns
- `BlueprintLoader.cs` — XML deserialization
- `BlueprintFinder.cs` — random blueprint selection from cache
- `BlueprintPreprocessor.cs` — initial filtering
- `DeteriorationProcessor.cs` — structural decay simulation
- `ScavengingProcessor.cs` — economic looting simulation
- `CoverageMap.cs` — placement overlap tracking
- `BlueprintTransferUtility.cs` — final map placement
- `PawnRestoreUtility.cs` — pawn reconstruction from XML
- `DefenderForcesGenerator/CitizenForcesGeneration.cs`
- `DefenderForcesGenerator/MilitaryForcesGenerator.cs`

---

## Pipeline Order

```
BlueprintFinder.FindRandomBlueprintWithParameters()
  → BlueprintLoader.LoadWholeBlueprintAtPath() or LoadRandomBlueprintPartAtPath()
  → BlueprintPreprocessor.ProcessBlueprint()
  → blueprint.FindRooms()
  → BlueprintTransferUtility.new()
  → btu.RemoveIncompatibleItems()
  → blueprint.UpdateBlueprintStats()
  → DeteriorationProcessor.Process()
  → ScavengingProcessor.RaidAndScavenge()
  → btu.Transfer(coverageMap)
  → btu.AddFilthAndRubble()
  → AbstractDefenderForcesGenerator.GenerateStartingParty()
  → AbstractDefenderForcesGenerator.GenerateForces()
```

Orchestrated by `RuinsScatterer.Scatter()` — see [04-scattering.md](04-scattering.md).

---

## Data Model

### `Blueprint`
The central object holding all parsed data for one blueprint.

| Field | Type | Purpose |
|-------|------|---------|
| `width`, `height` | int | Dimensions of bounding box |
| `wallMap` | `int[,]` | Room topology: `0`=unvisited, `-1`=wall, `1`=outside, `2+`=interior room IDs |
| `terrainMap` | `TerrainTile[,]` | Per-cell floor tile |
| `itemsMap` | `List<ItemTile>[,]` | Per-cell list of items/pawns/corpses |
| `roofMap` | `bool[,]` | Roof presence |
| `snapshotYear` / `dateShift` | int | Year of capture; `dateShift = -(year - 5500) - Rand.Range(5,500)` (always ≤ 0) |

**`FindRooms()`**: BFS flood-fill over `wallMap`. Edge cells seeded as room 1 (outside). Each unvisited interior cell starts a new room (2, 3, ...). Result stored in `wallMap` and `roomAreas` list (index 0 is dummy for 1-based room indexing).

**`RemoveWall(x, z)`**: Updates topology when a wall is removed — O(w×h) full scan to merge room numbers. Called per tile during deterioration and scavenging, making those operations O(w²×h²) in dense blueprints.

**`Part(location, size)`**: Slices a sub-rectangle. **Mutates the original** — `itemsMap` lists are moved by reference, not copied. Do not use the parent blueprint after calling `Part()`.

**`UpdateBlueprintStats(includeCost)`**: Computes `totalCost`, `itemsCount`, `itemsDensity`. When `includeCost=true`, calls `ThingComponentsMarketCost()` for every item — expensive. Called twice during the pipeline (once in `BlueprintFinder`, once in `RuinsScatterer` post-preprocess).

### `Tiles` (`TerrainTile`, `ItemTile`)
Lightweight data holders deserialized from blueprint XML.

`ItemTile` key fields: `defName`, `stuffDef`, `stackCount`, `isDoor`, `isWall`, `art` (optional `ItemArt`), `innerItems` (for containers/corpses), `rawXml` (for pawn/corpse full XML).

**`isDoor`/`isWall` detection**: XML attribute OR substring matching (`defName.ToLower().Contains("door")`, `"wall"`, `"Cooler"`, `"Vent"`) — legacy compatibility for older snapshots.

**Static factory tiles**: `WallReplacementItemTile()` and `DefaultDoorItemTile()` provide canonical replacement tiles (granite wall, wood door) used throughout the processors.

### `ItemArt`
Holds title, author, description for spawned art objects. `TextWithDatesShiftedBy(int shift)` mutates description text by offsetting the first 4-digit year found (regex-based). Only the first year is corrected.

### `BakedLogEntry`
Subclass of `LogEntry` that stores a pre-formatted combat string with a shifted `ticksAbs`. Used to inject past-tense wound history into restored pawns so hediff causes appear to have happened in the past.

---

## BlueprintLoader

### Entry Points
- `LoadWholeBlueprintAtPath(path)` — loads full blueprint
- `LoadRandomBlueprintPartAtPath(path, size)` — loads then calls `blueprint.Part(randomCenter, size)` centered on a random room

### Decompression
If extension is `.bp`, decompresses via `Compressor.UnzipFile()` to `{path}.xml`. The `.xml` is cached on disk — subsequent calls skip decompression.

### Per-Cell Parsing
- `CollapsedRocks` → replaced with `WallReplacementItemTile`
- `MinifiedThing` with no inner items → silently skipped
- Things with no `ThingDef` but flagged as wall/door → replaced with vanilla `Wall`/`Door`
- `"corpse"` def preserved even without ThingDef
- `fillPercent == 1.0` || `isWall` || `isDoor` → `wallMap[x,z] = -1`
- `stackCount` clamped to `thingDef.stackLimit`

**XML recovery**: On parse failure, calls `BlueprintRecoveryService.TryRecoverInPlace()`. If recovery fails, exception propagates to caller.

---

## BlueprintFinder

Single method: `FindRandomBlueprintWithParameters(minArea, minDensity, minCost, maxAttempts, removeNonQualified, options)`.

Retry loop (up to `maxAttempts`):
1. Draw random filename from `SnapshotStoreManager`
2. Load full blueprint + compute stats
3. Check: `width*height > minArea`, `itemsDensity > minDensity`, `totalCost >= minCost`
4. On failure: optionally delete the file (`removeNonQualified`)
5. On XML failure: always delete

Returns null if all attempts fail. No reason code is returned to the caller.

---

## BlueprintPreprocessor

Static in-place processor with two passes.

**Pass 1 — structural/options filtering:**
- Removes items with missing `ThingDef` (mod removed)
- Removes campfires and torch lamps unconditionally
- Removes non-wall/door items if `options.wallsDoorsOnly`
- Removes `EverHaulable` items if `options.disableSpawnItems`
- Removes animal sleeping spots and event spots
- Calls `blueprint.RemoveWall()` when a wall/door tile is removed (maintains topology)

**Pass 2 — cost cap filtering:**
- Re-runs `UpdateBlueprintStats(true)` to get accurate post-pass-1 costs
- Removes items/terrain exceeding `options.itemCostLimit`
- **Sentinel**: `itemCostLimit >= 1000` means "no limit" (magic number)

---

## DeteriorationProcessor

The most complex processor. Simulates structural decay by computing per-cell "integrity" values then probabilistically removing tiles and items.

### Integrity Map Construction

**Room-based (default):**
1. `FindRooms()` classifies interior rooms
2. Rooms intersecting the outer bounding circle are "opened" (exterior access)
3. For closed rooms: `terrainIntegrity = 20` (sentinel), `itemsIntegrity = 1.0`
4. Edge-expand by 1 pixel (8-neighborhood check for ≥20 neighbors)
5. Normalize to 1.0, then Gaussian blur (kernel 7 for terrain, 4 for items)

**Fallback (all rooms open):**
- Inner half: integrity 0.8–1.2
- Outer ring: 0.7–0.9
- Edge cells: 0 (fully deteriorated)
- Blurred (10 for terrain, 7 for items)

**Untouched** (`deteriorationMultiplier == 0`): all values 1.0.

### Application (`Deteriorate()`)
- Terrain survives with probability `terrainIntegrity[x,z]`
- Items survive with probability `itemsIntegrity[x,z] * (1 - deteriorationMultiplier)`
- Roofs survive with probability `itemsChance * 0.3`
- Surviving items get random stack count reduction
- Removed walls call `blueprint.RemoveWall()` — O(w×h) per call

**Performance note**: `RemoveWall()` called inside the O(w×h) deterioration loop = O(w²×h²) worst case.

---

## ScavengingProcessor

Simulates NPC factions looting the ruins before the player arrives. Operates on sorted economic value.

### Algorithm
1. Collect all terrain and item tiles into `tilesByCost` list with cumulative cost
2. Sort ascending by `cost/weight` ratio (cheapest-per-kg first, most valuable last)
3. Compute `raidsCount = floor(log(age/10+1) × scavengersActivity)`, capped at 50
4. Each raid's capacity grows geometrically: `capacity = baseCapacity × 1.1^i`
5. Each raid pops tiles from the back (most valuable) until capacity exhausted or `cost/weight < 7`
6. Removed doors: 80% replaced with `DefaultDoorItemTile()`, 20% fully removed
7. Removed walls: replaced with `WallReplacementItemTile()` (granite) to preserve structure
8. Post-raid: doors without two adjacent walls on either axis are removed

### `LimitCostToCap()`
If `options.costCap > 0`, randomly removes tiles until `totalCost ≤ costCap`. Walls/doors downgraded to wood rather than removed.

**Note**: `age = -blueprint.dateShift` (always positive). Older blueprints have more scavenging.

---

## BlueprintTransferUtility

The placement engine — materializes the blueprint onto the actual `Map`.

### Constructor
- Parses `spawnBlacklist` and `materialBlacklist` settings into `HashSet<string>` (O(1) lookups)
- Computes `mapOriginX/Z` by centering blueprint in `rp.rect`, clamped to map bounds

### `RemoveIncompatibleItems()`
Map-aware compatibility pass: checks terrain affordances for blueprint terrain tiles and each item. Removes items that can't stand on the target terrain.

### `MakeThingFromItemTile(tile, ...)` — Core Factory
Handles all cases:
- `"pawn"` → `PawnRestoreUtility.MakePawnWithRawXml()`
- `"corpse"` → generate pawn, kill it, create `Corpse`, set `timeOfDeath` and rot
- Checks spawn blacklist (O(1))
- Checks material blacklist; substitutes `fallbackMaterial` if matched
- Missing stuff → `GenStuff.DefaultStuffFor()` or `BlocksGranite` for walls
- Quality: Gaussian distribution, clamped 0–6
- Art: initializes `CompArt` with date-shifted description if present
- Food spoilage: proportional to `deteriorationMultiplier`
- HP: Gaussian-distributed reduction; or full HP if `forceFullHitPoints`
- Sets haulables as forbidden, storage buildings to `Unstored`
- **DubsBadHygiene compatibility**: explicitly skipped in clear/spawn passes to avoid infinite loop bug in that mod

### `Transfer(CoverageMap)`
Two-pass process:
1. **Clear pass**: Probabilistically (60%) or always (`overwritesEverything`) removes existing map objects where blueprint will place things
2. **Place pass**: Spawns terrain, roofs, items. Marks cells in `coverageMap`. Ticks every spawned thing once. Randomly breaks `CompBreakdownable` items (80% if deterioration enabled).

### `AddFilthAndRubble()`
Builds a blur-based `filthMap` from non-empty cells, then places dirt/trash/ash filth and steel slag chunks. Adds blood filth (5% per cell) if `shouldKeepDefencesAndPower`.

### `RestoreDefencesAndPower()`
Repairs `CompBreakdownable` on power plants, batteries, and turrets.

---

## PawnRestoreUtility

Reconstructs a `Pawn` from raw XML saved in the snapshot.

### `MakePawnWithRawXml(xml)`
1. Loads XML, looks up `PawnKindDef`; falls back to `Villager` if missing
2. Generates a base pawn via `PawnGenerator.GeneratePawn()`
3. Overlays saved properties via discrete methods, each in its own try/catch

| Restore method | Scope |
|----------------|-------|
| `RestorePawnName` | NameTriple or NameSingle |
| `RestorePawnGender` | Gender enum |
| `RestorePawnAge` | Biological + chronological ticks (chronological shifted by `dateShift`) |
| `RestorePawnHealth` | Dead state, hediff list with validation |
| `RestorePawnApparel` | Apparel items with degraded HP (max 60%) |
| `RestorePawnStory` | Backstory, body, hair, traits |
| `RestorePawnSkills` | Skill levels, XP, passion |
| `RestorePawnBodyAndTraits` | Empty stub — dead code |

**`ValidateAndFixHediffNode()`**: Validates HediffDef, Class type, BodyPartDef, and pawn body part existence before deserializing each hediff. Removes invalid references.

**Critical**: Temporarily sets `Scribe.mode = LoadingVars` for hediff deserialization — a global state mutation. Must be reset. An unhandled exception mid-loop could leave `Scribe.mode` dirty, destabilizing the save system.

---

## Defender Forces Generators

### `AbstractDefenderForcesGenerator` (base)
Constructed with `rp` (ResolveParams), `faction`, `uncoveredCost`, `forceMultiplier`, and `destructionLevel`. Two abstract methods: `GenerateStartingParty()` and `GenerateForces()`.

### `CitizenForcesGeneration`
- `GenerateStartingParty()`: spawns `Settlement` group immediately, size = `bedCount × 0.7–1.5`
- `GenerateForces()`: distributes `uncoveredCost` into `RaidTrigger` objects with random delays. Each trigger's value drawn from half-Gaussian + uniform mixture. Loop capped at `addedTriggers > 100`.

### `MilitaryForcesGenerator`
- `GenerateStartingParty()`: spawns `Combat` group immediately, then distributes remaining into triggers
- `GenerateForces()`: near-identical to `CitizenForcesGeneration.GenerateForces()` — code duplication
- `minimalTriggerFiringTimeout` constructor parameter is stored but never used — dead parameter

---

## CoverageMap

A `bool[,]` wrapper sized to the full map. Tracks which cells have been written to by the transfer utility. Prevents overlapping blueprint placements. `Mark(cell)` and `isMarked(cell)` are the only operations. `null`-safe — checked before use in `BlueprintTransferUtility`.
