# Utility Classes & Debug Tools

> **AI AGENT NOTE**: If you add new utility classes, change extension methods, modify the debug category system, or update CoroutineManager, update this file.

## Overview

Utility classes provide shared infrastructure used across the mod: HTTP management, compression, extensions, debug logging, and developer tooling.

**Files:**
- `Source/Classes/Utility/Extensions.cs`
- `Source/Classes/Utility/CoroutineManager.cs`
- `Source/Classes/Utility/FactionSelector.cs`
- `Source/Classes/Utility/PlanetaryRuinsOptions.cs` — (see [06-planetary-ruins.md](06-planetary-ruins.md))
- `Source/Classes/Utility/RealRuins_StartupHook.cs`
- `Source/Classes/Utility/SimpleJSON.cs`
- `Source/Classes/Utility/SmallQuestionDialog.cs`
- `Source/Classes/Utility/DebugTools/Debug.cs`
- `Source/Classes/Utility/DebugTools/DebugActionsRealRuins.cs`
- `Source/Classes/Utility/DebugTools/RealRuinsDebugUtils.cs`

---

## Extensions.cs

Three independent extension classes.

### `ArrayExtension.Blur(float[,], int)`
Box blur (3×3 kernel, 9-neighbor average) applied `iterations` times. Uses a separate `delta` array for write-isolation. Cells at value `1.0` are skipped (represent "intact" preserved zones). Used by `DeteriorationProcessor` to create gradient integrity fields.

### `StringExtension.SanitizeForFileSystem(string)`
Replaces all invalid filesystem characters with `_` via regex. Used to sanitize world seeds before forming directory names for blueprint storage subdirectories.

### `ThingDefExtension`

**`AbstractMarketValue(BuildableDef def, ThingDef stuff)`**
The game's own abstract `MarketValue` stat for a def: materials plus work (`StatWorker_MarketValue`). Defs made from stuff without a stuff are valued with their default stuff. Quality is not included: it is rolled only when the thing is spawned. The only blueprint cost (tile `cost`). Cached per (def, stuff) under a lock: a def's value doesn't change during a session, and the planetary loader analyzes blueprints off the main thread. (Before 2026-11-03 blueprints used a materials-only estimate that ignored work; it was ~10% lower overall and much lower for crafted items.)

## WealthUtility.cs

Wealth of things already spawned, in the same units as blueprint costs.
- **`MarketValueWithContents(thing)`**: `MarketValue × stackCount` (quality and hit points included) plus what the thing holds, at any depth. Minified things report their inner thing already; pawns and corpses are not opened.
- **`MapWealth(map, filter)`**: the above summed over the map's things. `RuinedBaseComp` (resources only), `RealRuinsPOIWorldObject` (all things).
- **`WealthDescription(cost)`**: the `RealRuins.RuinsWealth.N` tier (0-10k, 10k-100k, 100k-1M, 1M-10M, more) shown in the abandoned base and POI inspect strings.

**`ThingWeight(ThingDef def, ThingDef stuff)`**
Tries `Mass` stat; if zero or 1.0 (likely default), falls back to recursive weight sum from `costList`. Returns minimum 1.0. Used by `ScavengingProcessor` for raider carry-capacity calculations.

### `ListExtension.Pop<T>(List<T>)`
Removes and returns the last element. Used in download queue management (`SnapshotManager`) and scavenging sort (`ScavengingProcessor`). Not in the standard library.

---

## CoroutineManager

A minimal Unity `MonoBehaviour` singleton providing a stable coroutine host for non-`MonoBehaviour` classes (primarily `APIService`).

**Pattern**: Lazy singleton via property — creates a `GameObject("CoroutineManager")`, calls `DontDestroyOnLoad`, attaches itself as component. Idiomatic Unity infrastructure.

**Usage**: `APIService` calls `CoroutineManager.Instance.StartCoroutine(...)` for all HTTP operations.

---

## FactionSelector

Static utility for weighted-random faction selection.

### Weight Table
| Tech Level | Weight |
|------------|--------|
| Animal | 0% |
| Neolithic | 1% |
| Medieval | 5% |
| Industrial | 70% |
| Spacer | 15% |
| Ultra | 8% |

