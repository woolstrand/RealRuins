# Planetary Ruins Feature

> **AI AGENT NOTE**: If you change the POI creation pipeline, blueprint analysis classification, the new-game wizard flow, or the world component, update this file.

## Overview

The Planetary Ruins feature populates a new game's planet with Points of Interest (POIs) derived from other players' uploaded bases. This happens during the new-game setup flow, before the player has started playing.

**Files:**
- `Source/Classes/Page_PlanetaryRuinsLoader.cs` — wizard UI
- `Source/Classes/RealRuinsPlanetary_Mod.cs` — shared init state + enums
- `Source/Classes/DynamicMapObjects/RealRuinsPOIFactory.cs` — blueprint → world object
- `Source/Classes/DynamicMapObjects/BlueprintAnalyzer.cs` — blueprint classification
- `Source/Classes/DynamicMapObjects/PlanetTileInfo.cs` — server tile DTO
- `Source/Classes/Utility/PlanetaryRuinsOptions.cs` — per-game planetary settings
- `Source/Classes/Utility/SnapshotStoreManager.cs` — blueprint pool management (filesystem scanning)

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
     append POI's extra gen steps
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

Analyzes a loaded `Blueprint` and classifies it for POI creation. Files in `Source/Classes/DynamicMapObjects/Analysis/`:
- `BlueprintAnalyzer`: pipeline. `FindRooms()`, beds count (before the tech filter), `ProcessBlueprint()` with `ForAnalyzer`
  options (no mod options except the POI tech level cap), `BlueprintAnalyzerResult.Collect`, `POIClassifier.Classify`.
  Safe on a background thread. A second constructor takes options (sampling with other caps).
- `BlueprintAnalyzerResult`: metrics. `ToString()` lists all fields by reflection.
- `DefRoles`: what a def is for, from def data only (classes, comps, architect category, stats), cached. No def names
  except the vanilla architect categories Security, Furniture and Joy, which mods use too.
- `POIClassifier`: rules, thresholds as named constants.

### What counts (`DefRoles`, `BlueprintAnalyzerResult`)
- **Buildings**: placed buildings except walls (`ItemTile.isWall`), doors, fences and power conduits. Era and high tech are
  judged by them only: haulables are traded and looted, buildings show what the owners could make.
- **Military items**: primary weapons, shells, apparel with sharp armor >= 0.4 (default stuff), turrets (`building.IsTurret`,
  also counted as defensive items), traps, Security buildings (sandbags, barricades, modded trenches).
- **Research**: `Building_ResearchBench` subclasses; research facilities (`CompProperties_Facility` with research speed or linked
  to a research bench); high tech buildings (spacer and above, not furniture or recreation).
- **Power**: generators (`CompPowerPlant` subclasses, output from power props), batteries (`CompPowerBattery`).
- **Comms**: `Building_CommsConsole` subclasses, long range mineral scanners. Not trade beacons, nearly every colony has several.
- **Gravship**: gravship parts (anything needing the Substructure affordance: hull, consoles, extenders, gravcore cells; grav
  engine; gravship facilities: thrusters, fuel tanks), hull walls included; substructure under the floor. A floor laid over
  substructure hides it in a snapshot, so the hull is the main marker. The vanilla endgame ship is not counted.
- **Fortification**: `enclosedOpenArea` (enclosed unroofed cells, a courtyard) and perimeter walls: walls next to the outside with
  a courtyard cell within 2 cells, weighted by `DefRoles.WallStrength` (hit points relative to a steel wall, capped at 3).

### `POIClassifier` rules, in order
1. **Ruins**: `wallLength < 70` or (cost < 2000 and no beds); under 30% of items inside rooms; no enclosed rooms;
   `internalArea / wallLength < 2` with few rooms; average cost per tile < 10.
2. **Shipwreck** (before anything a ship carries: its generators, labs and turrets): gravship parts at least 30% of walls and
   buildings (30+ parts; gravships in sampled blueprints have 0.3–0.8, colonies with a small gravship parked next to them
   0.05–0.2), or substructure under 60%+ of the floor (200+ cells). Mostly from blueprints that were power plants before.
   Faction chance 0.3, defenders as a city; below industrial it falls back to ruins.
3. `fortification` = perimeter wall strength / perimeter of a square of the internal area (~ average strength of a closed ring),
   0 without a courtyard of 15% of the internal area (at least 100 cells).
   `militaryFeatures = (militaryItems + turrets × 10) × 25 / internalArea + fortification × 2.5`,
   `militaryPower = turrets × 250 / internalArea + 1` (defender strength).
