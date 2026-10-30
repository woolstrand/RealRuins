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

### H1. Fixed

---

### H2. Fixed

---

### H3. Fixed

---

### H4. Fixed

---

### H5. Fixed

---

### H6. Fixed

---

### H7. Fixed

---

### H8. Fixed

---

### H9. Fixed

---

## MEDIUM — Correctness and reliability

### M1. Fixed

---

### M2. Fixed

---

### M3. Fixed

---

### M4. Won't fix
Intended randomness, see the comment in `BlueprintAnalyzer.supposedType()`.

---

### M5. Fixed

---

### M6. Moved
Not a bug: this is how POI wealth tracking works now, and it needs a rework. See [IMPROVEMENTS_AND_FEATURES.md](IMPROVEMENTS_AND_FEATURES.md), item M.

---

### M7. Won't fix
Intended: same as vanilla `SettlementUtility.AffectRelationsOnAttacked`, see the comment in `CaravanArrivalAction_VisitRealRuinsPOI`.

---

### M8. Fixed

---

### M9. Fixed

---

### M10. Fixed

---

### M11. Fixed

---

### M12. Fixed

---

### M13. Fixed

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
