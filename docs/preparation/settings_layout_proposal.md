# Settings page: proposed rearrangement

UI only. Every config key stays as it is, so moving controls between tabs needs **no migration** (see `settings_migration.md`). Renamed labels need new English text and translation updates. Keep the keys, change only the text, and the translators get a "changed" diff instead of a "missing" one.

## 1. Principles

- **Tabs by topic.** A setting may appear on **two tabs** when it really belongs to both (for example "Disable friendly raids" is both balance and gameplay). One or two exceptions are allowed if needed.
- **One draw method per setting.** Every tab that shows a setting calls the same method, so the copies can't drift apart in label, tooltip, range or greying. A mirrored control is the same control, not a copy: changing it on one tab changes it on the other.
- **Every tab starts with a short description,** one or two sentences under the caption about what the page is for.
- **Grey out dependent controls** instead of hiding them, so the page height doesn't jump and players can still find the option.
- **One slider style for limits:** the same minimum, "Unlimited" at the right end, values rounded to 1,000.
- **Tooltips name their scope:** "Applies to: regular ruins / caravan ruins / pristine ruins / ruined locations / intact locations".

## 2. Tabs

`[mirror: X]` marks a control that is also shown on tab X. `[moved from …]`, `[renamed]` and `[new]` mark changes.

### Tab 1 — Presets
> *Quick setups for common ways to play. A preset changes only the settings it is about. Your cache, network and debug settings are never touched.*
```
Play style
  ( ) Default   ( ) Balanced   ( ) Nomadic   ( ) Gacha   ...            current: Custom
Tech level
  ( ) Normal   ( ) True neolithic   ( ) True medieval   ( ) Post-apocalyptic   current: Normal
Settings
  [Export settings] [Import settings] [Reset all settings]               [moved from General / Debug]
```
- **Presets are partial.** Each preset is a stored partial settings string applied through `SettingsSerializer`, so only the keys it lists change.
- **The two rows have disjoint keys, so they don't depend on each other.**
  - Tech level presets set only `techLimit*`. `materialBlacklist` and `fallbackMaterial` are reserved for them too, but no tech preset sets them yet.
  - Play style presets never set those keys. They also never set cache, network or debug keys.
  - That way it doesn't matter which row you click first.
- **What the tech level presets mean:**
  - Normal: no cap.
  - True neolithic: Neolithic cap, strict.
  - True medieval: Medieval cap, strict. Things above the cap don't exist in this world.
  - Post-apocalyptic: Medieval cap, **soft**. This is a medieval world after a cataclysm, so leftover industrial and spacer things still appear, rarely. Exact values to be tuned.
- **"current:" shows which preset matches the current settings**, or "Custom" if nothing matches.
- **"Reset all settings" means all settings, including cache, network and debug.** It is not the same as the "Default" play style preset, which only resets the play style keys.

### Tab 2 — Cache and storage
> *Where blueprints come from and where they are stored on your disk.*

No changes, except that "Manually trigger blueprint capture" moves to Debug and export / import moves to Presets.

### Tab 3 — Wealth and balance
> *How much the ruins on your map may be worth, and how dangerous they are. The caps are the main balance tool: whatever else you set, ruins never exceed them.*

