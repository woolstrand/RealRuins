# Snapshotting & Upload Pipeline

> **AI AGENT NOTE**: If you change the snapshot XML format, upload/download logic, file naming conventions, or compression approach, update this file accordingly.

## Overview

This subsystem captures the player's current map state into a compressed XML "blueprint" file and uploads it to the server. It also manages downloading blueprints from the server into the local disk cache.

**Files:**
- `Source/Classes/Snapshotting/SnapshotGenerator.cs` — XML capture
- `Source/Classes/Snapshotting/BakedTaleReference.cs` — art description persistence
- `Source/Classes/Utility/SnapshotManager.cs` — upload/download orchestrator
- `Source/Classes/Utility/SnapshotStoreManager.cs` — local disk cache
- `Source/Classes/Utility/APIService.cs` — HTTP transport
- `Source/Classes/Utility/Compressor.cs` — GZip compression
- `Source/Classes/Utility/BlueprintRecoveryService.cs` — XML repair utility

---

## SnapshotGenerator

### Purpose
Converts the live RimWorld map into a custom XML document for upload.

### Key Entry Points
- `CanGenerate()` — quick check: home area must have ≥ 300 active cells
- `Generate()` — full capture; returns temp `.xml` file path or `null` on failure

### Capture Logic (`Generate()`)
1. Skips non-surface map layers (Odyssey DLC underground — checks `layerDef`)
2. Computes home area bounding box
3. Writes `<snapshot>` root with: version, origin coordinates, dimensions, biome, mapSize, year
4. Writes `<world>` child: seed, tile ID, game ID, planet coverage
5. Iterates each cell in bounding box — only writes cells with terrain, roof, or things
6. Per cell: `<terrain>`, `<roof>`, and `<item>`/`<pawn>`/`<corpse>` children
7. **Quality gates before return:**
   - Density < 0.01 → discard (too sparse, likely an empty map)
   - `maxHPItemsCount < itemsCount * 0.25` → discard (already-ruined map, e.g., captured ruins)

