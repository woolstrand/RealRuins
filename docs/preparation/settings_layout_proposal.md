# Settings page: proposed rearrangement

UI only. Every config key stays as it is, so moving controls between tabs needs **no migration** (see `settings_migration.md`). Renamed labels need new English text and translation updates. Keep the keys, change only the text, and the translators get a "changed" diff instead of a "missing" one.

## 1. Current layout and what's wrong with it

| Tab (current) | Contents |
|---|---|
| **General, Network and Cache** | Cache size limit, local mode only, allow downloads, allow uploads, download more, clear cache, *manually trigger blueprint capture* (debug), export/import settings, planetary ruins cache table |
| **Spawn Settings** | Density, min/max fragment size, deterioration, scavengers, *max item cost*, *ruins wealth per map* (+ proximity / ignore structure / count damage), hostile units amount, *disable haulables*, *walls and doors only*, settlement proximity, no ruins on starting map, keep vanilla ruins |
| **Events and Advanced** | Events threat scale, *absolute wealth cap for events*, *scale with colony wealth* (+ reference), caravan reform type, new raids system, disable friendly raids, *spawn roofs*, abandoned ruins event, caravan ruins event, *caravan wealth cap*, *abandoned locations wealth cap*, *item / material blacklist, fallback material* |
| **Tech Level** | Enable, max level, strategy, applies to: ruins / POIs / events |
| **Planetary ruins and locations** | Offer at game start, download limit, transfer limit, discard plain ruins, no friendly locations, abandoned %, download and spawn now, remove all locations |
| **Debug** | Log level, keep snapshots, wealth log, staging, debug categories, *reset all settings* |

