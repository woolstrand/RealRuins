# Settings & UI

> **AI AGENT NOTE**: If you add new settings, change how settings are serialized/imported/exported, add a new tab, or fix the text buffer initialization bug, update this file.

## Overview

The settings system consists of a tabbed window rendered inside RimWorld's mod settings panel, a static settings store (`RealRuins_ModSettings`), and import/export serialization.

**Files:**
- `Source/Classes/RealRuins_Mod.cs` — `Mod` subclass, entry point
- `Source/Classes/RealRuins_ModSettings.cs` — static settings fields + `ExposeData`
- `Source/Classes/Settings/SettingsPage.cs` — abstract base for tab pages
- `Source/Classes/Settings/RealRuinsSettingsWindow.cs` — tabbed container
- `Source/Classes/Settings/MapGenerationSettingsPage.cs`
- `Source/Classes/Settings/NetworkCacheSettingsPage.cs`
- `Source/Classes/Settings/AdvancedSettingsPage.cs`
- `Source/Classes/Settings/PlanetaryRuinsSettingsPage.cs`
- `Source/Classes/Settings/DebugSettingsPage.cs`
- `Source/Classes/Settings/SettingsSerializer.cs`
- `Source/Classes/Settings/Dialog_ImportExportSettings.cs`

---

## RealRuins_Mod

`Mod` subclass that RimWorld instantiates.

- **Constructor**: registers `GetSettings<RealRuins_ModSettings>()` via `LongEventHandler.ExecuteWhenFinished` (deferred, correct pattern). Defensively re-initializes `defaultScatterOptions` if it comes back null.
- **`WriteSettings()`**: calls `base.WriteSettings()` then triggers `SnapshotStoreManager` cache content and size limit checks. Cache maintenance is a side-effect of saving settings — non-obvious coupling.
- **`DoSettingsWindowContents(Rect)`**: lazily initializes `RealRuinsSettingsWindow` on first call.

---

## RealRuins_ModSettings

All settings are `public static` fields — accessible from anywhere.

### Key Settings
| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| `offlineMode` | bool | false | Disable server communication |
| `allowDownloads` | bool | true | Enable blueprint downloads |
| `allowUploads` | bool | true | Enable blueprint uploads |
| `diskCacheLimit` | float | 256 | Local cache size limit (MB); -1 = unlimited |
| `ruinsCostCap` | float | 1e9 | Max blueprint total market value |
| `forceMultiplier` | float | 1.0 | Scale enemy forces |
| `logLevel` | int | 2 | 0=all, 1=warnings, 2=errors |
| `preserveStandardRuins` | bool | false | Keep vanilla ruins alongside mod ruins |
| `caravanReformType` | int | 0 | 0=normal, 1=instant, 2=delayed |
| `spawnBlacklist` | string | "" | Comma-separated def names to never spawn |
| `materialBlacklist` | string | "" | Comma-separated stuff def names to replace |
| `fallbackMaterial` | string | "" | Replacement for blacklisted materials |

**`ExposeData()`**: Uses `Scribe_Values.Look` for primitives, `Scribe_Deep.Look` for `ScatterOptions` and `PlanetaryRuinsOptions`. Post-load null checks re-initialize `defaultScatterOptions` and `planetaryRuinsOptions` if deserialization produced null.

**`Reset()`**: Hard-codes all defaults — a DRY violation (defaults also declared at field initialization sites).

**Orphaned field**: `allowInstantCaravanReform` (bool) is declared and initialized but **not saved in `ExposeData()`**. The active field is `caravanReformType` (int). The bool is dead legacy state.

**Debug fields in production settings**: `debugKeepSnapshotsAfterUpload`, `debugTileFocusId`, `debugExtras` are persisted to the main settings XML — debug state bleeds into the production settings file.

---

## RealRuinsSettingsWindow

Manages tab navigation and rendering. Not a `Window` — renders into the rect from `DoSettingsWindowContents`.

**Tabs** (5 total):
| Index | Label | Accent Color |
|-------|-------|-------------|
| 0 | Map Generation | Blue |
| 1 | Network & Cache | Green |
| 2 | Advanced | Purple |
| 3 | Planetary Ruins | Orange |
| 4 | Debug | Red |

**`DrawTabs(Rect)`**: tab background brightness = 40% (unselected), 60% (hovered), 100% (selected).

**Known issue**: Tab width calculation double-counts spacing — `tabSpacing` is subtracted from total width, then also from each individual tab width.

**Known issue**: Accent color array hardcoded to 5 entries. A 6th tab would silently cycle back to color 0.

---

## Tab Pages

### MapGenerationSettingsPage
Sliders for: density multiplier, min/max radius, deterioration, scavenging multiplier, item cost limit (1000 = "∞"), hostile chance. Checkboxes for: disable items, walls-only, proximity system, start-without-ruins, preserve vanilla ruins.

