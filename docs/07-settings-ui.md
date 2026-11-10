# Settings & UI

> **AI AGENT NOTE**: If you add new settings, change how settings are serialized/imported/exported, add a new tab, or fix the text buffer initialization bug, update this file.

## Overview

The settings system consists of a tabbed window rendered inside RimWorld's mod settings panel, a static settings store (`RealRuins_ModSettings`), and import/export serialization.

**Files:**
- `Source/Classes/RealRuins_Mod.cs` — `Mod` subclass, entry point
- `Source/Classes/RealRuins_ModSettings.cs` — static settings fields + `ExposeData`
- `Source/Classes/Settings/SettingsPage.cs` — abstract base for tab pages
- `Source/Classes/Settings/RealRuinsSettingsWindow.cs` — tabbed container
- `Source/Classes/Settings/SettingsControls.cs` — one draw method per setting, shared by all tabs
- `Source/Classes/Settings/SettingsPresets.cs` — play style and tech level presets
- `Source/Classes/Settings/PresetsSettingsPage.cs`
- `Source/Classes/Settings/NetworkCacheSettingsPage.cs`
- `Source/Classes/Settings/WealthBalanceSettingsPage.cs`
- `Source/Classes/Settings/SpawnSettingsPage.cs`
- `Source/Classes/Settings/GameplaySettingsPage.cs`
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
| `diskCacheLimit` | float | 1024 | Local cache size limit (MB); -1 = unlimited |
| `ruinsCostCap` | float | 1e9 | Max market value of an abandoned base |
| `caravanRuinsCostCap` | float | 25000 | Max market value of the caravan event's main ruin; 100,000 = no limit |
| `poiRuinsCostCap` | float | 15000 | Max market value of an abandoned POI; 100,000 = no limit |
| `ruinsScaleWithColonyWealth` | bool | false | Multiply ruins wealth limits by colony wealth / reference (`RuinsWealthLimits`) |
| `ruinsReferenceColonyWealth` | float | 100000 | Colony wealth the limits are set for; 25k–500k, shown only with scaling on |
| `forceMultiplier` | float | 1.0 | Scale enemy forces |
| `logLevel` | int | 2 | 0=all, 1=warnings, 2=errors |
| `preserveStandardRuins` | bool | false | Keep vanilla ruins alongside mod ruins |
| `caravanReformType` | int | 0 | 0=automatic, 1=instant, 2=manual (exit lock until raids are over) |
| `spawnBlacklist` | string | "" | Comma-separated def names to never spawn |
| `materialBlacklist` | string | "" | Comma-separated stuff def names to replace |
| `fallbackMaterial` | string | "" | Replacement for blacklisted materials |

**`ExposeData()`**: Uses `Scribe_Values.Look` for primitives, `Scribe_Deep.Look` for `ScatterOptions` and `PlanetaryRuinsOptions`. Post-load null checks re-initialize `defaultScatterOptions` and `planetaryRuinsOptions` if deserialization produced null.

**`Reset()`**: Hard-codes all defaults — a DRY violation (defaults also declared at field initialization sites).


**Debug fields in production settings**: `debugKeepSnapshotsAfterUpload`, `debugTileFocusId`, `debugExtras` are persisted to the main settings XML — debug state bleeds into the production settings file.

---

## RealRuinsSettingsWindow

Manages tab navigation and rendering. Not a `Window` — renders into the rect from `DoSettingsWindowContents`.

**Tabs** (6 total, layout reasoning in `docs/preparation/settings_layout_proposal.md`):
| Index | Label | Accent Color |
|-------|-------|-------------|
| 0 | Presets | Purple |
| 1 | Cache and storage | Blue |
| 2 | Wealth and balance | Gold |
| 3 | Spawn and tech level | Green |
| 4 | Gameplay | Orange |
| 5 | Debug (always visible) | Red |

**`DrawTabs(Rect)`**: tab background brightness = 40% (unselected), 60% (hovered), 100% (selected).

**Known issue**: Tab width calculation double-counts spacing — `tabSpacing` is subtracted from total width, then also from each individual tab width.

---

## Tab Pages

