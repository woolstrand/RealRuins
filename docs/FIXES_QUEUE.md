# RealRuins — Fixes Queue

Single list of known bugs, code smells and design weaknesses. Entries are grouped by severity and subsystem.

## How to maintain this file

- Each section has its own numbering that starts from 1 and a severity prefix: **C** critical, **H** high, **M** medium, **L** low, **U** unverified.
- IDs are never reused or renumbered. Add new items at the end of the matching section with the next free number.
- When an item is fixed, delete its description and leave only the heading, e.g. `### H3. Fixed`. This keeps the other IDs stable and the file short.
- Remove the `Fixed` stubs only when the user explicitly asks for a cleanup.
- An unverified item (U) should be confirmed or rejected against the code before work starts. Once confirmed, move it to the right severity section with a new ID.
- Items marked **Note** need developer input or extra analysis before changing code.

---

## CRITICAL — Data loss or incorrect behavior

### C1. Fixed

---

### C2. Fixed

---

## HIGH — Logic errors and stability risks

### H1. `SnapshotManager` Thread-Safety Gaps
**File**: `Source/Classes/Utility/SnapshotManager.cs`
**What**:
- `Pop()` in `LoadNextSnapshot` is locked (`snapshotsToLoadLock`), but `Add` in `AggressiveLoadSnapshots`/`LoadSomeSnapshots` and the list replacement in `Reset()` are not.
- `loadedSnapshotsCount++`, `failedSnapshotsCount++` and the completion check (`loaded + failed == total`) are not atomic.
- `snapshotTimestamps` is read and written without locking from the Unity game thread and callback threads.
**Impact**: Missed or duplicated completion callbacks, lost counts; a race on `snapshotTimestamps` can throw `ArgumentException` (duplicate key) or make the upload rate limiter see a wrong elapsed time.
**Fix**: Use the existing lock for all list access, `Interlocked` for counters, and `ConcurrentDictionary` (or a lock) for `snapshotTimestamps`.

---

### H2. `LoadSomeSnapshots` Retries Forever
**File**: `Source/Classes/Utility/SnapshotManager.cs`, `LoadSomeSnapshots(concurrent, retries)`
**What**: `retries` is decremented on failure but never checked. The comment says a 10 s delay while the code uses 20 s.
**Fix**: Stop when `retries <= 0`.

---

### H3. `StoreBinaryData` Dedup Hazards
**File**: `Source/Classes/Utility/SnapshotStoreManager.cs`, `StoreBinaryData()`
**What**: A date prefix that fails `int.TryParse` silently skips dedup. Nothing guards the raw background `Thread`: an exception in `GetFiles` or `File.Delete` is unhandled.
**Fix**: Wrap the thread body in `try/catch` with logging. Decide what to do with non-numeric prefixes.

---

### H4. `ScatterOptions.Copy()` Omits Fields
**File**: `Source/Classes/Scattering/ScatterOptions.cs`
**What**: `costCap`, `startingPartyPoints` and `overridePosition` are not copied. Every `options.Copy()` handed downstream loses them.
**Fix**: Copy them. Add a reflection test that fails on any uncopied field.

---

### H5. `BakedTaleReference.ExposeData` Uses `new` Instead of `override`
**File**: `Source/Classes/Snapshotting/BakedTaleReference.cs`
**What**: `ExposeData()` is declared with `new` (hiding the base method). The Harmony patch on `TaleReference.ExposeData` compensates for this, but if the patch silently fails, art description strings will not be saved or loaded for `BakedTaleReference` objects.
**Impact**: Art descriptions on ruins' art objects may be lost silently. Correctness depends on a Harmony patch rather than normal virtual dispatch.
**Fix**: If `TaleReference.ExposeData` is virtual, use `override`. If not, the Harmony workaround is necessary but should be documented more prominently.

---

### H6. Global `Scribe.mode` Mutation in `PawnRestoreUtility`
**File**: `Source/Classes/Scattering/Internal/PawnRestoreUtility.cs`
**What**: `Scribe.mode` is temporarily set to `LoadSaveMode.LoadingVars` for hediff deserialization. This is global state.
**Impact**: An unhandled exception between the set and the reset leaves RimWorld's save system in the wrong mode for the rest of the session.
**Fix**: Wrap in `try/finally`: `try { Scribe.mode = LoadingVars; ... } finally { Scribe.mode = Inactive; }`.

---

### H7. `Compressor.ZipFile()` Deletes Original Before Successful Write
**File**: `Source/Classes/Utility/Compressor.cs`
**What**: `ZipFile()` reads the file, deletes the original, then writes compressed data. A comment already says "Dangerous zipping."
**Impact**: If the write fails, the original XML is lost.
**Decision**: No fix needed. The process runs occasionally and silently, and the database already holds more than 4M blueprints, so losing one now and then does not matter. Only improve the comment to say this.