Scrollable.
```
Limits
  Regular ruins, per map                     [===|------]  5 000
  Caravan ruins                              [===|------]  25 000
  Pristine ruins                  [renamed]  [=======|--]  Unlimited
  Abandoned planetary locations              [==|-------]  15 000
  Single item cost limit                     [=========|]  Unlimited
  [ ] No haulable items                                          [mirror: Spawn]
  [ ] Walls and doors only                                       [mirror: Spawn]

Scaling
  [ ] Scale all limits with colony wealth
      Reference colony wealth   [==|------]  100 000      (greyed when off)
  Regular ruins: how the limit counts                     (greyed when per-map = Unlimited)
      [ ] Scale with settlement proximity
      [x] Don't count plain walls, doors and floors
      [ ] Count actual value (damage, quality) instead of full price

Threats and difficulty
  Threat scale (all ruins)        [renamed]  [====|----]  ×1.0
  Inhabitants in regular ruins    [renamed]  [=|-------]  10 %
  [ ] Disable friendly raids                                     [mirror: Gameplay]
  [x] Small raids keep coming (pristine ruins) [renamed]       [mirror: Gameplay]
  [ ] No ruins on starting map                                   [mirror: Spawn]
```
- Scavengers stay on Spawn only. They're a tool for shaping the ruins, not for measuring wealth. The cap always wins.
- Abandoned locations % stays on Spawn / Planetary. This tab is about the game map, not the world map.
- **Every row in Limits and Threats names its scope in the label** ("regular ruins", "pristine ruins", "all ruins"). The tooltip only adds details. Nobody should have to hover to learn what a slider affects.
- "Events threat scale" becomes "Threat scale (all ruins)". It scales the animals in regular ruins, the caravan raiders, the raids and raid triggers at pristine ruins, and the starting parties.
- "Hostile units amount" becomes "Inhabitants in regular ruins". The tooltip says: "Chance per ruin fragment that animals (80%) or mechanoids (20%) live in it. Other ruins have fixed values: caravan ruins 80%, pristine ruins always, planetary locations never. Threat scale still applies to all of them." The value is regular-only in code (`ScatterOptionsFactory`: the other kinds hard-code `hostileChance`).
- "Use new raids system" becomes "Small raids keep coming (pristine ruins)". The tooltip says: "While you're at pristine ruins, small groups of enemies keep arriving, a few at a time, on top of the big raids. The more loot is still left, the more of them come. As you carry it off, they thin out. Turn this off to get only the big raids."

### Tab 4 — Spawn and tech level
> *What the ruins look like: how many, how big, how worn, and what may appear in them. The wealth caps are on Wealth and balance.*

Scrollable.
```
Common (all ruins and locations)
  Tech level cap
    [ ] Limit tech level   Max: [Medieval ▾]   Mode: [Strict ▾]
  [x] Spawn roofs
  [ ] No haulable items          applies to: regular, caravan, ruined locations   [mirror: Wealth]
  [ ] Walls and doors only       applies to: regular, caravan, ruined locations   [mirror: Wealth]
  Item blacklist       [                        ]
  Material blacklist   [                        ]
  Fallback material    [Wood                    ]

Regular ruins
  ↳ Tech level cap applies here  [x]                 (techLimitRuins)
  Ruins density               [==|-------]  1.0
  Fragment size               [ 12 ]–[ 40 ]
  [x] Consider settlement proximity
  Additional deterioration    [=|--------]  0.0
  Scavengers activity         [===|------]  1.0
  [ ] Keep vanilla ruins
  [ ] No ruins on starting map                                   [mirror: Wealth]

Events (pristine ruins, caravan ruins)
  ↳ Tech level cap applies here  [x]                 (techLimitEvents)

Planetary locations
  ↳ Tech level cap applies here  [x]                 (techLimitPOIs)
  Download limit   [ 300 ]     Transfer limit   [ 50 ]
  [ ] Discard plain ruins
  [ ] No friendly locations
  Abandoned locations          [===|-----]  25 %
```
- **The tech checkboxes inside the groups belong to the cap, not to the group.** They must not look like separate switches:
  - indent them under a "↳" and label them "Tech level cap applies here", never just "Enable";
  - grey them out while "Limit tech level" is off;
  - their tooltip says "Set the cap in Common above".
- **Events has only the tech checkbox.** That's fine for now. If it still has nothing else to hold when we build it, drop the section and show the events checkbox under Common as "Tech level cap also applies to events".

### Tab 5 — Gameplay
> *Which special sites exist in your game and how you interact with them.*
```
Events
  [x] Pristine ruins quest                 [renamed]
  [x] Caravan spotted small ruins
  Caravan reform at pristine ruins    [Automatic ▾]       ⓘ one line per mode
  [x] Small raids keep coming (pristine ruins)                  [mirror: Wealth]
  [ ] Disable friendly raids                                    [mirror: Wealth]

Planetary locations
  [x] Offer to load planetary locations at game start
  [Download and spawn now]   [Remove all locations]
```
- The rule for Gameplay and Spawn: Gameplay is about **whether** a thing exists and how you interact with it. Spawn is about **what** it looks like.

