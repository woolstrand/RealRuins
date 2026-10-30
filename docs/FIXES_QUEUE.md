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

### M5. `RuinedBaseComp` Behavior Depends on `caravanReformType` in Hidden Ways
**File**: `Source/Classes/Components/RuinedBaseComp.cs`
**What** (re-checked 2026-10-30, still relevant; the original wording "V1 vs V2 forces" was inaccurate):
- The `state` machine (`FightingWaves` → `WaitingForEnemiesToBeDefeated` → … → `InformedWaitingForLeaving`) only advances in `DoRareTask` when `caravanReformType == 2` (manual). In modes 0 and 1 the state stays `FightingWaves` for the whole visit, so V2 reinforcement raids (`TriggerRaid`) keep coming as long as the map exists. In mode 2 they stop once all `RaidTrigger`s have fired plus the hysteresis.
- Mode 2 no longer does what its tooltip says. `mapExitLocked` is not read anywhere: the `GenHostility.AnyHostileActiveThreatToPlayer` prefix that used it was commented out in 4c9a3d7 (1.1 update) and later deleted. The comment at `RuinedBaseComp.cs:196` still refers to that prefix. Mode 2 now behaves like mode 0, plus a "no more enemies" message and the V2 raids stopping.
- The mode 1 block that sets the faction to the player (`CompTick`, ~line 158) has almost no effect. `AbandonedBase` has no `canBePlayerHome`, so `Map.IsPlayerHome` stays false. The world icon color is cached in `AbandonedBaseWorldObject.cachedMat` and is not reset, so even the color does not change. The block also calls `AnyHostileActiveThreatToPlayer` every tick until the map is cleared.
- `unlockHysteresisTimeout = 1000` is decremented once per `DoRareTask` (every 60 ticks), so it lasts about 1 game day, not the "short gap while drop pods open" the comment describes. It is not saved, so it restarts after a load.
- Related bug, tracked separately as M13: `destructionLevel` is not saved.
**Impact if not fixed**: Modes 0/1: endless reinforcements on pristine-ruins maps. The rate falls only as loot leaves the map (`destructionLevel` = wealth / cap). Mode 2: a fixed wave period of roughly the triggers plus 1 day. The difficulty depends on a setting labeled as a caravan option, which players won't expect. Nothing crashes and no data is lost.
**Impact if fixed**: Balance changes for players on modes 0/1 (fewer late raids) or mode 2 (depending on the chosen rule). Saves are safe: `state` is already saved, and new fields can default.
**How to fix**:
1. Make the wave lifecycle independent of the reform mode: run the `CheckTriggers` → `WaitingForEnemiesToBeDefeated` → `WaitingTimeoutAfterEnemiesDefeat` chain for every mode, and keep only the "no more enemies" message optional. V2 raids would then stop the same way for everyone. Alternatively, give V2 its own end condition (time since arrival, or `destructionLevel` below a threshold).
2. Remove the mode 1 faction flip (the existing comment already asks for it), or keep it and reset `cachedMat` if the color change is wanted.
3. Decide on mode 2: either bring back the exit lock (a prefix that makes `FormCaravanComp.CanFormOrReformCaravanNow` false while `mapExitLocked`), or merge it into mode 0 and remove the option. If the option is removed, existing value 2 needs a migration.
4. Count `unlockHysteresisTimeout` in ticks (subtract 60 per rare task, or compare against `TicksGame`), save it, and fix the stale comment at line 196.
**Note**: Steps 1 and 3 are design decisions for the developer.

---

### M6. POI Destruction Ratio Mixes Two Different Wealth Bases
**File**: `Source/Classes/DynamicMapObjects/RealRuinsPOIWorldObject.cs`, `ShouldRemoveMapNow()` / `CurrentMapWealth()`
**What** (re-checked 2026-10-30): The original claim does not hold. The two systems never compare against each other's numbers or share thresholds. `RuinedBaseComp` uses `GetTotalMapWealth` on both sides: `currentCapCost` is overwritten in `PostMapGenerate`. The inspect-string thresholds in both systems are applied to blueprint costs (`ThingComponentsMarketCost`), so they are consistent. Unifying the two formulas is not needed.
The real inconsistency is inside the POI: `ratio = (wealthOnEnter - CurrentMapWealth()) / approximateSnapshotCost`.
- The numerator counts every thing on the map: pawns, corpses, plants, rocks, chunks and filth, priced with `ThingComponentsMarketCost`. For things without a cost list that is `BaseMarketValue`.
- The denominator is `BlueprintAnalyzer.totalItemsCost`: only blueprint items, measured on the full snapshot before deterioration.
So killing or driving off defenders (pawns removed, corpses added at a different price), chopping plants and mining all count as "stolen". Deterioration makes the denominator larger than what actually spawned.
**Impact if not fixed**: The outcome on leaving (owner restores, may abandon, or POI destroyed, at the 0.1/0.3 thresholds) is noisy. A fight-heavy visit with little looting can destroy a POI. A heavily deteriorated POI is hard to destroy by looting. The enter cooldown (`difference / 2000` days) is affected the same way. The effect is on balance only.
**Impact if fixed**: POI fate tracks what the player actually took. This changes how often POIs get destroyed, so balance changes; check the 0.1/0.3 thresholds again afterwards. There is no save impact if `wealthOnEnter` keeps its meaning. Old saves store the old value, which mixes slightly for maps that are open when the update is loaded.
**How to fix**: In `CurrentMapWealth()`, count only things that blueprints can contain: skip `Pawn`, `Corpse`, `Plant`, `Filth`, `Mote`/`Projectile`, and mineable/natural rock (`def.building?.isNaturalRock`, `def.mineable`). Use the same filter on enter and on leave. Then either divide by `wealthOnEnter` instead of `approximateSnapshotCost`, so both sides measure the spawned map, or keep the blueprint cost only as a lower bound. Dividing by `wealthOnEnter` is the smaller and more robust change.

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

### M13. `RuinedBaseComp.destructionLevel` Is Not Saved
**File**: `Source/Classes/Components/RuinedBaseComp.cs`
**What**: `destructionLevel` and `baseSmallRaidChance` are not saved in `PostExposeData`, and `PostMapGenerate` does not run on load. After a save/load with a pristine-ruins map open, `destructionLevel` is 0, so `hostileCount < 15 * 0 * forceMultiplier` is never true and V2 reinforcement raids stop for good.
**Fix**: Save both fields (`Scribe_Values.Look`). For old saves, when `destructionLevel` is 0 and the map exists, recompute it from `GetTotalMapWealth(map) / currentCapCost` on the first `DoRareTask`.

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