### Thing Encoding (`EncodeThing`, `EncodePawn`, `EncodeCorpse`)
- Skips natural rocks
- Encodes `def`, `stuffDef`, `stackCount`, `rot`, `actsAsWall`, `isDoor`
- `CompArt`: captures art title, author, description
- **Signs & Memorials mod**: reads `text` field via reflection (`Type.GetType("SaM.CompText, Signs_and_Memorials")`). Runs per-item — performance cost on large maps
- Pawn encoding: name, gender, age, story, health, skills, apparel via `Scribe.saver.DebugOutputFor()` (uses RimWorld's save system in read mode — may break on RimWorld updates)
- **Known bug**: `chronologicalAge` incorrectly writes `AgeBiologicalTicks` for both biological and chronological age fields

### Blueprint File Lifecycle
The generator writes to a temp `.xml` file. `SnapshotManager` then calls `Compressor.ZipFile()` to produce a `.bp` (GZip) file, then either stores locally or calls `APIService.UploadMap()`.

---

## BakedTaleReference

A subclass of `TaleReference` that stores a pre-generated art description string instead of a live tale reference. Used so ruins' art objects carry their original colony's tale text when transferred to another player's game.

**Construction:** Created from a raw string (from the blueprint XML). Default constructor produces a "DeterioratedArtDescription" localized string.

**Critical note:** `ExposeData()` is declared with `new` (hides base) rather than `override`. A Harmony patch on `TaleReference.ExposeData` intercepts calls when the instance is a `BakedTaleReference` and calls the subclass method. If that patch ever fails silently, art descriptions will not be saved/loaded.

**Art_Extensions (in `RealRuins.cs`):** Sets private fields on `CompArt` (`taleRef`, `titleInt`, `authorNameInt`) via reflection to inject `BakedTaleReference` instances into spawned art objects.

---

## SnapshotManager

Singleton (`Instance` property). Orchestrates all download and upload operations.

### Upload: `UploadCurrentMapSnapshot()`
- Rate-limited per `snapshotId`: 3-hour cooldown tracked in `snapshotTimestamps` dictionary
- Calls `SnapshotGenerator.Generate()` → `Compressor.ZipFile()` → `APIService.UploadMap()`
- In `offlineMode`: stores with `"local-"` prefix in local cache
- In `SingleFile` mode: stores locally only (no upload)

### Download: `AggressiveLoadSnapshots()` and variants
- `AggressiveLoadSnapshots()`: fetches list of 50 random maps from API, filters already-downloaded, calls `AggressiveLoadSnaphotsFromList`
- `AggressiveLoadSnaphotsFromList(list)`: spawns up to 10 concurrent download chains
- `LoadSomeSnapshots(int)`: lightweight version, only downloads if queue is currently empty
- Each chain uses **continuation-passing style (CPS)**: `LoadNextSnapshot` callback triggers the next download on completion

### Retry Scheduling: `ExecuteAfter(action, TimeSpan)`
Uses `System.Threading.Timer`. Timers are kept in a static `HashSet<Timer>` (with lock) to prevent GC collection before they fire.

### Thread Safety Issues
- `snapshotsToLoad` (a `List<string>`) is accessed via `.Pop()` from multiple concurrent download callbacks **without a lock** — race condition
- `snapshotTimestamps` dictionary is read/written without synchronization
- Download completion check (`loaded + failed == total`) is not atomic

---

## SnapshotStoreManager

Singleton. Manages all local file system operations for blueprint storage.

### Storage Path
- Primary: `%SaveDataFolder%/RealRuins/`
- Fallback: `../Snapshots` (old install compatibility)
- `MoveFilesIfNeeded()` performs one-time migration from old path

### Blueprint Filename Convention
`{dateInt}={rest}.bp` — date prefix is a sortable integer used for cache-ordering and deduplication.

### Key Operations
- `StoreBinaryData(byte[], snapshotId, gameName)` — runs in background `Thread`; checks for existing files by pattern matching, skips if newer exists, deletes older versions, writes file
  - **Known bug**: compares dates by splitting the new filename on `'='` but the existing filename on `'-'` — the version deduplication logic almost never works correctly
- `CheckCacheSizeLimits()` — sorts files alphabetically (newest first by date prefix), accumulates sizes, deletes oldest files beyond `diskCacheLimit`
- `RandomSnapshotFilename()` — only searches the root folder, not game-specific subdirectories
- `CanFireMediumEvent()` / `CanFireLargeEvent()` — gate methods requiring 30 / 250 stored snapshots (any count in offline mode)
- `TotalSize()` / `StoredSnapshotsCount()` — called every UI frame from the settings page; if these hit disk I/O they will cause per-frame stutter

### Thread Safety Issues
- `totalFilesSize` and `totalFileCount` are written from background storage threads and read from the game thread without synchronization
- Lazy init of `snapshotsFolderPath` is not thread-safe (double-init possible, but harmless)

---

## APIService

All HTTP communication with the mod's backend.

### Endpoints
- **API** (`woolstrand.art`): metadata operations (random map lists, seed-based lists, upload)
- **Bucket** (`realruinsv2.sfo2.digitaloceanspaces.com`): blueprint file downloads

All operations are Unity coroutines routed through `CoroutineManager` (see [08-utility-debug.md](08-utility-debug.md)).

### Operations
| Method | Description |
|--------|-------------|
| `LoadRandomMapsList(limit, callback)` | Fetches up to N random map IDs; parses JSON `nameInBucket` field |
| `LoadAllMapsForSeed(seed, coverage, size, callback)` | Fetches all blueprints for a world seed; parses `PlanetTileInfo` DTOs including `tileLayer` (Odyssey DLC) |
| `LoadMap(link, callback)` | GET from CDN bucket: `{BucketRoot}{link}.bp` |
| `UploadMap(data, id, callback)` | Raw binary POST (`binary/octet-stream`); **no auth headers** |

### `AwaitResponseCoroutine()`
Frame-by-frame polling loop with `Time.unscaledTime` timeout (30 seconds, fixed, no per-operation override). Handles connection errors, protocol errors, unexpected status codes.

### Known Issues
- No upload authentication — accepts any binary payload
- `limit=9999` hard-coded in `LoadAllMapsForSeed` (effectively unlimited)
- Debug test URL `http://173.23.44.32/` left in a comment

---

## Compressor

Simple GZip utility.

- `ZipFile(path)` — **destructive in-place**: reads, **deletes original immediately**, writes GZip data to same path. Comment acknowledges: "Dangerous zipping." No error handling — if the write fails, the original is already deleted.
- `UnzipFile(path)` — decompresses `.bp` to UTF-8 string via `MemoryStream`
- `CopyTo(stream, stream)` — manual 4096-byte chunked copy (equivalent to `Stream.CopyTo()`)

---

## BlueprintRecoveryService

Attempts to repair a truncated/partial blueprint XML in-place:
1. Reads file as string
2. Truncates after last `</cell>` tag
3. Appends `</snapshot>` to close root
4. Re-validates with `XmlDocument.Load()`

**Critical limitation**: `.bp` files are GZip-compressed. This service only works on **uncompressed** XML. It is unclear from the code when (if ever) it is called before compression.

---

## Blueprint Format Reference

```xml
<snapshot version="2" originX="10" originZ="5" width="80" height="60" biome="TemperateForest" mapSize="250" year="5512">
  <world seed="mySeed" tileId="12345" gameId="guid" coverage="0.3"/>
  <cell x="0" z="0">
    <terrain def="FloorTile"/>
    <roof/>
    <item def="WoodFence" stuffDef="WoodLog" stackCount="1" rot="0" isDoor="False" actsAsWall="False">
      <art title="..." author="..." description="..."/>
    </item>
    <pawn> ... RimWorld scribe XML ... </pawn>
    <corpse timeOfDeath="123456"> ... pawn XML ... </corpse>
  </cell>
</snapshot>
```

Version 2 is current. Older blueprints may lack some attributes — loaders use `?? default` or conditional checks.