### Tab 6 — Debug (always visible)
> *Logging and developer tools. You only need these when you report a bug.*
```
  Log level [Errors ▾]   Debug categories [...]
  [ ] Log ruins wealth
  [ ] Keep snapshots after upload
  [ ] Use staging server     ⚠ also shows a red line on Cache and storage while it is on
  [Manually trigger blueprint capture]                    [moved from General]
```

## 3. Mapping table

| Setting | Now | Proposed | Label change |
|---|---|---|---|
| export / import | General | Presets | — |
| reset all settings | Debug | Presets | — |
| *play style / tech presets* | — | Presets | new |
| offlineMode, allowDownloads, allowUploads | General | Cache and storage | — |
| diskCacheLimit, download more, clear cache, planetary cache table | General | Cache and storage | — |
| manual capture | General | Debug | — |
| ruinsWealthPerMap, caravanRuinsCostCap, poiRuinsCostCap | Spawn / Advanced | Wealth / Limits | — |
| ruinsCostCap | Advanced | Wealth / Limits | "Pristine ruins" |
| itemCostLimit | Spawn | Wealth / Limits | "Single item cost limit" |
| ruinsScaleWithColonyWealth, ruinsReferenceColonyWealth | Advanced | Wealth / Scaling | — |
| ruinsWealthScaleWithProximity, IgnoreStructure, CountDamage | Spawn | Wealth / Scaling | — |
| forceMultiplier | Advanced | Wealth / Threats | "Threat scale (all ruins)" |
| hostileChance | Spawn | Wealth / Threats | "Inhabitants in regular ruins" |
| disableFriendlyRaids | Advanced | Wealth / Threats + Gameplay | — |
| useRuinsForcesGenerationV2 | Advanced | Wealth / Threats + Gameplay | "Small raids keep coming (pristine ruins)" |
| startWithoutRuins | Spawn | Wealth / Threats + Spawn / Regular | — |
| disableSpawnItems, wallsDoorsOnly | Spawn | Spawn / Common + Wealth / Limits | show scope |
| techLimitEnabled, MaxLevel, Strict | Tech Level | Spawn / Common | — |
| techLimitRuins / Events / POIs | Tech Level | Spawn / Regular / Events / Planetary | "Tech level cap applies here" |
| spawnRoofs, spawnBlacklist, materialBlacklist, fallbackMaterial | Advanced | Spawn / Common | — |
| densityMultiplier, min/maxRadius, enableProximity | Spawn | Spawn / Regular | — |
| deteriorationMultiplier, scavengingMultiplier | Spawn | Spawn / Regular | — |
| preserveStandardRuins | Spawn | Spawn / Regular | — |
| planetary download / transfer limit, discard plain, no friendly, abandoned % | Planetary | Spawn / Planetary | — |
| enableAbandonedRuinsFoundEvent | Advanced | Gameplay / Events | "Pristine ruins quest" |
| enableCaravanFoundRuinsEvent | Advanced | Gameplay / Events | — |
| caravanReformType | Advanced | Gameplay / Events | add a description per mode |
| planetary: offer at start, download and spawn now, remove all | Planetary | Gameplay / Planetary | — |
| logLevel, debug categories, keepSnapshots, wealth log, staging | Debug | Debug | — |

## 4. Order of work

1. Pull every control into its own draw method (`SettingsControls.DrawX(listing)`), so mirroring is just a call.
2. Build the new tabs (Cache and storage, Wealth and balance, Spawn and tech level, Gameplay, Debug) from those methods, add the descriptions, and fix `ContentHeight` and scrolling.
3. Write the label changes in English and Russian.
4. Presets tab: start with export / import / reset and the preset format. The actual preset values come after a playtest.
