# Planetary Ruins Feature

> **AI AGENT NOTE**: If you change the POI creation pipeline, blueprint analysis classification, the new-game wizard flow, or the world component, update this file.

## Overview

The Planetary Ruins feature populates a new game's planet with Points of Interest (POIs) derived from other players' uploaded bases. This happens during the new-game setup flow, before the player has started playing.

**Files:**
- `Source/Classes/Page_PlanetaryRuinsLoader.cs` — wizard UI
- `Source/Classes/RealRuinsPlanetary_Mod.cs` — shared init state + enums
- `Source/Classes/DynamicMapObjects/RealRuinsPOIFactory.cs` — blueprint → world object
- `Source/Classes/DynamicMapObjects/BlueprintAnalyzer.cs` — blueprint classification
- `Source/Classes/DynamicMapObjects/MapRuinsStore.cs` — world component (persists remote IDs)
- `Source/Classes/DynamicMapObjects/PlanetTileInfo.cs` — server tile DTO
- `Source/Classes/Utility/PlanetaryRuinsOptions.cs` — per-game planetary settings

---

## PlanetaryRuinsInitData

Lives in `RealRuinsPlanetary_Mod.cs`. Singleton-like context object (`public static shared`).

**Enums:**
- `PlanetaryRuinsState`: `disabled → configuring → configured → spawned`
- `SettleMode`: `normal` (arrive at ruins), `takeover` (start with undamaged base), `attack` (fight for it)

**Fields**: `selectedMapSize`, `selectedSeed`, `state`, `startingPOI`, `settleMode`.

**`Cleanup()`**: Nulls out POI and seed, sets `state = spawned` (not `disabled` — "finished" semantics).

**Important**: This is writable global state. Any code reading `shared.startingPOI` during map generation must be aware the state transitions once `GenStep_ScatterPOIRuins` calls `Cleanup()`.

---

## New-Game Flow (Harmony Patches in `RealRuins.cs`)

```
Page_SelectStartingSite.PostOpen [postfix]
  → if (allowOnStart enabled): push Page_PlanetaryRuinsLoader onto window stack
  → set PlanetaryRuinsInitData.shared.state = configuring

Window.Close [postfix on every window close]
  → if closed window is Dialog_AdvancedGameConfig
  → if map size changed during configuring state
  → re-open ruins loader with forceCleanup: true

Page_SelectStartingSite.CanDoNext [prefix]
  → if selected tile has RealRuinsPOIWorldObject: force result = true

Page_SelectStartingSite.DoNext [prefix]
  → if selected tile is non-player-faction POI:
     show SmallQuestionDialog: [make abandoned / takeover / attack / cancel]
     defer original DoNext via reflection + static bool forceCallOriginal flag

MapGenerator.GenerateMap [prefix]
  → if shared.startingPOI is set:
     replace mapGenerator with POI's generator def
     (NOTE: extraGenStepDefs.Concat() result is discarded — extra gen steps never applied; BUG)
```

---

## Page_PlanetaryRuinsLoader (Wizard)

A `Window` subclass implementing a multi-step download + placement wizard.

### State Machine
`RuinsPageState`: `Idle → LoadingHeader → LoadedHeader → LoadingBlueprints → LoadedBlueprints → ProcessingBlueprints → Completed`

### Modes
`RuinsPageMode`: `Default` (auto-proceed with user confirmations), `Manual` (wait at each step), `FullAuto` (fully automatic).

### Flow

**`StartLoadingList()`**
- Calls `APIService.LoadAllMapsForSeed(seed, size, coverage, callback)`
- On success: stores `mapTiles` (tile infos) and `blueprintIds`
- If count > 500 in Default mode: shows `SmallQuestionDialog` "get 500 / get all / show options"
- Otherwise: proceeds to `LoadItems()` directly

**`LoadItems()`**
- Applies download limit by taking a random subset of `filteredList`
- Creates new `SnapshotManager`, sets `Progress` and `Completion` delegates
- Calls `AggressiveLoadSnaphotsFromList`
- Progress callbacks update `blueprintsLoadedCount`; checks `forceStopLoading` flag

**`LoadingCompleted()`**
- Transitions to `LoadedBlueprints`
- In `FullAuto`/`Default` mode: immediately calls `CreateSites()`

**`CreateSites()`**
- Spawns a background `Thread` running `CreateSitesInt()`

**`CreateSitesInt()`**
- Iterates `mapTiles`, skipping non-filtered entries
- Applies biome/cost/area filtering
- Calls `RealRuinsPOIFactory.CreatePOI()` per tile
- Tracks `blueprintsProcessedCount`, `blueprintsUsed`; respects `transferLimit` and `forceStopTransfer`

### Thread Safety Issues
- `CreateSitesInt()` runs on background thread but writes `blueprintsProcessedCount`, `blueprintsUsed`, `pageState` without synchronization (`volatile`, `lock`, or `Interlocked`)
- `forceStopLoading` and `forceStopTransfer` shared between threads without synchronization
- No cleanup of background thread if window is closed mid-transfer

