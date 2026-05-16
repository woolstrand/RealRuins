# RealRuins — Weak Points & Areas for Improvement

This report documents bugs, code smells, design weaknesses, and architectural concerns found during a full audit of the RealRuins codebase. Issues are grouped by severity and subsystem.

---

## CRITICAL — Active Bugs That Cause Data Loss or Incorrect Behavior

### 1. Fixed
---

### 2. Fixed
---

### 3. Fixed
---

### 4. Fixed
---

### 5. Fixed
---

### 6. `MapRuinsStore.remoteMapIds` Uses `LookMode.Undefined`
**File**: `Source/Classes/DynamicMapObjects/MapRuinsStore.cs`
**What**: `Scribe_Collections.Look(ref remoteMapIds, "remoteMapIds", LookMode.Undefined)`.
**Impact**: `LookMode.Undefined` tells Scribe it doesn't know how to serialize the list elements. On load, `remoteMapIds` will be empty or unpopulated, losing all stored remote map IDs across saves.
**Fix**: Change to `LookMode.Value`.

---

### 7. `chronologicalAge` Writes Biological Age
**File**: `Source/Classes/Snapshotting/SnapshotGenerator.cs`, `EncodePawn()`
**What**: `xmlWriter.WriteAttributeString("chronologicalAge", pawn.ageTracker.AgeBiologicalTicks.ToString())` — should be `AgeChronologicalTicks`.
**Impact**: For pawns with slow aging (Royalty trait) or long-lived xenotypes, the chronological age is saved as the biological age. When the pawn is restored in another player's game, their lifespan/appearance may be subtly wrong.
**Fix**: Replace `AgeBiologicalTicks` with `AgeChronologicalTicks` for the `chronologicalAge` attribute.

---

## HIGH — Logic Errors and Stability Risks

### 8. `BakedTaleReference.ExposeData` Uses `new` Instead of `override`
**File**: `Source/Classes/Snapshotting/BakedTaleReference.cs`
**What**: `ExposeData()` is declared with `new` (hiding base method) rather than `override`. The Harmony patch on `TaleReference.ExposeData` compensates for this, but if the patch silently fails, art description strings will not be saved/loaded for BakedTaleReference objects.
**Impact**: Art descriptions on ruins' art objects may be lost silently on save/load failures. Correctness depends on a Harmony patch rather than the normal virtual dispatch chain.
**Fix**: If `TaleReference.ExposeData` is virtual, use `override`. If not, the Harmony workaround is necessary but should be documented more prominently.

---

### 9. Global `Scribe.mode` Mutation in `PawnRestoreUtility`
**File**: `Source/Classes/Scattering/Internal/PawnRestoreUtility.cs`
**What**: `Scribe.mode` is temporarily set to `LoadSaveMode.LoadingVars` for hediff deserialization. This is global state. An unhandled exception mid-loop could leave `Scribe.mode` dirty.
**Impact**: If any unhandled exception fires between the mode set and the reset, RimWorld's entire save system will behave incorrectly for the remainder of the session.
**Fix**: Wrap in `try/finally` to guarantee mode restoration: `try { Scribe.mode = LoadingVars; ... } finally { Scribe.mode = Inactive; }`.

---

### 10. `Compressor.ZipFile()` Deletes Original Before Successful Write
**File**: `Source/Classes/Utility/Compressor.cs`
**What**: `ZipFile()` reads the file, **immediately deletes the original**, then writes compressed data. Comment acknowledges: "Dangerous zipping."
**Impact**: If the write phase throws (disk full, I/O error, etc.), the original XML file is permanently lost. No recovery is possible.
**Fix**: No need to fix. This process is done occasionally, silently and just populated a database which already has >4M blueprints. If one blueprint is lost once in a while, this is not a big deal. Communicate this in comments.

---