### SettingsPage
`Draw` opens a `Listing_Standard` with `maxOneColumn` (otherwise overflow wraps into an invisible second column), calls the page's `DrawContents` and stores `listing.CurHeight` as `ContentHeight`. The scroll view uses last frame's height, so pages never hard-code it.

### SettingsControls
Every setting has exactly one draw method here. A setting shown on two tabs (for example "Disable friendly raids" on Wealth and balance and on Gameplay) calls the same method from both pages, so label, tooltip, range and greying stay identical. Rules:
- At most two tabs per setting.
- Every page starts with `PageHeader` (caption + one or two sentence description); sections use `Section`.
- Dependent controls are greyed out with `using (EnabledIf(cond))`, never hidden: the scope draws them gray and drops their changes. `Indented()` shifts rows right (done by hand, `Listing_Standard.Indent` differs between game versions). Checkboxes are drawn with `Widgets.CheckboxLabeled` for the same reason.
- Labels of limits and threats name their scope ("regular ruins", "pristine ruins", "all ruins").
- The tech level "applies here" checkboxes sit in the Regular / Events / Planetary sections of the Spawn tab but belong to the cap in the Common section: they are indented, greyed while the cap is off, and their tooltip says so.
- Text buffers (blacklists, planetary limits) are static and remember the value they were last synced with, so import, reset or a preset refills them instead of writing the old text back.

### PresetsSettingsPage
Play styles are rows (button in the left 30%, description in the right 70%); tech levels are columns (buttons on top, descriptions below). Each group shows "Current: <preset>|Custom" and highlights the matching preset. Descriptions are the presets' `TT` texts. Each group ends with Custom: highlighted whenever the settings match no preset of the group. No change handlers: the page compares on every draw and, while nothing matches, stores the group's keys in `customPlayStyle` / `customTechLevel` (saved with the mod settings, `SettingsPresets.Pack` format), so after picking a preset the Custom button restores them. It is disabled until something has been stored. Then Export / Import / Reset all settings.

### SettingsPresets
A preset is a partial settings dictionary in the export string format, applied key by key through `SettingsSerializer.ApplyValue`. Play style and tech level presets set disjoint keys (`TechKeys`), so the order they're picked in doesn't matter; no preset sets `PersonalKeys` (cache, network, log level, item blacklist). `Validate()` logs violations in dev mode. Play styles: Default, Nomadic, Balanced, Dense ruins. Each is `DefaultPlayStyle` plus overrides (`PlayStyle(...)`), so all play styles set the same keys and switching between them leaves nothing behind. "Reset all settings" (`RealRuins_ModSettings.Reset`) is different: it resets everything, cache and debug included.

### NetworkCacheSettingsPage
Shows live cache stats, cache limit slider, offline/download/upload toggles, "Download 50" / "Download 500" buttons, "Clear Cache" button (red, with confirmation). A red line warns while the staging server is on.

Below them, a **Planetary ruins cache** table: one row per planetary ruins subfolder (`<seed>-<mapSize>-<coverage %>`, see `SnapshotStoreManager.GamePath`) with seed, coverage, map size, blueprint count, total folder size (bold) and a "Clear" button with confirmation (`ClearPlanetaryCacheFolder`). The folders are scanned by `SnapshotStoreManager.GetPlanetaryCacheFolders()` only when `SnapshotStoreManager.CacheVersion` changes (bumped from any thread by every write, delete or clear in the cache), at most once a second so background downloads don't rescan every frame, and again whenever the page is shown after not being drawn (to catch changes made outside the game). The main "Clear Cache" button still deletes everything, planetary folders included.

**"Download 50" button**: Calls `SnapshotManager.Instance.LoadSomeSnapshots(5)`. The argument `5` is a batch count; display label "50" assumes batch size = 10. Relationship is undocumented.

**Performance**: `SnapshotStoreManager.Instance.TotalSize()` and `StoredSnapshotsCount()` called every render frame — potential stutter if these involve disk I/O.

### WealthBalanceSettingsPage
Limits (regular per map, caravan, pristine ruins, abandoned planetary locations, single item cost, no haulables, walls and doors only), Scaling (colony wealth + reference, how the regular limit counts — greyed while per-map is unlimited), Threats and difficulty (threat scale, inhabitants in regular ruins, disable friendly raids, small raids keep coming, no ruins on starting map).

