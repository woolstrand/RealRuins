# RealRuins — Architecture Overview

> **AI AGENT NOTE**: This file is part of the project knowledge base. If you make structural changes to the mod (add/remove subsystems, rename major classes, change the pipeline order), update this file to reflect the new architecture.

## What the Mod Does

RealRuins periodically uploads the player's base (as an XML "blueprint") to a central server. When other players generate new maps, ruins from real uploaded bases are procedurally degraded and placed on those maps. Players can also visit the ruins of other players' bases as persistent world-map Points of Interest (POIs) or temporary events (Pristine Ruins).

## Technology Stack

- **Language**: C# targeting .NET 4.7.2 (LangVersion 7.2)
- **Game**: RimWorld (via Assembly-CSharp.dll), Unity engine
- **Patching**: Harmony 2.x (attribute-based patches in nested inner classes)
- **Serialization**: RimWorld's `Scribe` system for save/load; custom XML writer for blueprints
- **HTTP**: Unity `UnityWebRequest` routed through `CoroutineManager`
- **Build**: MSBuild project, outputs to `1.6/Assemblies/RealRuins.dll`

## Major Subsystems

### 1. Startup & Harmony Patches (`RealRuins.cs`)
Entry point. `[StaticConstructorOnStartup]` fires `harmony.PatchAll(...)`. Hosts all Harmony patch classes as nested types. See [07-settings-ui.md](07-settings-ui.md) for settings layer.

### 2. Snapshotting / Upload Pipeline
Captures map state → compresses → uploads to server.
- `SnapshotGenerator` — XML capture
- `SnapshotManager` — orchestrates upload/download, manages concurrency
- `SnapshotStoreManager` — local disk cache
- `APIService` — HTTP transport
→ Details: [02-snapshotting-upload.md](02-snapshotting-upload.md)

### 3. Blueprint Processing Pipeline
From raw XML file to spawned objects on a map.
- `BlueprintLoader` → `BlueprintPreprocessor` → `DeteriorationProcessor` → `ScavengingProcessor` → `BlueprintTransferUtility`
→ Details: [03-blueprint-pipeline.md](03-blueprint-pipeline.md)

### 4. Scattering / Map Generation
How blueprints get placed during map generation.
- `RuinsScatterer` — pipeline orchestrator
- `GenStep_ScatterRealRuins` — standard/event map scatter (multiple small chunks. see several classes in this file.)
- `GenStep_ScatterPOIRuins` — POI/AbandonedBase single large placement
→ Details: [04-scattering.md](04-scattering.md)

### 5. World Objects & Incidents
How ruins appear on the world map and what happens when players interact with them.
- `AbandonedBaseWorldObject` / `SmallRuinsWorldObject` — incident-based ruins
- `RealRuinsPOIWorldObject` — persistent POI world objects
- `IncidentWorker_RuinsFound` / `IncidentWorker_CaravanFoundRuins`
- Caravan/transport pod arrival actions
→ Details: [05-world-objects.md](05-world-objects.md)

### 6. Planetary Ruins Feature
Pre-game setup that populates the planet with POI ruins before a new game starts.
- `Page_PlanetaryRuinsLoader` — wizard UI (download → create sites)
- `RealRuinsPOIFactory` — blueprint-to-world-object creation
- `BlueprintAnalyzer` — classifies blueprints (type, military power, etc.)
- `MapRuinsStore` — world component storing remote map IDs
→ Details: [06-planetary-ruins.md](06-planetary-ruins.md)

### 7. Settings & UI
Tabbed settings window, serialization, import/export.
→ Details: [07-settings-ui.md](07-settings-ui.md)

### 8. Utility & Debug
Extensions, helpers, debug tooling.
→ Details: [08-utility-debug.md](08-utility-debug.md)

## Key Data Flows

### Upload (Save/Game Over)
```
GameDataSaveLoader.SaveGame [Harmony postfix]
  → SnapshotSaver.SaveSnapshot()
  → SnapshotGenerator.Generate() → temp .xml file
  → Compressor.ZipFile() → .bp file (GZip)
  → APIService.UploadMap()  (or local store in offline mode)
```