**Logic**: Filters by hostile/friendly flags, humanlike, non-hidden. Removes zero-weight tech levels. Weighted random pick. Falls back to `RandomEnemyFaction()` if list is empty.

**Note**: Uses `new System.Random()` as a static field (not RimWorld's `Rand`) — faction selection is not seed-deterministic. Intentional for gameplay variety.

---

## RealRuins_StartupHook

`[StaticConstructorOnStartup]` class that queues a `LongEventHandler` action (runs during the loading screen):
1. If downloads enabled, offline mode off, AND fewer than 100 blueprints stored → triggers `AggressiveLoadSnapshots()` to bootstrap the local cache
2. Always calls `CheckCacheSizeLimits()` to enforce the configured disk quota

The 100-snapshot threshold prevents redundant re-downloads after the cache is already populated.

---

## SimpleJSON

Third-party MIT-licensed JSON parser by Bunny83 (2012–2018). Included directly (no NuGet).

**Architecture**: Abstract base `JSONNode` with concrete types `JSONArray`, `JSONObject`, `JSONString`, `JSONNumber`, `JSONBool`, `JSONNull`. `JSONLazyCreator` enables chained property access without null checks during construction. Struct-based enumerators minimize heap allocations.

**Usage**: Exclusively used by `APIService` to parse API responses.

---

## SmallQuestionDialog

Generic multi-button dialog (`Verse.Window` subclass).

**Constructor**: takes title, body text, array of button labels, and `Action<int>` callback. Fires callback with button index and self-closes.

**Usage throughout the mod**: 
- New-game POI settle mode selection (make abandoned / takeover / attack / cancel)
- Planetary ruins download size selection (get 500 / get all / show options)
- Any small binary or multi-way confirmation prompt

---

## Debug.cs

Centralized logging utility with category-based filtering.

### Category Constants
String constants representing subsystems: `Generic`, `Loader`, `Store`, `BlueprintGen`, `BlueprintTransfer`, `Scattering`, `APIService`, `WorldObjects`, etc. `AllCategories` array enables iteration (used by settings UI for toggle buttons).

### Log Levels (`RealRuins_ModSettings.logLevel`)
| Level | What's shown |
|-------|-------------|
| 0 | All messages (Extra, Messages, Warnings, Errors) |
| 1 | Warnings and Errors |
| 2 | Errors only (default) |

**`SysLog(string)`**: Bypasses level filtering. Called for critical startup/system messages.

**`Report(category, format, args)`**: Bypasses level filtering too. For reports the user switched on explicitly in debug settings (`RuinsWealthReport`).

**`Extra(category, message)`**: Logged only when `logLevel == 0` AND the category appears in `debugExtras` list. Provides per-subsystem verbose opt-in without flooding all debug output.

**`Debug.Log()` → `Debug.Message()` → `Debug.Warning()` → `Debug.Error()`**: Tiered output methods each checking their level threshold. Warnings go to `Log.Warning`, errors to `Log.Error` (which opens the log window in dev mode, so expected situations use `Debug.Warning`); everything else goes to `Log.Message`.

### Visual Debug Helpers
`PrintIntMap`, `PrintBoolMap`, `PrintNormalizedFloatMap`: Render 2D arrays as ASCII art in Verse log. Used during development to visualize deterioration maps, room topology, and scatter fields.

---

## DebugActionsRealRuins.cs

Registers a `[DebugAction]` in RimWorld's developer debug menu (LudeonTK) for the world map screen.

**`ShowFocusTileDialog()`**: Opens `Dialog_FocusTile` (defined inline in same file) — a minimal window with a numeric input field. On "Focus Tile", validates tile ID and delegates to `RealRuinsDebugUtils.FocusTile()`.

Uses `[StaticConstructorOnStartup]` to ensure registration during game startup.

**`GeneratePristineRuinsQuest()`** ("Generate pristine ruins quest", on a map): generates `RealRuins_AbandonedBase` with the current threat points. Vanilla "Generate quest" passes no points for this quest (`affectedByPoints` false), and `QuestScriptDef.CanRun` caches its result per (tick, points) starting from "tick 0, 0 points, false", so on a fresh, still paused game it always answers "CanRun returned false" without testing anything.

**`SampleRuinsWealth()`** ("Sample ruins wealth", 10/25/50 maps, on a map) → `RuinsWealthSampler`: for each map a temporary player settlement on a random settle-able tile, full map generation (regular ruins scatter runs as on a new colony), then the map and settlement are removed. Turns the wealth log on and drops the log level to errors for the duration (the game stops logging after 10000 messages), then logs a `SAMPLE` line with percentiles of ruins wealth (actual, estimate, richest chunk, chunk count) and generation time (whole map, ruins scatter, cost calculation).

**`ToggleRuinsWealthLog()`** ("Toggle ruins wealth log", in game only: `AllowedGameStates` flags must all match, so `Entry | Playing` never shows): flips `RealRuins_ModSettings.debugLogRuinsWealth`, the same setting as the "Log ruins wealth" checkbox on the mod's debug settings page, and saves settings.

---

## RuinsWealthReport.cs

Debug measurement of ruins wealth, collected only while `debugLogRuinsWealth` is on and logged with `Debug.Report` (category `Wealth`) whatever the log level is.

`BlueprintTransferUtility.Transfer` creates one report per run (`ScatterState.wealthReport`): for every transferred tile it adds the estimate (tile `cost`) and the actual value of what was spawned — `WealthUtility.MarketValueWithContents` (pawns skipped) and the market value of placed terrain. Measured right after the item is spawned, before forces, filth and roof cleanup.

`CostTiming` (same file) counts time spent in blueprint cost calculation (`Blueprint.UpdateItemStats` and terrain), always on; the `MAP` line of generic ruins shows `limit`, `scatterMs`, `costMs`.

Log lines:
- `Chunk <blueprint>: estimate, allowance, actual (things, terrain). Top: ...` — per run, from `RuinsScatterer`.
- `MAP tile=.. size=.. chunks=.. estimate=.. actual=.. things=.. terrain=.. maxChunk=.. site=..` — one greppable line per map, followed by every chunk sorted by actual value and the 10 most valuable things on the map. Written by `GenStep_ScatterRealRuins` (with proximity conditions: planned chunk count, settlement distance, scavenging multiplier, radius) and by `GenStep_RealRuinsSite` for single-run sites (abandoned base, caravan ruins, POI).

---

## RealRuinsDebugUtils.cs

Static utility for world-view debug operations.

**`FocusTile(int tileId)`**: Validates tile ID (also validated by `Dialog_FocusTile` — redundant), calls `Find.WorldSelector.SelectFirstOrNextAt(new PlanetTile(tileId))` to move world camera, confirms with `NeutralEvent` message.

---

## Startup Sequence Summary

```
Game boot:
  RealRuins [StaticConstructorOnStartup]
    → Harmony.PatchAll(Assembly.GetExecutingAssembly())
    → SnapshotManager.Instance.LoadSomeSnapshots() [if downloads allowed]
    → SnapshotStoreManager.Instance.CheckCacheSizeLimits()

  RealRuins_StartupHook [StaticConstructorOnStartup]
    → LongEventHandler queued:
       if (downloads && !offline && count < 100): AggressiveLoadSnapshots()
       CheckCacheSizeLimits()

  DebugActionsRealRuins [StaticConstructorOnStartup]
    → registers LudeonTK debug action

  RealRuins_Mod (Mod subclass, instantiated by RimWorld):
    → Constructor: LongEventHandler queued: GetSettings<RealRuins_ModSettings>()
```

**Note**: Both `RealRuins` and `RealRuins_StartupHook` use `[StaticConstructorOnStartup]`. Order between them is not guaranteed by the attribute — both are called during assembly load but sequence is undefined. `RealRuins` does call `LoadSomeSnapshots` directly (not via `LongEventHandler`), while `StartupHook` defers to `LongEventHandler`. In practice they complement each other (one eagerly starts downloads, the other ensures the cache is bootstrapped before the game loads fully).