4. **Stronghold**: military features >= 8 and more than 15 turrets.
5. **Research** (prevails over military): a research bench, `highTech + 2 × min(facilities, 2 × benches) >= 12`, haulable stacks
   worth 150 on average.
6. **PowerPlant**: 4+ generators, generators and batteries at least 15% of the buildings.
7. Production site = 6+ worktables and at least 0.75 per bed.
8. Military features < 3, not production: **City** (internal area 1200+, 13+ rooms, 8+ beds; up to medieval era 600+, 8+, 5+
   as villages are smaller; beds counted before the tech filter, which takes them under a Neolithic cap), else **Camp** (small,
   cheap, has beds), **Storage**, **Camp**.
9. Military features >= 3, power < 8, not production, by internal area (military bases have ~1000–1800): < 800:
   **MilitaryBaseSmall** if fortification >= 0.5, else **Communication** with comms, else **Outpost**; < 1500:
   **MilitaryBaseSmall**; < 6000: **MilitaryBaseLarge**; larger: **City**.
10. **Factory**: production with military features < 8.
11. Military features >= 8: **MilitaryBaseLarge** (internal area 1200+) or **MilitaryBaseSmall**.

### Era fit (`POITypeUtility`, next to `POIType`)
The rules tell what kind of place it is (settlement, production, military); `POITypeUtility.FitToEra` then steps the type down
to its counterpart one era earlier until `MinTechLevel(type) <= era`:

| Industrial | Medieval | Neolithic |
|---|---|---|
| `City` | `Village` | `Village` |
| `Factory`, `Research`, `PowerPlant` | `Workshop` | `Village` |
| `MilitaryBaseSmall`, `MilitaryBaseLarge`, `Stronghold` | `Castle` | `Village` |
| `Communication` | `Outpost` | `Camp` |
| | `Storage` | `Camp` |

The era is Medieval when at most 5% (`POIClassifier.lowTechAllowedShare`) of the buildings are above medieval, and the POI tech
level cap if that is lower. With the cap on, the analyzed blueprint is already filtered, so a Medieval cap always gives medieval
types and a Neolithic cap villages and camps. `Village`, `Castle` and `Workshop` (ordinals 12–14, icons `Textures/poi-12..14.png`)
are produced only this way. `Shipwreck` is ordinal 15 (`Textures/poi-15.png`). Defenders: `Castle` and `Communication` as military bases, `Village` and `Workshop` as cities and
factories.

### Tuning: Sample POI types
Debug actions → RealRuins → **Sample POI types** (main menu or in game) → 100 / 300 / 1000 blueprints. Classifies random cached
blueprints (seeded; the set still shifts when new blueprints are downloaded) under no cap, Spacer, Industrial, Medieval,
Medieval limited and Neolithic caps; logs a type × cap table and the most frequent defs per role, and writes every analysis
with all metrics to `RealRuinsPOISample.csv` in the save data folder. About 4 blueprints per second per cap.

Measured on 1000 blueprints (2026-11-03), counts of ~530 non-ruins POIs:

| Type | No cap | Medieval | Neolithic |
|---|---|---|---|
| Camp | 156 | 147 | 239 |
| City / Village | 68 / 9 | 0 / 244 | 0 / 282 |
| Factory / Workshop | 95 / 4 | 0 / 17 | |
| Research | 46 | | |
| Outpost / Communication | 50 / 28 | 54 / 0 | |
| MilitaryBaseSmall / Castle | 20 / 1 | 0 / 21 | |
| Storage | 32 | 47 | |
| PowerPlant | 12 | | |
| Stronghold | 9 | | |

**Known issues:**
- Mined or found materials have no tech level (plasteel, modded natural resources), so they pass any cap.

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

### Faction tech level
`POITypeUtility.MinTechLevel()` maps POI type to minimum faction tech level (e.g., Military requires Industrial+, Castle Medieval). With the tech level cap on for POIs, the
faction's maximum tech level is the cap and the minimum is lowered to it (see 04-scattering, Tech level cap).

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
- `1.7/Patches/WorldObjects_Patch.xml` — patches world object defs to add POI components
- POI world objects use def names `RealRuinsPOI` and `RealRuinsPOI_Unlisted`
