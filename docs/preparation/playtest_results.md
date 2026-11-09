# Playtest results (in progress, 2026-11-09; second run done, fixes in `8709136` and `4d3ad0b`)

Build: `development` @ `a50b298` (DLL), Windows, RimWorld 1.7.5000. Mods: Harmony, Core, all DLCs, RealRuins.
Old build: the baseline `b3124b5` DLL committed in `1.6/Assemblies`, deployed with a `v1.7 → 1.6` load folder. The Workshop copy wasn't used: it's an in-between build without `MapRuinsStore`.
Fixtures: old saves `PT-old-S1` (= S6), `PT-old-S2-manual`, `PT-old-S3-auto`, `PT-old-S4`, `PT-old-S5-poi` in the Saves folder. The FX-2 config and export string are in the session scratchpad. S7 (pods in flight) wasn't made.
Game-over tests (U-04, U-05) are dropped: the game-over mechanics changed in the game itself.

Screenshots are in `%TEMP%\realruins-run\` (overwritten per run). Only the ones that matter are named below.

## Results

| ID | Result | Notes |
|---|---|---|
| **P — Packaging** | | |
| P-01 | PASS | Patching OK, 0 errors on every launch (about 15 launches) |
| P-02 | DROPPED | 1.6 and 1.7 are the same for the mod; only upgrading the mod is tested |
| P-03 | PASS | Empty cache → 100 blueprints from `api.woolstrand.art`. The planetary list from production returned 582 for `shotgun` |
| P-04 | PASS | No `Duplicate def` / `TrippingTrigger` errors in any startup log |
| **M — Migration** | | |
| M-01 | PASS | All old values shown; new defaults 5k/25k/15k, roofs on, tech off. Removed keys dropped silently. Minor: `ruinsCostCap` 50000 → 50000.0078 after opening the tab (log-slider round-trip; the baseline has it too) |
| M-02 | PASS | Old export string imported; blacklist text visible at once |
| M-03 | PASS | S6 loads with no error; `MapRuinsStore` gone after re-save |
| M-04 | PASS | New map in the old save: 5 chunks, limit=5000, 0 errors; save/reload clean (about half a day played) |
| M-05 | PASS (accepted) | Old travelling caravans load already stopped ("Waiting") with no NRE, but also without vanilla's "arrival action no longer valid" message |
| M-06 | PARTIAL | Reform disabled with the manual-lock text; small raids keep coming (destructionLevel 0.62). "You can leave" letter not reached. Load logged 1 `BakedLogEntry` duplicate-ID error (finding 1, fixed → RT-02). Rest → H-1 |
| M-07 | PARTIAL | Reform enabled in auto; lifecycle letter not observed. 146 `BakedLogEntry` + hediff ID errors on load (finding 1, fixed → RT-02). Rest → H-1 |
| M-08 | PASS | "Moderate damage. Restoring owner, activating cooldown for 4 days", 0 errors. 218 `BakedLogEntry` errors on load (finding 1, fixed → RT-02) |
| M-09 | NOT RUN | No S7 fixture |
| M-10 | PASS | `Quest_PristineRuins` → only the "incident removed" letter |
| M-11 | PASS | New saves store `mapParent`; both caravans arrive after reload. Destroyed target → vanilla "couldn't reach its destination", no NRE |
| **S — Settings UI** | | |
| S-01 | PASS | All tabs scroll to the bottom; the reference-wealth row appears with colony scaling on; the planetary table is in one column |
| S-02 | PASS | Reset clears the blacklist fields at once |
| S-03 | PASS | Export has 50 keys incl. the 6 tech level and 9 wealth settings. Changed the tech toggles, imported: "Settings imported successfully", toggles back to the exported values |
| S-04 | PARTIAL | Slider clicks at the ends didn't move the caravan and POI caps (clicks on the track, not drags). → H-5 |
| S-05 | NOT RUN | |
| S-06 | → H-5 | |
| **C — Storage & cache** | | |
| C-01 | PASS | Table sorted by seed: seed, coverage, size, count, MB |
| C-02, C-03, C-04, C-05 | NOT RUN | Paused before C-04 |
| C-06 | PASS | Same seed again: "Already available: 577, new blueprints: 5"; only the 5 new ones downloaded (2 fail as usual). Transfer used 580 local blueprints |
| **U — Server & uploads** | | |
| U-01 | PASS | Save → `Uploading file …RealRuinsPendingUploads\…bp` → "Map upload successful", pending folder emptied, file kept in `%TEMP%` (debug keep). One small test snapshot went to **production** |
| U-02 | PARTIAL | Retry path verified: a file in the pending folder at startup → "Retrying pending upload" → success (against staging). The network block itself wasn't simulated → H-8 |
| U-03 | PASS | Uploads off + a pending file → the file is deleted at startup |
| U-04, U-05 | DROPPED | The game's game-over mechanics changed |
| U-06 | NOT RUN | |
| U-07 | PASS | Offline: save writes `RealRuins/local-1893566810.bp`, no network call |
| U-08 | NOT VERIFIED | The uploaded snapshot (28×23, crash-landing area) has no pawns |
| **T — Tech level** | | |
| T-01 | PASS | Cap off: spacer and industrial things present (defaults sample) |
| T-02 | PASS | Medieval strict, density 3, unlimited wealth: only walls, wood, wooden beds and leather armchairs in the chunk top lists; 0 errors |
| T-03 | PASS | Limited, 10 maps: a few industrial things (power conduit 5, cooler 4, fueled stove 4, hydroponics 6) among hundreds of medieval ones; no spacer things |
| T-04 | PASS | Events-only cap: generic ruins keep power conduits, biosculpter pods and flak jackets. The abandoned base at the same settings had only gold, a steel reliquary and psychite tea in its top list. Caravan ruins: walls, wooden and steel beds |
| T-05 | PASS | Medieval cap, planetary game: POI icons are era types (village, camp, castle, storage); factions are tribal. Started at a Village with attack: no conduits, solar, batteries, turrets, research benches or electric stoves in the save. The few industrial items are world-pawn gear and the crash-landing start kit |
| T-06 | PASS | 300 blueprints: Medieval/Neolithic give only Camp, Castle, Outpost, Storage, Village, Workshop, Ruins. CSV written |
| T-07 | → H-7 | No tech mods in the run |
| **W — Wealth** | | |
| W-01 | PASS | 10-map sample: counted ≤ 4,799, p50 1,808, limit 5,000; later chunks still get loot |
| W-02 | PASS | Unlimited: limit=1e9, counted up to 7.5k, median actual 5.7k |
| W-03 | PASS | Structure counted: counted = estimate on every map |
| W-04 | PASS | Count actual value: counted (median 573) ≪ estimate |
| W-05 | PASS | Scale with proximity: limit 2,456 at dist 16, 3,323 at 11, 4,410 at 8 |
| W-06 | PASS | Default: main ruin 23,798 ≤ 25k, minimum 15k. Unlimited: no cut |
| W-07 | RECORDED | Caravan cap 0: the main ruin is completely empty (0 things, not even walls), yet a 693-point raider party spawns from the random bonus. Fixed: minimum 1,000 → RT-04 |
| W-08 | PASS | Cap 50k: inspect string "low (49851)"; map wealth 25k counted; site cap 49,851 |
| W-09 | PASS | Abandoned 100%: three POIs at 14,825 / 9,377 / 1,732 (blueprints 49k / 24.6k / 17k), all ≤ 15k |
| W-10 | PASS (×3 not reached) | (a) starting map limit 2,500 (×0.5). (b) After spawning gold: limit 5,594 (×1.12 at about 112k colony wealth). The first sample still used the old wealth: vanilla recounts wealth only every 5,000 ticks |
| W-11, W-12 | → H-6 | |
| **D — Threats** | | |
| D-01 | PASS | Hostile chance 1.0: no force generation on the starting map |
| D-01b | PASS | Camp map in the same game: animal generation on all 10 chunks |
| D-02 | NOT VERIFIED | Ran about 2 days at fast speed; no `Releasing … animal inhabitants` line seen in the window watched |
| D-03 | NOT RUN | |
| D-04 | PASS | Force multiplier 0: caravan ruins "0 points of raiders"; abandoned base generated with no `ForceGen` lines, no triggers; the ruins are still there |
| D-07, D-08 | → H-3, H-2 | |
| D-05 | PASS* | Points = value/40 plus a 50% chance of up to about 1,500 random points; the random part dominates cheap ruins (2.5k ruin → 1,218 points, 7.5k ruin → 294) |
| D-06 | PASS | Trigger expected at about tick 3453, fired at 3210 |
| **R — Regular ruins** | | |
| R-01 | PASS | Dist 6 → 7–8 chunks, dist 16 → 2 of 4 maps empty, `count x…` logged |
| R-02 | PASS | Proximity off: dist=0 logged, flat count 7–12 |
| R-03 | NOT RUN | |
| R-04 | → H-4 | Preserve on: map generated, but vanilla ruins don't log and weren't checked visually |
| R-05 | PASS | Start without ruins: the starting map had no `Spawning N ruin chunks`; the next map (caravan event) had regular ruins |
| R-06 | PASS (off) / → H-4 (on) | Roofs off: no roof inside ruin rooms. On: 10 sampled maps logged no `Removed N roof cells` line (nothing to remove, or nothing floating); whether a floating roof collapses needs play → H-4 |
| R-07 | PASS | Walls & doors only: generic ruins have only walls and doors; the abandoned base keeps its full contents (multi-analyzer, fabrication bench, autocannon) |
| R-08 | PASS | Density 0.1: every map has ≥ 1 chunk (250×250 maps, not a small map) |
| **E — Events** | | |
| E-01 | PASS | 1,000 rolls: RealRuins_AbandonedBase 10.1% |
| E-02 | PASS | Event off: the quest never appears |
| E-03 | PARTIAL | Quest → DEV: Explore → map, raids, triggers. Site expiry not observed (still to run) |
| E-04 | PARTIAL | See M-06 (manual lock shown); walking off the edge and the unlock → H-1 |
| E-05 | PASS (partial) | Instant: reform blocked with "There are still some enemies in this area" while hostiles are present. Instant reform after the fight → H-2 |
| E-06 | PARTIAL | Reform enabled under fire, item list, route; formation starts. The caravaners died before leaving → H-2 |
| E-06b | PASS | "The caravan can't carry that much", mass shown in red |
| E-07 | PASS | Caravan small ruins: medium ruin + 4 regular chunks, raiders scaled, 0 errors |
| E-08 | NOT RUN | |
| E-09 | PASS | `caravanReformType` 5 shown as Automatic; reform enabled with raiders present; 0 errors |
| **L — Planetary** | | |
| L-01 | PASS | Transfer limit 100: "Reached limit of 100 blueprints" (no "Transfer finished" line when the limit stops it). Closing the window mid-transfer: transfer continued (580 local, 392 sites) |
| L-02 | NOT RUN | |
| L-03 | PASS | Village, Castle, Workshop, Factory, City and other icons render; abandoned POIs white |
| L-04 | PASS | Own-tile start: shelves player-owned, items not forbidden, no fog, no defenders |
| L-05 | PASS | Attack start: citizen defenders + triggers for the POI faction; no animal or mechanoid generation |
| L-06 | PASS | "Discarding starting POI chosen for another tile", normal map |
| L-07 | PASS | Chose a POI, backed out to the main menu, loaded a save, attacked a pirate base: "Game start context is empty", normal base map |
| L-08, L-09 | → H-4 | |
| L-10 | PASS | 300 blueprints, no cap: large military base 2, small 8 |
| L-11 | **FAIL** → fixed | See finding 2 → RT-01 |
| **X — Compatibility** | | |
| X-01 | PASS | About 90 maps generated over the run (samplers + quicktests); no `Failed to spawn` storms; only "[2] Failed to load, recovering…" (recovered) |
| X-03 | NOT RUN | |
| X-02, X-04 | → H-7 | |
| X-06 | → H-4 | |
| X-05 | PASS (new build) | Fresh pristine ruins save/reload: 0 errors. Old saves: see finding 1 |
| **G — Debug** | | |
| G-01 | PASS | All samplers worked, 0 errors. After a 10-map sample and opening/closing the settings, the config still has `logLevel` 0 and `debugLogRuinsWealth` True |
| G-02 | NOTE | After a reload, DEV: Reset says "not entered with Explore: reverted as far as known" |
| G-03 | PASS | Staging on: planetary list goes to `staging.woolstrand.art`, pending upload retried there |
| **RT — Retests** | | |
| RT-01 | PASS | (a) Back out to the menu with an attack POI, quicktest: `Discarding starting POI of another game or tile`, normal map, no `null relation` errors (one unrelated popup, see popups #1). (b) Same, then New colony on the same tile: `Game start context is empty`, normal start. (c) Attack start still works (POI map, `Found starting poi`, citizen defenders + triggers for the POI faction, 0 errors); takeover start works (0 errors); L-06 `Discarding starting POI chosen for another tile`. L-07 load part: S2 loaded after backing out with a stored POI, but no enemy base was attacked afterwards |
| RT-02 | PASS | S2, S3, S5 load with **0** duplicate-ID errors (first run: 1 / 146 / 218). Remaining on first load: S3 5× beard, S5 12× beard + 24× null part (old-build pawn data, vanilla removes it: popups #2, #3). Re-saved S2/S3/S5 reload with 0 errors; 1/145/253 log entries and 0/18/39 hediffs got negative ids. New build: abandoned base with 65 baked log entries, all with own ids, save/reload 0 errors |
| RT-03 | PASS (+ new bug, fixed) | Offline: save with home < 300 cells made nothing; painted home, saved at once → snapshot made (slot not used up). But the file was **0 bytes**: new finding 8, fixed in `4d3ad0b`, re-checked on staging: valid XML (132 cells, 19 items), upload successful |
| RT-04 | PASS | Config 0 → caravan event used `needed 1000` and the cap; label now shows 1,000 (fix in `4d3ad0b`, before it showed 0). With cap 1,000 the main ruin was tiny (worth 2) and got 0.06 raider points — no longer "empty with raiders". Slider drag → H-5 |
| RT-05 | PASS | `ruinsCostCap` 50000 in the config: tab drawn, settings closed → still `50000`. Drag → H-5 |
| RT-06 | PASS | "Planetary ruins" between Factions and Next; opens the loader with "Remove all spawned locations". Next → Back: loader doesn't reopen. "Allow on start" off: vanilla 4 buttons, no loader. Map-size change with cleanup not re-checked (changing it would have dropped the RT-01 context; the code path is unchanged). No `layout not recognized` warning |
| **H — Hand tests** | | |
| H-1 … H-8 | FOR THE USER | See the plan |

## Findings

1. **Fixed in `8709136` (RT-02).** **Old saves: duplicate `BakedLogEntry` IDs on load (S2: 1, S3: 146, S5: 218 errors)**, plus `Hediff_X` ID collisions, "had a beard. Removed" and "null part after loading" for restored pawns. `BakedLogEntry` never sets `logID`, so entries with the same `ticksAbs` register as the same `LogEntry_<ticksAbs>_0`. Pre-existing (the baseline writes the same data, the file is unchanged), not a regression. The hediff collisions come from restored hediffs keeping the blueprint game's `loadID`; the beards from switching the gender after the pawn was generated. Fix: new log entries and restored hediffs get fresh ids, pawns are generated with the blueprint's gender, and duplicate ids in existing saves are renumbered on load (`DuplicateLoadIds`). A new-build save with only 4 baked entries reloaded clean, so the collision wasn't exercised on the new build.
2. **Fixed in `8709136` (RT-01).** **L-11 FAIL: the starting POI survives backing out to the main menu.** Chose a City POI with "attack", went Back through every page to the main menu, then Dev quicktest: the quicktest map was generated as that POI ("Found RR POI in game start context … changing to RealRuinsPOI", POI blueprint placed on the new tile). Then 4,000+ errors "Faction Union of Welgopli has null relation with …" (the POI faction from the abandoned setup doesn't exist in the new game). `SessionState` resets on `GoToMainMenu` and `LoadGame`, but backing out through the setup pages doesn't call `GoToMainMenu`, and `InitNewGame` reads the context before its finalizer resets it. The L-07 path (load a save) is fine. Blocker-level for the R9 fix: reset the context when a new game setup starts (e.g. `Page_SelectScenario` / `Page_CreateWorldParams`), or check in the `GenerateMap` prefix that the POI belongs to the current world.
3. **Fixed in `8709136` (RT-03).** **Upload 3-hour slot used up by a skipped snapshot (pre-existing).** `UploadMapSnapshot` writes `snapshotTimestamps` before `CanGenerate()`. A save with a home area < 300 cells blocks uploads of that map for 3 hours, even after the home area grows. The baseline has the same order.
4. M-05: old caravans don't get vanilla's message; they just stop. No crash. **Accepted, no fix**: it only affects caravans already travelling in an old save, once; the player sends them again.
5. **Fixed in `8709136` (RT-04).** W-07: caravan cap 0 gives an empty ruin with raiders. The minimum is now 1,000.
6. **Fixed in `8709136` (RT-06).** Minor UX: the planetary loader window opens again every time the site-selection page opens, even with POIs already placed.
7. **Fixed in `8709136` (RT-05).** Minor: the `ruinsCostCap` log slider writes 50000.0078 for 50000 just by being drawn (pre-existing).

8. **New, fixed in `4d3ad0b`: snapshots were truncated XML.** `SnapshotGenerator` read its `StringBuilder` while the `XmlWriter` still held buffered output. On 1.7 the output came out **empty or cut off** at whatever had been flushed so far (only `EncodePawn` flushes). Seen in this run: `local-….bp` 0 bytes; the first run's U-01/U-07 files also end mid-element. Every upload from 1.7 so far (incl. the one to production in U-01) is broken data. Pre-existing logic, but the baseline on 1.6/Mono may have behaved the same: worth a server-side check of recent uploads.
9. **Offline mode deletes all downloaded blueprints at startup** (`SnapshotStoreManager.CheckCacheContents`, same in the baseline). With offline mode on and no local snapshots, generic ruins are skipped ("low blueprints count"). Intended by design ("offline" = only your own bases) but surprising: switching offline on wipes a 700-blueprint cache. Decision needed; not changed.
10. Caravan ruins at the 1,000 cap: the main ruin is nearly empty (a 13k blueprint cut to ~2). Raiders scale with value, so ~0 raiders. Fine, but the minimum could be higher if a near-empty ruin should not happen.

Error-log popups of the run are listed in `playtest_popups.md` (one still needs a check without the mod).

## Tooling changes during the run

- `driver.sh drag X1 Y1 X2 Y2` (Windows): left-drag, used to paint a home area. Added to `win-input.cs`, `platform/win.sh`, `driver.sh`, documented in `SKILL.md` (commit `d5e6c8a`).

## Not run yet

By the driver: RT-07, RT-08 (popup #1 without the mod), M-09, S-05, C-02–C-05, U-06, U-08, W-10 (×3 part), D-02 (release), D-03, R-03, E-03 (expiry), E-08, L-02, L-07 (attack after the load), RT-06 (map-size change), X-03, G-02.
By the user: H-1–H-8.

## Current environment state

Game closed. `Config/ModsConfig.xml`, `Prefs.xml` and `Mod_RealRuins_RealRuins_Mod.xml` restored from the backup. `Mods/RealRuins` has the `4d3ad0b` build. The blueprint cache was refilled from production (~650 blueprints; offline mode had wiped it, finding 9). Test saves `PT-rt02-*`, `PT-rt03`, `PT-snap` and the `PT-old-*` fixtures are in the Saves folder. One test snapshot went to **staging** (RT-03).