---

### H8. Division by Zero in `BlueprintAnalyzer`
**File**: `Source/Classes/DynamicMapObjects/BlueprintAnalyzer.cs`
**What**:
- In `supposedType()`, branch `militaryFeatures < 3 && prodScore <= 50`: when `occupiedTilesCount == wallLength`, `(occupiedTilesCount - wallLength) / haulableStacksCount` divides by zero if `haulableStacksCount == 0`.
- The `prodScore` line (`result.haulableStacksCount * 5 / result.internalArea`) is integer division and throws `DivideByZeroException` when `internalArea` is 0.
**Impact**: `DivideByZeroException` when analyzing a blueprint with only wall tiles or with no internal area.
**Fix**: Guard both divisions.

---

### H9. `Page_PlanetaryRuinsLoader` Background Thread Without Synchronization
**File**: `Source/Classes/Page_PlanetaryRuinsLoader.cs`
**What**: `CreateSitesInt()` runs on a background thread and writes `blueprintsProcessedCount`, `blueprintsUsed`, `pageState`, `forceStopLoading`, `forceStopTransfer`, all read from the UI thread, without synchronization. It also calls engine APIs off the main thread.
**Impact**: Data races. In practice the fields are simple integers and enums, so torn reads are unlikely on x86 but not guaranteed. No cleanup if the window is closed mid-transfer.
**Fix**: Mark shared fields `volatile`, use `Interlocked` for counters, and check the window-close state in the background loop.

---

## MEDIUM — Correctness and reliability

### M1. `RealRuins_ModSettings.Reset()` Incomplete
**File**: `Source/Classes/RealRuins_ModSettings.cs`
**What**: `diskCacheLimit` is not reset. `defaultScatterOptions` becomes `new ScatterOptions()` instead of the `ScatterOptions.Default` singleton.

---

### M2. Double `.bp` Extension
**File**: `Source/Classes/Utility/SnapshotStoreManager.cs`, `Source/Classes/Utility/SnapshotManager.cs`
**What**: `StoreBinaryData` always appends `.bp`; `SnapshotManager` passes names already ending in `.bp` (e.g. `local-<id>.bp.bp`). The download path (`next`) has not been checked for the same problem.

---

### M3. `BlueprintRecoveryService.TryRecoverInPlace` Throws Without `</cell>`
**File**: `Source/Classes/Utility/BlueprintRecoveryService.cs`
**What**: `LastIndexOf("</cell>")` returns -1 and `Substring(0, -1)` throws `ArgumentOutOfRangeException`.

---

### M4. Non-Deterministic Blueprint Classification
**File**: `Source/Classes/DynamicMapObjects/BlueprintAnalyzer.cs`, `supposedType()`
**What**: `Rand.Chance(0.5f)` decides between `POIType.Outpost` and `POIType.Communication` in one branch, so the same blueprint can get different types.
**Decision**: No fix needed. The game is random anyway and POI types matter only at game start. Add a comment so this stops being flagged.

---

### M5. `RuinedBaseComp` V1/V2 Forces Logic Conflict
**File**: `Source/Classes/Components/RuinedBaseComp.cs`
**What**: V1 faction-transfer code (`caravanReformType == 1` path) and the V2 wave system (`DoRareTask`) both run simultaneously. Their behavior partially overlaps and can conflict depending on the setting.
**Impact**: Unexpected ownership transfers or wrong force counts depending on `caravanReformType`.
**Fix**: Clearly separate or consolidate the two systems, or add an explicit mode guard at the top level.
**Note**: This touches core logic. Do not fix it without thorough analysis and input from the developer.

---

### M6. Wealth Calculation Inconsistency Between POI and AbandonedBase Systems
**What**: `RealRuinsPOIWorldObject.CurrentMapWealth()` counts all things with market cost. `RuinedBaseComp.GetTotalMapWealth()` counts only `CountAsResource` things. Both are compared against the same threshold arrays.
**Impact**: The POI destruction ratio and the ruined-base raider activity are computed on incompatible bases, so restoration can behave unexpectedly.
**Fix**: Standardize on one wealth calculation method across both systems.

---

### M7. `CaravanArrivalAction_VisitRealRuinsPOI` Sets Faction Hostile Silently
**File**: `Source/Classes/DynamicMapObjects/CaravanArrivalAction_VisitRealRuinsPOI.cs`
**What**: `AffectRelationsIfNeeded()` sets goodwill to `GoodwillToMakeHostile` at the moment of caravan arrival, before any letter is sent.
**Impact**: No advance warning. A neutral relationship can be destroyed by accidentally dispatching a caravan to a POI.
**Fix**: Move the goodwill change to after the letter is shown, or ask for confirmation before arrival if the faction would become hostile.