`ruinsCostCap` (pristine ruins) uses a logarithmic slider (1000–1e9 via `Math.Log`/`Math.Exp`) and is written back only when moved, because the round trip isn't exact.

### SpawnSettingsPage
Common (tech level cap, roofs, no haulables, walls and doors only, blacklists and fallback material), Regular ruins (tech applies here, density, fragment size, proximity, deterioration, scavengers, keep vanilla ruins, no ruins on starting map), Events (tech applies here), Planetary locations (tech applies here, download/transfer limits, discard plain ruins, no friendly locations, abandoned %).

**Min/Max Radius enforcement**: After setting `maxRadius`, clamps `minRadius` if `min > max`.

**Cost limit sentinel**: `itemCostLimit >= 1000` displays as "Unlimited". The slider maximum of 1000 is an in-band magic value.

### GameplaySettingsPage
Events and quests (pristine ruins quest, caravan ruins event, caravan reform mode, small raids keep coming, disable friendly raids) and Planetary locations (offer at game start, "Download and spawn now", "Remove all locations"). Rule against Spawn: Gameplay is about whether something exists and how you interact with it, Spawn about what it looks like.

**Remove all locations**: two-pass (collect `RealRuinsPOIWorldObject` instances, then remove) to avoid modifying the collection during enumeration.

### DebugSettingsPage
Log level FloatMenu, keep snapshots, log ruins wealth, staging server, debug category toggle buttons (flow-wrap layout, green=enabled, grey=disabled), "Manually trigger blueprint capture".

**Side effect**: Clicking any category toggle calls `WriteSettings()`, which triggers `SnapshotStoreManager` cache checks — heavier than needed for a toggle.

---

## SettingsSerializer

Import/export via Base64-encoded `key=value|key=value` strings.

**`CurrentValues()` / `ApplyValue(key, value)`**: the key list and the parser, shared by export/import and by presets.

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

- **Export**: Pre-populated `GUI.TextArea`; its result is ignored, so the text can be selected and copied but not edited. "Copy to Clipboard" via `GUIUtility.systemCopyBuffer`.
- **Import**: Editable text area. "Import" calls `SettingsSerializer.TryImport()`, shows success/failure message.
- `absorbInputAroundWindow = true` (modal). `closeOnClickedOutside = false`.
- `forcePause = false` — game does not pause while dialog is open (inconsistent with most RimWorld modals).
- `GUI.SetNextControlName("ExportTextField")` is called but nothing subsequently uses the name — the call is a no-op.

## What's new window (`Source/Classes/WhatsNew/`)

- **Articles:** `News/<language folder>/<id>.xml` in the mod root, `<NewsArticle><title/><text/><important>true</important></NewsArticle>` (`important` optional, default false). The id is the file name, the same in every language; a file missing in the active language falls back to `News/English/`. Names sort by date, newest shown first. Postbuild scripts copy `News/`.
- **Read state:** ids in `Config/RealRuins_ReadNews.txt`, separate from the mod settings so "Reset all settings" doesn't bring old news back.
- **When it opens (`WhatsNewLauncher`):** a postfix on `MainMenuDrawer.MainMenuOnGUI`, once per launch, after loading, 1.5 s after the menu appears, and only while no other Dialog-layer window is open (dev windows such as the debug log don't count). So it never stacks on other mods' startup popups (HugsLib news, error reports): it waits for them to be closed.
- **Read rule (`Dialog_WhatsNew`):** read once the player scrolled or the window has been open 3 s. Close protection applies only if the window shows an unread `important` article; otherwise it closes like any window. With one, closing earlier in any way (button, X, Escape) goes through `OnCloseRequest` and opens `Dialog_SkipWhatsNew`. Its "Back to the notes" button is placed exactly over the notes' Close button, "Close anyway" far left, so a double click never skips the notes. Confirming marks the notes read. "Open Real Ruins settings" marks them read and opens `Dialog_ModSettings` for the mod.
- **Debug actions (main menu):** RealRuins → Show what's new (all articles), Forget read news.