**Min/Max Radius enforcement**: After setting `maxRadius`, clamps `minRadius` if `min > max`. Only `minRadius` is auto-corrected — `maxRadius` can be dragged below `minRadius` causing UI jitter.

**Cost limit sentinel**: `itemCostLimit >= 1000` displays as "∞". The slider maximum of 1000 is an in-band magic value.

---

### NetworkCacheSettingsPage
Shows live cache stats, cache limit slider, offline/download/upload toggles, "Download 50" / "Download 500" buttons, "Clear Cache" button (red, with confirmation), "Upload Now" button.

**"Download 50" button**: Calls `SnapshotManager.Instance.LoadSomeSnapshots(5)`. The argument `5` is a batch count; display label "50" assumes batch size = 10. Relationship is undocumented.

**Performance**: `SnapshotStoreManager.Instance.TotalSize()` and `StoredSnapshotsCount()` called every render frame — potential stutter if these involve disk I/O.

**`GUI.color`** is restored without `try/finally` — exception between assignment and restore corrupts the global GUI color state.

---

### AdvancedSettingsPage
Sliders for: `forceMultiplier` (0–2 linear), `ruinsCostCap` (logarithmic 1000–1e9 via `Math.Log`/`Math.Exp`). `caravanReformType` FloatMenu dropdown. Text fields for `spawnBlacklist`, `materialBlacklist`, `fallbackMaterial`. Export/Import buttons.

**CRITICAL BUG — Text Field Buffers**:
```csharp
private string spawnBlacklistBuffer = "";  // never seeded from settings
```
On the **first render frame**, `spawnBlacklistBuffer = ""`. `Widgets.TextField` returns `""`. The comparison `if (spawnBlacklistBuffer != RealRuins_ModSettings.spawnBlacklist)` is `true` whenever any saved blacklist is non-empty. This **immediately overwrites the saved setting with an empty string**. Any configured blacklist is silently erased the moment the Advanced tab is first opened.

The same bug affects `materialBlacklistBuffer` and `fallbackMaterialBuffer`.

**Fix approach**: Seed buffers from settings in a lazy-init or `Initialize()` method called before first draw.

---

### PlanetaryRuinsSettingsPage
Enable-on-start checkbox, download/transfer limit fields, exclude-plain-ruins checkbox, disable-friendly-locations checkbox, abandoned percentage slider, "Spawn Sites" and "Remove All Locations" buttons.

**`RemoveAllLocations()`**: Correct two-pass pattern (collect `RealRuinsPOIWorldObject` instances, then iterate temp list to remove) — avoids collection-modification-during-enumeration.

---

### DebugSettingsPage
Log level FloatMenu, debug category toggle buttons (flow-wrap layout, green=enabled, grey=disabled), Reset button with confirmation dialog.

**Height estimate**: `((count + 2) / 3) × (rowHeight + spacing)` assumes 3 buttons per row. Actual wrapping uses `buttonWidth=160f` against available width — estimate may over/under-allocate at non-standard panel widths.

**Side effect**: Clicking any category toggle calls `WriteSettings()`, which triggers `SnapshotStoreManager` cache checks — heavier than needed for a toggle.

---

## SettingsSerializer

Import/export via Base64-encoded `key=value|key=value` strings.

**`Export()`**: Builds pipe-delimited string with `Uri.EscapeDataString`-encoded string values, `"R"` format floats (round-trip accurate). Base64-encoded for clipboard.

**`TryImport(string)`**: Base64-decode → split by `|` → parse `key=value` switch. Wrapped in `try/catch(Exception)` — failure reason silently swallowed.

**Limitations:**
- No schema version field — forward-compatible by ignoring unknown keys, no backward-compat warning
- `debugExtras` excluded from export (intentional but undocumented)
- `diskCacheLimit = -1` (unlimited sentinel) round-trips correctly as float
- TryImport directly modifies `defaultScatterOptions` fields via `ref` — requires object to be non-null (guaranteed by `GetSettings()`)

---

## Dialog_ImportExportSettings

Modal window for copy/pasting the Base64 settings string.

- **Export**: Pre-populated `GUI.TextArea` (technically editable — user can accidentally modify before copying). "Copy to Clipboard" via `GUIUtility.systemCopyBuffer`.
- **Import**: Editable text area. "Import" calls `SettingsSerializer.TryImport()`, shows success/failure message.
- `absorbInputAroundWindow = true` (modal). `closeOnClickedOutside = false`.
- `forcePause = false` — game does not pause while dialog is open (inconsistent with most RimWorld modals).
- `GUI.SetNextControlName("ExportTextField")` is called but nothing subsequently uses the name — the call is a no-op.