---

### M8. `Page_SelectStartingSite.DoNext` Uses Static `forceCallOriginal` Flag
**File**: `Source/Classes/RealRuins.cs`, `Page_SelectStartingSite_DoNext_Patch`
**What**: A `static bool forceCallOriginal` bypasses the dialog on re-entry. It is not thread-safe and persists between game sessions.
**Impact**: If the flag gets stuck `true` on an error path, all later `DoNext` calls skip the dialog. If stuck `false`, the dialog may show incorrectly.
**Fix**: Use an instance-scope field tied to the wizard page lifecycle instead of a static flag.

---

### M9. `FormCaravanFromRuinsComp.CanFormOrReformCaravanNow` Uses `new` Not `override`
**File**: `Source/Classes/Components/FormCaravanFromRuinsComp.cs`
**What**: The property is shadowed with `new` instead of overridden, so callers holding a `FormCaravanComp` reference do not see it.
**Impact**: Reform-blocking logic may be bypassed when the component is accessed through the base class.
**Fix**: If the base property is virtual, use `override`. If not, add a Harmony patch to redirect the check.

---

## LOW — Code quality, performance, maintainability

### L1. `RemoveWall()` O(w×h) Per Call Inside O(w×h) Loops
**File**: `Source/Classes/Scattering/Internal/Blueprint.cs`, used by `DeteriorationProcessor` and `ScavengingProcessor`
**What**: `RemoveWall()` scans the whole map to merge room numbers and is called for every removed wall tile inside loops that iterate all cells.
**Impact**: O(w²×h²) worst case for dense ruins; about 8.1 billion iterations on a 300×300 blueprint with heavy deterioration.
**Fix**: Keep a reverse index (room ID to list of cells) for O(room_size) merges, or defer room renumbering until after the full pass.

---

### L2. Multiple `DefDatabase` Lookups for the Same Def Across Pipeline Stages
**What**: The same `defName` is resolved independently in `BlueprintLoader`, `BlueprintPreprocessor` and `BlueprintTransferUtility`.
**Impact**: Triple or more lookup overhead per item per run.
**Fix**: Cache the resolved `ThingDef` on `ItemTile` after the first lookup in `BlueprintLoader`.

---

### L3. Signs & Memorials Mod Integration via Per-Item Reflection
**File**: `Source/Classes/Snapshotting/SnapshotGenerator.cs`, `EncodeThing()`
**What**: `Type.GetType("SaM.CompText, Signs_and_Memorials")` and the field lookup run for every item during snapshot generation.
**Impact**: Slow snapshots on large maps.
**Fix**: Remove Signs & Memorials support (the mod is dead).

---

### L4. `GenerateForces()` Code Duplication in Defender Generators
**File**: `CitizenForcesGeneration.cs` and `MilitaryForcesGenerator.cs`
**What**: `GenerateForces()` is near-identical in both classes; only `GenerateStartingParty()` differs. `BattleRoyaleForcesGenerator` has a similar trigger budget loop.
**Fix**: Move the common loop into `AbstractDefenderForcesGenerator`, with customizable parameters as virtual properties.

---

### L5. Code Duplication Across Arrival Actions
**What**: `TransportPodsArrivalAction_VisitRuins` / `_VisitRuinsPOI` and `CaravanArrivalAction_VisitAbandonedBase` / `_VisitRealRuinsPOI` share about 60–70% of their code.
**Fix**: Extract a shared base class or utility for the common arrival logic.

---

### L6. `NetworkCacheSettingsPage` Calls Disk Methods Every Render Frame
**File**: `Source/Classes/Settings/NetworkCacheSettingsPage.cs`
**What**: `SnapshotStoreManager.Instance.TotalSize()` and `StoredSnapshotsCount()` are called on every `Draw()`.
**Impact**: Possible per-frame stutter if they touch the disk.
**Fix**: Cache the values with a timer (e.g. 5 s) or invalidate on cache operations.

---

### L7. Dead Code
- `TryFindEntryCell()` in `IncidentWorker_CaravanFoundRuins` — never called
- `Constants.cs` string keys — abandoned `ResolveParams` approach
- `POIType.PowerPlant` — appears in `chanceOfHavingFaction()` but `supposedType()` never returns it
- `RestorePawnBodyAndTraits()` in `PawnRestoreUtility` — empty stub
- `minimalTriggerFiringTimeout` parameter in `MilitaryForcesGenerator` — stored, never read
- `allowInstantCaravanReform` in `RealRuins_ModSettings` — orphaned, not saved
- Large commented-out SRTS mod integration block in `RealRuins.cs`
- `ConstructUntouchedIntegrityMap` in `DeteriorationProcessor` — unused