Problems:
1. **Wealth is spread over two tabs.** The per-map limit is on Spawn, the three other caps and the colony scaling that multiplies **all four** are on Events & Advanced. The code already has a single concept for it (`RuinsWealthLimits`), but the UI doesn't show one.
2. **"What may spawn" is spread over three tabs.** Haulables and walls/doors are on Spawn, blacklists and roofs on Events & Advanced, the tech cap on its own tab.
3. **The scope of a setting is invisible.** For example, "walls and doors only" affects regular ruins, caravan ruins and ruined POIs, but **not** the abandoned base or intact POIs. "Events threat scale" now also scales the animals in regular ruins. The tech level tab already solves this ("applies to"), and nothing else follows its example.
4. **"Advanced" is a catch-all.** Roofs and blacklists are content, not advanced options.
5. **Debug things sit in user tabs and user things in Debug.** "Manually trigger capture" is on the General tab, "Reset all settings" is on the Debug tab. The Debug tab is visible to every player (R6).
6. **Labels are out of date:** "Events threat scale" (it isn't only events anymore), "Absolute wealth cap for events" (only the abandoned base now), "Enable abandoned ruins found event" (it's a quest now), "Hostile units amount" (it's a chance of inhabitants per chunk).

## 2. Two ways to arrange it

**A. By topic** (your list: storage, content/tech, wealth, threats, scattering, debug). Settings that differ per ruin kind are shown as **rows per kind** inside the topic.
**B. By ruin kind** (Regular / Events / Planetary tabs, each with its own wealth, threats and content sections).

**Recommendation: A.** Most settings are shared (blacklists, tech, colony scaling, threat scale), so B would either repeat them or need a "Common" tab, which brings back the catch-all. In A, the kind-specific settings sit in small tables, and the player sees all four wealth caps next to each other, which is the way to balance them.

## 3. Proposed layout (A)

Seven tabs (now six). New controls and moves are marked: `[moved from …]`, `[renamed]`, `[new]`.

### Tab 1 — General
*Where blueprints come from and where they are stored.*
```
Server
  [x] Local mode only
  [x] Allow downloading ruins from database
  [x] Allow uploading your bases to ruins database
Local cache
  Cache size limit            [====|-----]  256 MB
  Cached: 4 812 blueprints, 231 MB      [Download more] [Clear cache]
  Planetary ruins cache (per world)   seed | coverage | size | count | MB | [Clear]
Settings
  [Export settings] [Import settings] [Reset all settings]     [moved from Debug]
```
- "Manually trigger blueprint capture" → Debug.
- Planetary cache table: disable [Clear] for the world that's currently loaded (R11).

### Tab 2 — Ruins on the map
*Regular ruins scattered on every new map: how many, how big, how worn.*
```
Amount and size
  Ruins density               [==|-------]  1.0
  Fragment size               [ 12 ]–[ 40 ]   (one double slider or two sliders)
  [x] Consider settlement proximity  (more and bigger ruins near settlements)
Condition
  Additional deterioration    [=|--------]  0.0
  Scavengers activity         [===|------]  1.0
Where
  [ ] No ruins on starting map
  [ ] Keep vanilla ruins
```
- Wealth, item cost and hostiles move out. This tab only answers "how much ruin".

### Tab 3 — Wealth
*How much the ruins may be worth. One table for all kinds, one scaling switch for all of them.*
```
Limits                                    value         ∞ = unlimited
  Regular ruins, per map                  [===|------]  5 000
  Caravan ruins                           [===|------]  25 000
  Abandoned base (quest)       [renamed]  [=======|--]  Unlimited
  Abandoned planetary locations           [==|-------]  15 000
  Single item cost limit (regular ruins)  [=========|]  Unlimited   [moved from Spawn]

Scaling
  [ ] Scale all limits with colony wealth
      Reference colony wealth   [==|------]  100 000      (greyed when off)
      ⓘ At game start the colony has no wealth yet: limits are halved.

Regular ruins: how the limit counts                        (greyed when per-map = ∞)
  [ ] Scale with settlement proximity
  [x] Don't count plain walls, doors and floors
  [ ] Count actual value (damage, quality) instead of full price
```
- "Absolute wealth cap for events" becomes "Abandoned base (quest)". Use the same log slider as now, or a 0–100k slider with ∞ at the end, the same as the others.
- All four sliders share one minimum and one "Unlimited" end (fixes R20, the 0 on caravan/POI).

### Tab 4 — Threats
*Who lives in or comes to the ruins.*
```
  Ruins threat scale            [renamed]  [====|----]  ×1.0   (0 = no inhabitants, raiders or raid triggers)
  Chance of inhabitants in ruins [renamed] [=|-------]  10 %   [moved from Spawn]
  [ ] Disable friendly raids                                    [moved from Advanced]
  [x] Use new raids system for abandoned bases                  [moved from Advanced]
```
- "Events threat scale" → "Ruins threat scale". It now scales animals in regular ruins, caravan raiders, raid triggers and starting parties.
- "Hostile units amount" → "Chance of inhabitants in ruins" (it's a per-chunk chance; 80% animals, 20% mechanoids).
- The new raids system toggle (default on) could be removed altogether. It's a legacy switch nobody should turn off. If it stays, it belongs here.

### Tab 5 — Content
*What may appear in ruins. Uses the "applies to" pattern of the tech level tab for every filter.*
```
                                             Regular  Events  Planetary
Tech level                                                                  [moved from own tab]
  [ ] Limit tech level   Max: [Medieval ▾]  Mode: [Strict ▾]
                                             [x]      [x]     [x]
Filters
  [ ] No haulable items                      [x]      [x]     [x] (ruined only)   [moved from Spawn]
  [ ] Walls and doors only                   [x]      [x]     [x] (ruined only)   [moved from Spawn]
  [x] Spawn roofs                            all ruins and locations              [moved from Advanced]
Blacklists                                                                     [moved from Advanced]
  Item blacklist       [Hive, Sarcophagus               ]
  Material blacklist   [Gold                            ]
  Fallback material    [Wood                            ]
```
- The group checkboxes for the filters are **new behavior**: today the scope is hard-coded in `ScatterOptionsFactory` (`ApplyContentFilters`). Showing them only as fixed text ("applies to: regular, caravan, ruined locations") is the cheap version that doesn't change the code. The checkboxes are the full version and need 4–6 new settings.
- Merging the tech level into this tab removes a tab. If the tab gets too long, keep Tech Level as its own tab next to Content; that's fine too.

### Tab 6 — Events & planetary
*Which special sites exist and how you get in and out of them.*
```
Events
  [x] Pristine ruins quest                [renamed]
  [x] Caravan spotted small ruins
  Caravan reform at abandoned bases   [Automatic ▾]           [moved from Advanced]
      ⓘ one line per mode: Automatic / Instant / Manual (no reforming until the raids are over)
Planetary locations
  [x] Offer to load planetary locations at game start
  Download limit        [  300 ]     Transfer limit   [   50 ]
  [ ] Discard plain ruins    [ ] No friendly locations
  Abandoned locations   [===|-----]  25 %
  [Download and spawn now]   [Remove all locations]
```
- Events and Planetary fit on one tab after the wealth and content settings move out (about 13 controls). Splitting them into two tabs is also fine. The point is that the event wealth caps aren't here anymore; they're on Wealth.

### Tab 7 — Debug (visible only in dev mode)
```
  Log level [Errors ▾]   Debug categories [...]
  [ ] Log ruins wealth
  [ ] Keep snapshots after upload
  [ ] Use staging server     ⚠ also show a red line on tab 1 while it is on
  [Manually trigger blueprint capture]                      [moved from General]
```

## 4. Mapping table (every setting: from → to)

| Setting | Now | Proposed | Label change |
|---|---|---|---|
| offlineMode, allowDownloads, allowUploads | General | General / Server | — |
| diskCacheLimit, download more, clear cache | General | General / Local cache | — |
| planetary cache table | General | General / Local cache | — |
| export / import | General | General / Settings | — |
| reset all settings | Debug | General / Settings | — |
| manual capture | General | Debug | — |
| densityMultiplier, min/maxRadius, enableProximity | Spawn | Ruins on the map | — |
| deteriorationMultiplier, scavengingMultiplier | Spawn | Ruins on the map / Condition | "Scavengers activity" |
| startWithoutRuins, preserveStandardRuins | Spawn | Ruins on the map / Where | — |
| ruinsWealthPerMap (+3 options) | Spawn | Wealth | — |
| itemCostLimit | Spawn | Wealth | "Single item cost limit (regular ruins)" |
| caravanRuinsCostCap, poiRuinsCostCap | Advanced | Wealth | — |
| ruinsCostCap | Advanced | Wealth | "Abandoned base (quest) wealth cap" |
| ruinsScaleWithColonyWealth, ruinsReferenceColonyWealth | Advanced | Wealth / Scaling | — |
| forceMultiplier | Advanced | Threats | "Ruins threat scale" |
| hostileChance | Spawn | Threats | "Chance of inhabitants in ruins" |
| disableFriendlyRaids, useRuinsForcesGenerationV2 | Advanced | Threats | V2: consider removing |
| techLimit* (6) | Tech Level | Content (or keep as its own tab) | — |
| disableSpawnItems, wallsDoorsOnly | Spawn | Content / Filters | show scope |
| spawnRoofs | Advanced | Content / Filters | — |
| spawnBlacklist, materialBlacklist, fallbackMaterial | Advanced | Content / Blacklists | — |
| enableAbandonedRuinsFoundEvent | Advanced | Events & planetary | "Pristine ruins quest" |
| enableCaravanFoundRuinsEvent | Advanced | Events & planetary | — |
| caravanReformType | Advanced | Events & planetary | add a description per mode |
| planetaryRuinsOptions.* | Planetary | Events & planetary | — |
| logLevel, debug categories, keepSnapshots, wealth log, staging | Debug | Debug (dev mode only) | — |

## 5. Smaller UI rules worth applying everywhere

- **Grey out dependent controls** instead of hiding them (reference wealth, proximity/structure/damage options when per-map = ∞, everything under "limit tech level" when it's off). Hiding changes the page height, and players don't find the option.
- **One slider style for limits:** the same minimum, "Unlimited" at the right end, values rounded to 1,000.
- **Every tooltip names its scope:** "Applies to: regular ruins / caravan ruins / abandoned base / ruined locations / intact locations".
- **"Unlimited" in labels** should also be the value used for the import/export string, so that "∞" survives the round trip (it does today; keep it that way).

## 6. Effort

- Moving the controls only: about half a day. It's all `Listing_Standard` code in 6 files plus the new tab list, and `ContentHeight` has to be fixed for each tab.
- Label changes: new English text plus a translation note. Russian can be done in the same pass, the other languages fall back to English.
- "Applies to" checkboxes for the filters: new settings (Scribe, serializer, reset) plus changes in `ScatterOptionsFactory.ApplyContentFilters`. This is a behavior change; I'd do it after the release.
- The Debug tab only in dev mode and Reset moved to General: trivial, and it fixes R6 at the same time.