### Download (Startup / Load Game)
```
RealRuins_StartupHook  [StaticConstructorOnStartup]
  → SnapshotManager.AggressiveLoadSnapshots()
  → APIService.LoadRandomMapsList()
  → APIService.LoadMap() [up to 10 concurrent]
  → SnapshotStoreManager.StoreBinaryData() [background thread]
```

### Standard Map Generation
```
GenStep_ScatterRealRuins.Generate()
  → BlueprintFinder.FindRandomBlueprintWithParameters()
  → RuinsScatterer.Scatter()
     → BlueprintLoader → BlueprintPreprocessor
     → DeteriorationProcessor → ScavengingProcessor
     → BlueprintTransferUtility.Transfer()
     → AbstractDefenderForcesGenerator (places RaidTriggers)
     → BlueprintTransferUtility.AddFilthAndRubble()
```

### POI Map Generation
```
GenStep_ScatterPOIRuins.Generate()
  → RuinsScatterer.Scatter() with full blueprint (no random subpart)
  → BaseGen symbol stack for post-processing (chargeBatteries, refuel, etc.)
```

### New-Game Planetary Ruins Flow
```
Page_SelectStartingSite.PostOpen [Harmony postfix]
  → Page_PlanetaryRuinsLoader (wizard)
    → APIService.LoadAllMapsForSeed()
    → SnapshotManager.AggressiveLoadSnaphotsFromList()
    → RealRuinsPOIFactory.CreatePOI() → RealRuinsPOIWorldObject (world map)
  PlanetaryRuinsInitData.shared.startingPOI set
  MapGenerator.GenerateMap [Harmony prefix]
    → redirects mapGenerator def to POI's generator
```

## Namespace & File Layout

All source is under `Source/Classes/`:
```
RealRuins.cs                    # Startup + all Harmony patches
RealRuins_Mod.cs                # Mod class (settings entry point)
RealRuins_ModSettings.cs        # Static settings fields + ExposeData
RealRuinsPlanetary_Mod.cs       # PlanetaryRuinsInitData context object
Page_PlanetaryRuinsLoader.cs    # New-game wizard
Snapshotting/                   # SnapshotGenerator, BakedTaleReference
Scattering/                     # ScatterOptions, RuinsScatterer, GenStep_*
  Internal/                     # Blueprint, Tiles, Loader, Processors, Transfer
    DefenderForcesGenerator/    # CitizenForcesGeneration, MilitaryForcesGenerator
Incidents/                      # World objects + incident workers (non-POI)
DynamicMapObjects/              # POI world object, factory, analyzer, store
  DefenderForcesGenerator/      # (same subpath in DynamicMapObjects — note duplicate folder name)
Quest/                          # QuestNode_FindBlueprint, QuestNode_GenerateRuinsObject
Components/                     # WorldObjectComps (RuinedBase, POI, FormCaravan)
Triggers/                       # RaidTrigger, TrippingTrigger
Thoughts/                       # ThoughtWorker_ScavengingRuins
Settings/                       # All settings page classes, SettingsSerializer
Utility/                        # APIService, SnapshotManager, SnapshotStoreManager,
                                #   Compressor, CoroutineManager, Extensions,
                                #   FactionSelector, SimpleJSON, SmallQuestionDialog
  DebugTools/                   # Debug, DebugActionsRealRuins, RealRuinsDebugUtils
```

## Important Cross-Cutting Concerns

- **`ScatterOptions`**: The central config bag passed through the entire blueprint pipeline. Both user-configured (persisted) and runtime fields live here. `ScatterOptions.Default` is a **shared mutable singleton** — never mutate it directly.
- **`PlanetaryRuinsInitData.shared`**: Global mutable context for new-game flow. Only valid during game initialization.
- **`RealRuins_ModSettings` static fields**: All settings are `public static`, accessible from anywhere with no instance lookup.
- **Thread safety**: Background threads are used in `SnapshotStoreManager.StoreBinaryData()` and `Page_PlanetaryRuinsLoader.CreateSitesInt()`. These threads write to shared fields without locks — be careful adding logic that touches those fields.
- **Harmony reflection targets**: `RealRuins.cs` uses `System.Reflection` to access private RimWorld fields in several patches. These are brittle against RimWorld updates.