---

### L8. `RaidTrigger.Tick()` Redundant 250-Tick Gating
**File**: `Source/Classes/Triggers/RaidTrigger.cs`
**What**: `Tick()` implements manual 250-tick gating before calling `TickRare()`.
**Fix**: Remove the `Tick()` override and rely on `TickRare()`.
**Note**: First check that `TickRare` is actually called for this thing class. Some entity types have only `Tick` and need their own `TickRare`.

---

### L9. `TrippingTrigger.Tick()` Runs Every Tick
**File**: `Source/Classes/Triggers/TrippingTrigger.cs`
**What**: Checks the thing list at `Position` on every game tick.
**Impact**: O(triggers × thingList) per tick; unlikely to matter at real quantities.
**Fix**: Override `TickRare()` instead, or gate with `TicksUntilNextUpdateLong`.
**Note**: First check that the tripping trigger is used at all. If not, remove it.

---

### L10. `ThoughtWorker_ScavengingRuins` Has a Gap in Stage Thresholds
**File**: `Source/Classes/Thoughts/ThoughtWorker_ScavengingRuins.cs`
**What**: Stage boundaries leave a gap between 10,000 and 25,000, where the state is `Inactive`.
**Impact**: Pawns in a moderately valuable ruin get no mood effect, possibly unintended.
**Fix**: Make the stage boundaries contiguous.

---

### L11. `Dialog_ImportExportSettings` Export TextArea is Editable
**File**: `Source/Classes/Settings/Dialog_ImportExportSettings.cs`
**What**: `GUI.TextArea` in export mode is editable, so the user can alter the string before copying.
**Fix**: Use a read-only display such as `GUI.SelectableLabel`, or warn the user.

---

### L12. Harmony Reflection on Private RimWorld Fields
**File**: `Source/Classes/RealRuins.cs`
**What**: Private member names (`"taleRef"`, `"titleInt"`, `"authorNameInt"`, `"map"`, `"destinationTile"`, `"TryFormAndSendCaravan"`) are hard-coded in reflection calls. A RimWorld update that renames them breaks art descriptions, caravan reform and so on.
**Decision**: No change needed. If an update renames these members, it is better for the mod to fail loudly than to fail silently. Optionally cover them with a reflection contract test.

---

### L13. `POIType` Enum Declared at Global Scope
**File**: `Source/Classes/Components/RealRuinsPOIComp.cs`
**What**: `enum POIType { ... }` is declared outside any namespace and can conflict with other mods or assemblies.
**Fix**: Move it into `namespace RealRuins`. Keep the ordinals unchanged (they are persisted in saves and used in texture and translation keys).

---

### L14. `Window_Close_Patch` Fires on Every Window Close
**File**: `Source/Classes/RealRuins.cs`
**What**: The Harmony postfix on `Window.Close` runs for every window in the game.
**Impact**: Negligible per-close overhead.
**Fix**: Optionally add an early return when the window is not a `Dialog_AdvancedGameConfig`.

---

## UNVERIFIED — Reported but not yet confirmed in code

### U1. Settings Export Culture and Partial Import
`SettingsSerializer` may write floats with the current culture (`ToString("R")`) while import parses with `InvariantCulture`, so values like `0,5` fail to import and the field keeps its old value. `TryImport` applies fields as it goes, so a late failure can leave settings half-applied while returning false.

### U2. `APIService` Culture-Sensitive `coverage`
`coverage` may be formatted into the request URL with the current culture. `seed` may be concatenated into the path without escaping.

### U3. `Blueprint.Part` May Drop the Last Row and Column
`Part` loops over `x < maxX` and `z < maxZ`. Whether this is a bug depends on how `maxX` and `maxZ` are defined.

### U4. `Rand.Range` With min Greater Than max
Possible in `Blueprint.RandomPartCenteredAtRoom` (`Rand.Range(size.x, width - size.x)`), `BattleRoyaleForcesGenerator.ScatterStartingParties`, and `DeteriorationProcessor` (`Rand.Range(1, Math.Min(stackLimit, stackCount))` with `stackCount == 0`).

### U5. Raid Trigger Loops May Spin Forever
In the forces generators, a `continue` after a failed `InBounds` check never decrements the budget, and `result.IsValid` may stay false in the battle royale generator.

### U6. `AdvancedSettingsPage` Buffers May Go Stale
`RealRuins_Mod` keeps the settings window and pages alive. After `TryImport` or `RealRuins_ModSettings.Reset()` the text buffers may still hold the old values and overwrite the new ones on the next draw.

### U7. Debug Logging Reaches Unity on Error
`Debug.Error` calls `Verse.Log.Message` at the default log level, which touches Unity natives. This matters for unit tests outside the engine.