### 11. `BlueprintRecoveryService` Reads Compressed Files as Plain Text
**File**: `Source/Classes/Utility/BlueprintRecoveryService.cs`
**What**: The service reads the file as a plain text string (`File.ReadAllText`). But `.bp` files are GZip-compressed. This would produce garbled or error output on any compressed file.
**Impact**: Blueprint recovery will never successfully repair any `.bp` file (the format it's supposed to fix). It would only ever work on uncompressed XML files, which are temporary intermediates not normally persisted.
**Fix**: Decompress first (via `Compressor.UnzipFile()`), repair the XML in memory, re-compress, and write back.

---

### 12. Division-by-Zero in `BlueprintAnalyzer.supposedType()`
**File**: `Source/Classes/DynamicMapObjects/BlueprintAnalyzer.cs`
**What**: In the branch `militaryFeatures < 3 && prodScore <= 50`, when `occupiedTilesCount == wallLength`, the expression `(occupiedTilesCount - wallLength) / haulableStacksCount` attempts division by zero if `haulableStacksCount == 0`.
**Impact**: `DivideByZeroException` when analyzing a blueprint that contains only wall tiles.
**Fix**: Guard with `haulableStacksCount > 0` before the division, or use a null-coalescing pattern.

---

### 13. `Page_PlanetaryRuinsLoader` Background Thread Without Synchronization
**File**: `Source/Classes/Page_PlanetaryRuinsLoader.cs`
**What**: `CreateSitesInt()` runs on a background thread and writes `blueprintsProcessedCount`, `blueprintsUsed`, `pageState`, `forceStopLoading`, `forceStopTransfer` — all read from the UI thread — without any synchronization.
**Impact**: Data races; in practice the fields are simple integers/enums so torn reads are unlikely on x86 but not guaranteed. Also: no cleanup if the window is closed mid-transfer.
**Fix**: Mark shared fields `volatile`, use `Interlocked` for counters, and check window-close state in the background loop.

---

## MEDIUM — Code Correctness and Reliability Issues

### 14. Non-Deterministic Blueprint Classification
**File**: `Source/Classes/DynamicMapObjects/BlueprintAnalyzer.cs`, `supposedType()`
**What**: `Rand.Chance(0.5f)` is used to decide between `POIType.Outpost` and `POIType.Communication` in one branch.
**Impact**: The same blueprint analyzed twice may yield different POI types, propagating to different faction tech levels and forces generation. Blueprint analysis should be deterministic.
**Fix**: No need to fix. The game has a lot of randomness. POIs affect only during game start and nobody realistically follows if the type is the same or not. Add comments so it won't trigger alerts anymore.

---

### 15. `RuinedBaseComp` V1/V2 Forces Logic Conflict
**File**: `Source/Classes/Components/RuinedBaseComp.cs`
**What**: V1 faction-transfer code (`caravanReformType == 1` path) and V2 wave system (`DoRareTask`) both run simultaneously. Their behavior partially overlaps and can conflict depending on settings value.
**Impact**: Unexpected ownership transfers or incorrect force counts depending on `caravanReformType` setting.
**Fix**: Clearly separate or consolidate the two systems, or add an explicit mode guard at the top level.
**Note**: This issue might touch some core logic, so do not fix it without thorough analysis and input from the developer.

---

### 16. Wealth Calculation Inconsistency Between POI and AbandonedBase Systems
**What**: `RealRuinsPOIWorldObject.CurrentMapWealth()` counts all things with market cost. `RuinedBaseComp.GetTotalMapWealth()` counts only `CountAsResource` things. These two systems compare wealth values to the same threshold arrays but measure different things.
**Impact**: The destruction ratio in POI restoration logic and the raider activity in ruined base logic are calculated on incompatible bases. A player clearing an expensive base may see unexpected POI restoration or no restoration because the numbers don't align.
**Fix**: Standardize on one wealth calculation method across both systems.

---

### 17. `CaravanArrivalAction_VisitRealRuinsPOI` Sets Faction Hostile Silently
**File**: `Source/Classes/DynamicMapObjects/CaravanArrivalAction_VisitRealRuinsPOI.cs`
**What**: `AffectRelationsIfNeeded()` sets goodwill to `GoodwillToMakeHostile` (forces war) at the moment of caravan arrival, before any letter is sent.
**Impact**: The player receives no advance warning. A neutral relationship with a faction can be destroyed by accidentally dispatching a caravan to their POI.
**Fix**: Move the goodwill change to after the letter is shown, or add a confirmation dialog before arrival if the faction would become hostile.

---

### 18. `Page_SelectStartingSite.DoNext` Uses Static `forceCallOriginal` Flag
**File**: `Source/Classes/RealRuins.cs`, `Page_SelectStartingSite_DoNext_Patch`
**What**: A `static bool forceCallOriginal` is used to bypass the dialog on re-entry. This is not thread-safe and persists between game sessions until the static class is unloaded.
**Impact**: If the flag gets stuck `true` due to an error path, all future `DoNext` calls skip the dialog permanently. If stuck `false`, the dialog may show incorrectly.
**Fix**: Use a `Tuple<bool>` or instance-scope field tied to the wizard page lifecycle rather than a static flag.

---

### 19. `SnapshotManager.snapshotTimestamps` Lacks Thread Safety
**File**: `Source/Classes/Utility/SnapshotManager.cs`
**What**: `snapshotTimestamps` is read and written without locking from both the Unity game thread and callback threads.
**Impact**: A race could cause `ArgumentException` (duplicate key) or a torn read where the upload rate limiter returns an incorrect elapsed time.
**Fix**: Use `ConcurrentDictionary<string, DateTime>` or wrap all access in a `lock`.

---

### 20. `FormCaravanFromRuinsComp.CanFormOrReformCaravanNow` Uses `new` Not `override`
**File**: `Source/Classes/Components/FormCaravanFromRuinsComp.cs`
**What**: The property is shadowed with `new` instead of overriding. Polymorphic callers with a `FormCaravanComp` reference will not see the override.
**Impact**: Reform-blocking logic may be bypassed when the component is accessed through the base class interface.
**Fix**: If `FormCaravanComp.CanFormOrReformCaravanNow` is virtual, use `override`. If not, add a Harmony patch to redirect the check.

---

## LOW — Code Quality, Performance, and Maintainability

### 21. `RemoveWall()` O(w×h) Per Call Inside O(w×h) Loops
**File**: `Source/Classes/Scattering/Internal/Blueprint.cs`, used by `DeteriorationProcessor` and `ScavengingProcessor`
**What**: `RemoveWall()` performs a full O(w×h) scan to merge room numbers. It is called for every wall tile that is removed during deterioration and scavenging loops that themselves iterate all cells.
**Impact**: O(w²×h²) worst-case for dense ruins. On a 300×300 blueprint with heavy deterioration, this is 8.1 billion iterations.
**Fix**: Maintain a reverse index (position → room ID → list of cells) for O(room_size) merges. Alternatively, defer room renumbering until after the full deterioration pass.

---

### 22. Multiple `DefDatabase` Lookups for the Same Def Across Pipeline Stages
**What**: The same `defName` is looked up via `ThingDef.Named(name, false)` or `DefDatabase<ThingDef>.GetNamedSilentFail(name)` in `BlueprintLoader`, `BlueprintPreprocessor`, and `BlueprintTransferUtility` independently.
**Impact**: Triple (or more) lookup overhead per item per pipeline run.
**Fix**: Cache the resolved `ThingDef` on `ItemTile` after first lookup in `BlueprintLoader`. This also simplifies the preprocessor and transfer utility code.

---

### 23. Signs & Memorials Mod Integration via Per-Item Reflection
**File**: `Source/Classes/Snapshotting/SnapshotGenerator.cs`, `EncodeThing()`
**What**: `Type.GetType("SaM.CompText, Signs_and_Memorials")` and field lookup via reflection run for **every item** on the map during snapshot generation.
**Impact**: Performance degradation on large maps (300+ items). Reflection is expensive when called thousands of times.
**Fix**: Remove SaM mod support. It is dead anyway.

---

### 24. `GenerateForces()` Code Duplication in Defender Generators
**File**: `CitizenForcesGeneration.cs` and `MilitaryForcesGenerator.cs`
**What**: `GenerateForces()` is near-identical in both classes — same loop structure, same trigger distribution, same cap logic. The only divergence is in `GenerateStartingParty()`.
**Impact**: Bug fixes or improvements to the forces algorithm must be made in two places.
**Fix**: Move the common `GenerateForces()` loop into `AbstractDefenderForcesGenerator`, exposing only customizable parameters as virtual properties.

---

### 25. Significant Code Duplication Across Arrival Actions
**What**: `TransportPodsArrivalAction_VisitRuins` and `_VisitRuinsPOI`, and `CaravanArrivalAction_VisitAbandonedBase` and `_VisitRealRuinsPOI`, share ~60–70% of their code with minor behavioral differences.
**Fix**: Extract a shared abstract base class or utility class for the common arrival logic.

---

### 26. `NetworkCacheSettingsPage` Calls Disk Methods Every Render Frame
**File**: `Source/Classes/Settings/NetworkCacheSettingsPage.cs`
**What**: `SnapshotStoreManager.Instance.TotalSize()` and `StoredSnapshotsCount()` are called on every `Draw()` call (every frame while settings are open).
**Impact**: If these methods hit disk (e.g., checking file sizes), they will cause per-frame stutter in the settings UI.
**Fix**: Cache the values and invalidate them on a timer (e.g., every 5 seconds) or on cache operations.

---

### 27. Dead Code
Multiple dead code instances across the codebase:
- `TryFindEntryCell()` in `IncidentWorker_CaravanFoundRuins` — defined but never called
- `Constants.cs` string keys — abandoned `ResolveParams` approach
- `POIType.PowerPlant` in `BlueprintAnalyzer` — appears in `chanceOfHavingFaction()` but never returned by `supposedType()`
- `RestorePawnBodyAndTraits()` in `PawnRestoreUtility` — empty stub
- `minimalTriggerFiringTimeout` parameter in `MilitaryForcesGenerator` — stored, never read
- `allowInstantCaravanReform` field in `RealRuins_ModSettings` — orphaned, not saved
- Large commented-out SRTS mod integration block in `RealRuins.cs`

---

### 28. `RaidTrigger.Tick()` Redundant 250-Tick Gating
**File**: `Source/Classes/Triggers/RaidTrigger.cs`
**What**: `Tick()` implements manual 250-tick gating before calling `TickRare()`. `TickRare()` in RimWorld is already called automatically every 250 ticks if you don't override `Tick()`. The manual gating in `Tick()` is redundant.
**Fix**: Remove the `Tick()` override and rely on `TickRare()` alone.
**Note**: Ensure there is a TickRare and it is called for this exact trigger class. I remember that some entities do not have native TickRare, but have just Tick and I had to implement TickRare on my own. Not sure if it is the same case or not.

---

### 29. `TrippingTrigger.Tick()` Runs Every Tick
**File**: `Source/Classes/Triggers/TrippingTrigger.cs`
**What**: Checks the thing list at `Position` every single game tick.
**Impact**: O(triggers × thingList) per tick. Unlikely to be a problem at the quantities placed in practice, but could be slow in extreme scenarios.
**Fix**: Override `TickRare()` instead of `Tick()`, or use `TicksUntilNextUpdateLong` gating.
**Note**: Ensure tripping trigger is used. Otherwise just remove it completely.

---

### 30. `ThoughtWorker_ScavengingRuins` Has a Gap in Stage Thresholds
**File**: `Source/Classes/Thoughts/ThoughtWorker_ScavengingRuins.cs`
**What**: Stage boundaries leave a gap between 10,000 and 25,000 — values in this range return `ThoughtState.Inactive` (no thought at all).
**Impact**: Pawns in a moderately valuable ruin have no mood effect, potentially unintentional.
**Fix**: Align the stage boundaries so they are contiguous.

---

### 31. `Dialog_ImportExportSettings` Export TextArea is Editable
**File**: `Source/Classes/Settings/Dialog_ImportExportSettings.cs`
**What**: `GUI.TextArea` in export mode is technically editable. A user can modify the string before copying.
**Fix**: Use a read-only display or `GUI.SelectableLabel` for the export view, or warn the user not to edit the string.

---

### 32. Harmony Reflection on Private RimWorld Fields
**File**: `Source/Classes/RealRuins.cs`
**What**: Private field names (`"taleRef"`, `"titleInt"`, `"authorNameInt"`, `"map"`, `"destinationTile"`, `"TryFormAndSendCaravan"`) are hard-coded as strings in reflection calls.
**Impact**: Any RimWorld update that renames these fields will silently break the associated functionality (art descriptions, caravan reform, etc.).
**Mitigations**: Add null checks on all `AccessTools.Field()`/`AccessTools.Method()` results and log an error when they fail. Consider using Harmony's `AccessTools` with early validation.
**Note**: If an update renames thos fields, it's better for mod to crash and get some attention than just fail siletly. Crashing mods after game update is an usual thing and I'm not sure we need any adjustments here.

---

### 33. `POIType` Enum Declared at Global Scope
**File**: `Source/Classes/Components/RealRuinsPOIComp.cs`
**What**: `enum POIType { ... }` is declared outside any namespace.
**Impact**: Pollutes the global namespace; any other mod or assembly with the same enum name would conflict.
**Fix**: Move into `namespace RealRuins`.

---

### 34. `Window_Close_Patch` Fires on Every Window Close
**File**: `Source/Classes/RealRuins.cs`
**What**: The Harmony postfix on `Window.Close` runs for every window closed in the game (not just mod windows).
**Impact**: Minor per-close overhead. The `is Dialog_AdvancedGameConfig` check is cheap, so practical impact is negligible, but it's a broad hook.
**Fix**: If concerned about overhead, add an early-exit `if (!(window is Dialog_AdvancedGameConfig)) return;` as the very first line.

---

## Summary Table

| # | Severity | File | Description |
|---|----------|------|-------------|
| 1 | CRITICAL | AdvancedSettingsPage | Text buffers erase saved blacklists |
| 2 | CRITICAL | RealRuins.cs | Concat result discarded, POI gen steps never applied |
| 3 | CRITICAL | SnapshotManager | Race condition on snapshotsToLoad.Pop() |
| 4 | CRITICAL | SnapshotStoreManager | Version dedup split delimiter mismatch |
| 5 | CRITICAL | ScatterOptions | asIs() mutates shared Default singleton |
| 6 | CRITICAL | MapRuinsStore | LookMode.Undefined breaks remoteMapIds serialization |
| 7 | CRITICAL | SnapshotGenerator | chronologicalAge writes biological age |
| 8 | HIGH | BakedTaleReference | ExposeData uses `new` not `override` |
| 9 | HIGH | PawnRestoreUtility | Scribe.mode not in try/finally |
| 10 | HIGH | Compressor | ZipFile deletes original before write succeeds |
| 11 | HIGH | BlueprintRecoveryService | Reads compressed files as plain text |
| 12 | HIGH | BlueprintAnalyzer | Division by zero on wall-only blueprints |
| 13 | HIGH | Page_PlanetaryRuinsLoader | Background thread writes unsynchronized shared state |
| 14 | MEDIUM | BlueprintAnalyzer | Non-deterministic type classification |
| 15 | MEDIUM | RuinedBaseComp | V1/V2 forces conflict |
| 16 | MEDIUM | POI/AbandonedBase | Inconsistent wealth calculation |
| 17 | MEDIUM | CaravanArrivalAction_POI | Hostile goodwill set silently on arrival |
| 18 | MEDIUM | RealRuins.cs | Static forceCallOriginal flag |
| 19 | MEDIUM | SnapshotManager | snapshotTimestamps dict lacks thread safety |
| 20 | MEDIUM | FormCaravanFromRuinsComp | `new` not `override` for CanForm property |
| 21 | LOW | Blueprint + Processors | RemoveWall() O(w×h) inside O(w×h) loops |
| 22 | LOW | Pipeline stages | Multiple DefDatabase lookups per def |
| 23 | LOW | SnapshotGenerator | Per-item reflection for SaM mod integration |
| 24 | LOW | ForceGenerators | GenerateForces() code duplication |
| 25 | LOW | Arrival actions | ~70% code duplication between arrival action pairs |
| 26 | LOW | NetworkCacheSettingsPage | Disk methods called every render frame |
| 27 | LOW | Various | Dead code throughout codebase |
| 28 | LOW | RaidTrigger | Redundant 250-tick gating in Tick() |
| 29 | LOW | TrippingTrigger | Tick() runs every tick unnecessarily |
| 30 | LOW | ThoughtWorker | Gap in mood thought stage thresholds |
| 31 | LOW | Dialog_ImportExportSettings | Export textarea is editable |
| 32 | LOW | RealRuins.cs | Reflection on private fields (fragile) |
| 33 | LOW | RealRuinsPOIComp | POIType enum at global scope |
| 34 | LOW | RealRuins.cs | Window_Close_Patch too broad |