---

## BlueprintAnalyzer

Analyzes a loaded `Blueprint` and classifies it for POI creation.

### `BlueprintAnalyzerResult`
Struct with ~16 metrics: wall count, room counts, total cost, military items, beds, production tables, etc. `ToString()` uses reflection to enumerate all fields (debug convenience).

### `Analyze()` Pipeline
1. `FindRooms()` + `ProcessBlueprint()` on the blueprint
2. Initializes result, counts total area and room data
3. Iterates all tiles; `ProcessTile()` accumulates cost/count metrics
4. Military detection: checks `IsShell`, `IsRangedWeapon`, name-contains `"turret"`, `"cannon"`, `"gun"` — string matching, will miss modded items and may have false positives

### `supposedType()` Classification

Uses threshold-based if/else rules (not a scoring system):

**Classified as `Ruins`** if:
- `wallLength < 70` OR low cost OR no beds
- Less than 30% of items inside rooms
- `internalArea / wallLength < 2` AND few rooms
- Average cost per tile < 10

**Otherwise classified by:**
- `militaryFeatures = (militaryItems + defensiveItems × 10) × 25 / internalArea`
- `militaryPower = defensiveItems × 250 / internalArea + 1`
- Classification tree: `Stronghold`, `Military`, `Outpost`, `City`, `Factory`, `Communication` based on feature scores, area, room count

**Known issues:**
- Uses `Rand.Chance(0.5f)` to decide between `Outpost` and `Communication` — non-deterministic. Same blueprint analyzed twice may yield different types
- `POIType.PowerPlant` appears in `chanceOfHavingFaction()` but is never returned by `supposedType()` — dead path
- Division-by-zero possible when `occupiedTilesCount == wallLength` and `haulableStacksCount == 0`

### `chanceOfHavingFaction()`
Switch on `POIType` returning 0–0.9 probability:
- `Ruins`: always 0 (no faction)
- `Stronghold`, `City`: always 0.9

---

## RealRuinsPOIFactory

Factory that creates `RealRuinsPOIWorldObject` instances from `PlanetTileInfo` + blueprint data.

### `CreatePOI()` Pipeline
1. Validate tile index and world placement viability
2. Optional biome-strict check (`aggressiveDiscard`)
3. Load blueprint; validate origin+size fits world map
4. Run `BlueprintAnalyzer.Analyze()`
5. Optionally reject `Ruins` type if `aggressiveDiscard` is on
6. Enforce minimum total cost (1000) and area thresholds
7. Determine faction: combine `chanceOfHavingFaction()` with `abandonedChance`
   - If `(100 - abandonedChance) > 90`: use only parametric chance
   - Otherwise: AND-combine the two probabilities (strictly less likely than either alone)
8. Call `TryCreateWorldObject()`
9. Populate `RealRuinsPOIComp` with all analyzed data

### `TryCreateWorldObject()`
Creates either `RealRuinsPOI` or `RealRuinsPOI_Unlisted` based on whether it's a ruins type. Refuses to create if a world object already exists at the tile.

### `MinTechLevelForPOIType()`
Maps POI type to minimum faction tech level (e.g., Military requires Industrial+).

---

## MapRuinsStore

`WorldComponent` persisting remote map IDs and a per-world folder GUID.

**Fields:**
- `remoteMapIds`: list of map ID strings from the server
- `blueprintsFolder`: per-world GUID for blueprint subdirectory

**Known bug**: `Scribe_Collections.Look(ref remoteMapIds, "remoteMapIds", LookMode.Undefined)` — `LookMode.Undefined` will not serialize list elements correctly. Should be `LookMode.Value`.

---

## PlanetTileInfo

Lightweight DTO for tile metadata returned by `APIService.LoadAllMapsForSeed()`. Not persisted (transient).

**Fields**: `mapId`, `tile` (world tile index), `tileLayer` (default 0, for Odyssey DLC layer support), `biomeName`, `originX`, `originZ`.

---

## PlanetaryRuinsOptions

`IExposable` data object for per-game planetary ruins configuration. Persisted in `RealRuins_ModSettings.planetaryRuinsOptions`.

**Fields:**
- `allowOnStart` — enable ruins on game start
- `downloadLimit` / `transferLimit` — cap on blueprint downloads/POI transfers
- `excludePlainRuins` — exclude ruins type from POIs
- `abandonedLocations` — proportion of abandoned (no faction) world sites (default 0.2)
- `disableSpawnFriendlyLocations` — prevents ruins at friendly faction tiles

---

## POI World Object Defs

Registered in XML:
- `1.6/Patches/WorldObjects_Patch.xml` — patches world object defs to add POI components
- POI world objects use def names `RealRuinsPOI` and `RealRuinsPOI_Unlisted`
