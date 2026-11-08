# Playtest plan for the release

Baseline `b3124b5` → `development` `15ee008`. The changes are listed in `changes_by_group.md`, the risks in `regressions_list.md` (R#), and settings and saves in `settings_migration.md`.
The tests are run with the `run-realruins` skill (`.claude/skills/run-realruins/driver.sh`). Read its `NOTES.md` before the run and add to it after.

## 0. Conventions

- **ID**: group letter + number. **Prio**: P0 = release blocker, P1 = must pass, P2 = should pass.
- **Log check**: `M=$(driver logmark)` before each action, then `driver logsince $M RealRuins` and `driver errors $M`. Unless a test says otherwise, every test also requires **0 new errors/exceptions** from RealRuins.
- Default test settings unless stated: Dev mode on, RealRuins **log level = all**, `debugLogRuinsWealth` on (it gives `Chunk …` and map summary lines with `limit=`), 1.7, Harmony + RealRuins only. Storyteller Cassandra, Strive to Survive, unless stated.
- After each test group: restore defaults (Network & Cache → reset), unless the next test needs the state.

## 1. Fixtures to prepare (once, before testing)

| ID | Fixture | How |
|---|---|---|
| FX-1 | **Old build** to create old data | Best: the version currently on the Steam Workshop (what players have). If it can't be used: `git worktree add ../rr-baseline b3124b5`, build, and deploy under the same packageId. Make sure only one copy is active. |
| FX-2 | **Old config** with non-default values | With the old build: set every old option to a non-default value (log level warnings, force ×1.5, cost cap 50k, caravan reform = manual, density 2.0, radius 10–30, scav 0.5, deterioration 0.5, item limit 500, hostile chance 0.3, walls/doors only on, proximity off, blacklist `Hive, Sarcophagus`, material blacklist `Gold`, fallback `Steel`, abandoned event **off**, caravan event on, preserve standard ruins on, planetary download limit 300, transfer limit 50). Save. Copy `Config/Mod_*RealRuins*.xml` to the fixtures folder, plus the **export string** from the old Import/Export dialog. |
| FX-3 | **Old saves** made with the old build | S1: a plain colony. S2: an abandoned base (quest) **map open, mid-fight**, with raid triggers left (manual mode). S3: the same in **auto** mode, map open, all triggers fired, enemies dead. S4: a **caravan travelling** to a POI, and a second one travelling to an abandoned base. S5: a game started with **planetary ruins**, with an owned POI map **open** (entered, defenders killed). S6: a save that contains `MapRuinsStore` (any game from an old version). S7: pods in flight to ruins. |
| FX-4 | A **1.6 install** | A Steam beta branch or a second install of RimWorld 1.6. Needed only for P-02. **If it isn't available, ask the user.** |
| FX-5 | Blueprint cache | At least 500 blueprints in the cache (the "Network & Cache" count). Generic ruins need ≥10. |

---

## P — Packaging & startup (P0)

| ID | Prio | Test | Expected |
|---|---|---|---|
| P-01 | P0 | Clean start on 1.7 with Harmony + RealRuins. Main menu. | `RealRuins started patching`, no Harmony errors, no `MissingMethod`/`TypeLoad`. Mod icon visible in the mod list. |
| P-02 | P0 | Same on **1.6** (FX-4). New game, map generated, open the settings tabs. | Same as P-01. This checks R2, the shared DLL. Any `MissingMethodException` is a blocker. |
| P-03 | P0 | **Server host (R1).** Online mode, fresh cache folder (rename the old one). Start the game and wait. | Blueprints download (the cache count grows), no `Could not load maps list`. Repeat with planetary: `Get count` returns a number. |
| P-04 | P1 | 1.7 game: Defs load once. Dev console → no duplicate def warnings for `RaidTrigger`, `ScatterRealRuins`, `RealRuins_AbandonedBase`. | No `Duplicate def` / `TrippingTrigger` errors. |

## M — Migration (P0)

| ID | Prio | Test | Expected |
|---|---|---|---|
| M-01 | P0 | Put the FX-2 config in place, start the new build, open every settings tab. | Every old value is shown unchanged (see the table in `settings_migration.md` §1). New settings show their defaults: per map 5,000; caravan 25,000; POI 15,000; roofs on; tech level off. No errors. Removed keys (`allowInstantCaravanReform`, `claimableBlocks`) cause no message. |
| M-02 | P1 | Import the FX-2 **export string** into the new build (after a reset). | All old values restored, new ones unchanged, no error. The text fields (blacklists) show the imported text **without reopening** the window. |
| M-03 | P0 | Load **S6** (MapRuinsStore). | Loads with no error about `RealRuins.MapRuinsStore`. Save again, then reload: the node is gone from the XML. |
| M-04 | P0 | Load **S1**, play 1 day, save, reload. | No errors. Regular ruins on new maps work (form a caravan, settle a new tile, or use dev "Generate map"). |
| M-05 | P0 | Load **S4** (caravans travelling). Select each caravan (inspect pane), let it arrive. | R4 fixed: the caravan stops with vanilla's "arrival action no longer valid" message, no NRE in the inspector or on arrival. |
| M-06 | P1 | Load **S2** (manual, mid-fight). | Raids keep coming (`destructionLevel` recalculated, not 0). The reform button is disabled with the manual-lock text. After the raids, the "you can leave" letter, then reforming works. |
| M-07 | P1 | Load **S3** (auto, after the fight). | No errors. The lifecycle continues: a letter may appear ("informed"). Note the behavior; it's expected (`settings_migration.md` §4). The reform button works. |
| M-08 | P1 | Load **S5** (POI map open), leave the map (form a caravan, wait until the map is removed). | No errors. Owner restored or the POI abandoned, depending on the loot; the cooldown is applied. |
| M-09 | P2 | Load **S7** (pods in flight). | They arrive at the ruins normally. |
| M-11 | P1 | **New build:** send one caravan to an abandoned base and one to a POI, save while they travel, reload. Then send another one and remove its target while it travels (dev: destroy the world object). | After reload both arrive and enter (the target is now saved). The one with the removed target stops with the vanilla "arrival action no longer valid" message, no NRE (R4). |
| M-10 | P1 | Old save with an active abandoned base (from the old incident) and one from the quest. | Both still enterable. Debug → Do incident `Quest_PristineRuins` → only the "retired" letter, no site. |

## S — Settings UI (P1)

| ID | Prio | Test | Expected |
|---|---|---|---|
| S-01 | P1 | Open every tab and scroll to the bottom. | Nothing is cut off. Advanced grows when "scale with colony wealth" is on. Network & Cache shows the planetary table without a second column. |
| S-02 | P1 | Reset to defaults while the blacklist fields contain text. | The fields clear immediately. |
| S-03 | P1 | Export, then change values, then import. | Values are restored, including the 6 tech level and 8 wealth settings. |
| S-04 | P2 | Caravan and POI cap sliders: drag to 0 and to max. | Max shows "Unlimited". 0 behavior: see W-07 (R20). |
| S-05 | P1 | **R6:** turn dev mode off. Is the Debug tab visible? Is the staging toggle visible? | Record it. If staging is reachable without dev mode, report it (release decision). |
| S-06 | P2 | Switch the game language to Russian, then Japanese, French, German, Chinese. Open all tabs and the planetary loader. | No raw keys (`RealRuins.…`). Untranslated strings are shown in English. |

## C — Storage & cache (P1)

| ID | Prio | Test | Expected |
|---|---|---|---|
| C-01 | P1 | Planetary cache table with ≥2 worlds in the cache. | Rows: seed, coverage %, size NxN, count, MB. Sorted by seed. |
| C-02 | P1 | Clear a world that **isn't** the loaded one. | Confirmation dialog with counts; the folder is deleted; the table updates within ~1 s. |
| C-03 | P1 | **R11:** in a running planetary game, clear **its own** folder, then enter an unvisited POI. | Record it. Expected now: an empty map plus the warning `Could not load POI blueprint`. Release decision: block or warn. |
| C-04 | P1 | Set the disk cache limit low (for example 10 MB) with a full cache, then close the settings. | Cache trimmed to the limit, no `FileNotFoundException` (M15). |
| C-05 | P2 | Set the limit to unlimited. | No trimming (C2). |
| C-06 | P1 | Planetary loader twice for the same seed (start a new game with the same seed). | The second run downloads only **new** blueprints (`Already available: N, new blueprints: M`). The number of POIs doesn't shrink. |

## U — Server & uploads (P1)

| ID | Prio | Test | Expected |
|---|---|---|---|
| U-01 | P1 | Uploads on. Play a home map, then save (the upload trigger). | `Uploading file …`, then success; the file is removed from `RealRuinsPendingUploads`. |
| U-02 | P1 | Block the network (offline mode **off**, block the host in the hosts file), save, then quit. Start again with the network back. | The file stays in `RealRuinsPendingUploads`, then `Retrying pending upload` and success on the next start. |
| U-03 | P1 | Turn uploads off with files in the pending folder, then restart. | The pending files are deleted. |
| U-04 | P0 | **R3:** game over by "everyone is dead" while **looking at the home map**. | `GameEndTick` triggers one upload, ignoring the 3 h limit. |
| U-05 | P0 | **R3:** game over while the **world view** is open (`CurrentMap == null`), and once while looking at a raided POI map. | The home map is uploaded, no NRE; the POI map is never uploaded. |
| U-06 | P1 | Planetary game: clear a POI so it becomes player-owned, then save while on that POI map. | `Skipping snapshot of a planetary POI map`, no upload. |
| U-07 | P2 | Offline mode: save. | Stored locally as `local-…`, no network call. |
| U-08 | P2 | Snapshot content: open a generated `.bp` (debug keep files on). | `chronologicalAge` ≠ `biologicalAge` for an old pawn. |

## T — Tech level (P1)

| ID | Prio | Test | Expected |
|---|---|---|---|
| T-01 | P1 | Tech limit off. Generate 3 maps. | Ruins as before (spacer and industrial things present). |
| T-02 | P1 | On, Medieval, strict, all groups. New map (generic ruins). | No industrial or spacer things or terrain. Walls and doors above the cap become plain ones (rooms stay closed). Containers have no above-cap contents. No error. |
| T-03 | P1 | Same, **limited** strategy. Sample with "Sample ruins wealth" or several maps. | A few Industrial things (~10%), Spacer very rare (~1%). |
| T-04 | P1 | Groups: only "events" on. Generic map vs abandoned base (debug "Generate pristine ruins quest" → DEV: Explore) vs caravan ruins. | The cap applies only to the abandoned base and caravan ruins. |
| T-05 | P1 | Cap Medieval plus the POI group, then a new planetary game. Inspect POIs. | POI types are era types (Village, Castle, Workshop, Camp, Outpost, Ruins). POI factions are ≤ medieval (strict). Entering one shows no above-cap things. |
| T-06 | P2 | "Sample POI types" from the main menu at Neolithic and at Ultra. | The CSV in SaveData is written. The type spread changes with the era. |
| T-07 | P2 | Modded content (if available: one tech mod with custom materials). | Modded things get a sensible era (research or ingredient based), no exceptions. |

## W — Wealth control (P1)

Use `debugLogRuinsWealth`. The map summary line includes `limit=` and the total `counted`.

| ID | Prio | Test | Expected |
|---|---|---|---|
| W-01 | P1 | Defaults (5k per map). Generate 5 generic maps at different settlement distances. | Counted total per map ≤ ~5k (+ basic structure, which is ignored). No map has "all wealth in the first chunk": later chunks still have some loot. |
| W-02 | P1 | Per map = Unlimited. | No `limit=` cut; the wealth is like the old version. |
| W-03 | P1 | "Ignore basic structure" off (R7: now read through `PipelineSettings`). | Walls and floors count; less loot fits. |
| W-04 | P1 | "Count actual value" on (R7). | Counted value < estimate (damage); more items fit. |
| W-05 | P2 | "Scale with proximity" on. Map near a settlement vs far away. | The limit is higher near settlements (the factor is in the log). |
| W-06 | P1 | Caravan ruins: debug-fire the caravan "found ruins" incident with defaults, then with the cap unlimited. | Default: the main ruin ≤ 25k (`Caravan ruins worth …`); the blueprint minimum is 15k. Unlimited: as before. |
| W-07 | P2 | Caravan cap = 0 (R20). | Record what spawns (expected: almost only structure). Decide whether the slider minimum should change. |
| W-08 | P1 | Abandoned base: cost cap 50k, quest via the debug action, look at the inspect string, DEV: Explore. | The wealth description matches the cap (dev mode shows the number). Map wealth ≈ ≤ cap. Golden walls may be dismantled (`cutDismantlesWalls`). |
| W-09 | P1 | POI cap: new planetary game with POIs as ruins (abandoned %, 100). Enter 3 abandoned POIs. | Each ≤ 15k. |
| W-10 | P1 | Scale with colony wealth on, reference 100k. (a) The starting map. (b) A colony worth about 300k (dev: spawn silver): new map and quest. | (a) Limits ×0.5. (b) ×3 (log or inspect string). |
| W-11 | P2 | Scavenging: inspect several ruin chunks with bookcases or shelves. | Containers are partly emptied rather than the whole container gone. |
| W-12 | P2 | Item cost limit 200: a container with expensive and cheap contents. | The expensive contents are removed, the container and cheap contents stay. |

## D — Threats & defenders (P1)

| ID | Prio | Test | Expected |
|---|---|---|---|
| D-01 | P1 | New game (any start). The starting map. | No animal or mechanoid inhabitants in ruins (also with a POI attack start). |
| D-01b | P1 | Same game, hostile chance 1.0: settle a second colony or get a caravan encounter map. | Inhabitants spawn there: the starting-map flag (`SessionState`, set only while `Game.InitNewGame` runs) is off (R9). |
| D-02 | P1 | Hostile chance 1.0. A new non-starting map in a biome with predators. | Animal groups of ≤5, meat eaters only, in ruin rooms. After 2–4 days (dev time skip) and no fight: `Releasing N animal inhabitants`, they become wild (no faction). |
| D-03 | P2 | Same in a biome without suitable animals (sea ice). | `No wild animals in biome … fit … points` and no crash. |
| D-04 | P1 | Threat scale 0 (custom difficulty) or force multiplier 0. | No inhabitants, no caravan raiders, no raid triggers. The abandoned base still has its ruins. |
| D-05 | P1 | Caravan ruins: a cheap ruin vs an expensive one (cap unlimited). | Raider points grow with the ruin's value, max 5,000 × difficulty. A cheap ruin may have none. |
| D-06 | P1 | Abandoned base: enter, count the raid triggers (log `Raid trigger N spawned … expected to fire at about tick T`), and play until the first one fires. | It fires at about T (the rare ticker). |
| D-07 | P1 | A friendly faction raid on ruins near gravship cargo or player items. | Friendly raiders don't take player-owned or substructure items. Hostile raiders behave like vanilla. |
| D-08 | P2 | V2 forces: an abandoned base, many small raids while the base is intact; loot heavily. | Raid chance drops as wealth drops (`Recalculating raid chance`). |

## R — Scattering: regular ruins (P1)

| ID | Prio | Test | Expected |
|---|---|---|---|
| R-01 | P1 | Proximity on: maps at distance 1–3, 8, and 16+ from a settlement. | More and bigger chunks near settlements, fewer far away; 50% of maps at 16+ have none. The log shows `count x…`. |
| R-02 | P1 | Proximity off. | A flat count by density. |
| R-03 | P1 | A quest site map (for example an item stash or a prisoner rescue quest). | Ruins don't overlap the quest's buildings or items (`UsedRects`). |
| R-04 | P1 | Preserve standard ruins **on**: vanilla ruins appear once. **Off**: no vanilla surface ruins. Cave or underground map (if available): underground vanilla ruins still present. | |
| R-05 | P1 | Start without ruins on, new game. | No real ruins on the starting map; the next maps have them. |
| R-06 | P1 | Spawn roofs off (now read through `PipelineSettings`, R7). Generate a map plus a POI. | No roofs anywhere in the ruins (and intact POIs). Spawn roofs on: no floating roofs (`Removed N roof cells without support`); nothing collapses on a pawn walking in. |
| R-07 | P2 | Walls & doors only, and "no items", for generic ruins. | Only walls and doors / no items. Abandoned base and intact POI **not** affected (the new scope). |
| R-08 | P2 | Very low density (0.1) on a small map. | At least 1 chunk (ceil). |

## E — Scattering: events (P1)

| ID | Prio | Test | Expected |
|---|---|---|---|
| E-01 | P1 | "Simulate random quest rolls" (event toggle on). | The `RealRuins_AbandonedBase` share is plausible (weight 1.5). No world objects are left behind by TestRun (the world object count doesn't change). |
| E-02 | P1 | Event toggle **off**, then simulate. | The quest never appears. |
| E-03 | P1 | "Generate pristine ruins quest", accept, travel by caravan. | The map generates, raids come, the lifecycle runs, the site expires on time. |
| E-04 | P1 | Caravan reform **manual** at the abandoned base. | The reform button is disabled with the manual text while raids can come; leaving across the edge works; after the "you can leave" letter, reforming works. |
| E-05 | P1 | Reform **instant**. | Blocked while hostiles are present; instant reform after. The site stays unowned (no faction flip). |
| E-06 | P1 | Reform **auto** under fire: raiders on the map. Press Reform, choose a route, pick some colonists and loot, keep the other colonists drafted. | The button is enabled while hostiles are present. The dialog lists all items (inventories included). Without a route: "must choose route". The caravan starts forming (vanilla "caravan formation started" message); undrafted caravaners haul during the fight while drafted pawns keep fighting; the caravan leaves via the edge. Pawns can still walk off the edge on their own. No errors. |
| E-06b | P1 | Same, but select more than the caravan can carry. | Refused with the vanilla "too heavy" message and the mass readout flashes; nothing starts forming. |
| E-07 | P1 | Caravan small ruins event (debug incident on a caravan). | A medium ruin with regular chunks around it, raiders scaled, no crash; caravan reform is normal. |
| E-09 | P2 | Put an invalid reform mode into the config (`<caravanReformType>5</caravanReformType>`), start, open Events & Advanced, enter an abandoned base. | The setting shows Automatic and behaves as automatic (E-06). No errors (`CaravanReformPolicy`). |
| E-08 | P2 | The quest when no blueprint fits (move the cache away). | The quest isn't offered, or the site keeps the cap; it never expires immediately. |

## L — Scattering: planetary (P1)

| ID | Prio | Test | Expected |
|---|---|---|---|
| L-01 | P1 | A new game with planetary ruins: Get count → Load → Transfer (limit 100). Close the loader window during the transfer. | The transfer continues; `N of M listed blueprints are available locally`; `Transfer finished, K sites created`; the game stays responsive (budget of a few ms per frame). |
| L-02 | P1 | Stop the transfer halfway. | It stops; the state goes back to Idle; no orphan objects. |
| L-03 | P1 | POI types on the world map: check the icons for Village, Castle, Workshop, Shipwreck. | New icons render with a white fill. |
| L-04 | P1 | Start at an owned POI: **take over**. | The player owns everything, nothing is forbidden, no fog, no defenders. |
| L-05 | P1 | Start at a POI with **attack**. | The faction defends; no animals or mechanoids; ruins aren't added on top. |
| L-06 | P1 | Choose a POI, go Back, choose another tile, start. | A normal start (`Discarding starting POI chosen for another tile`). |
| L-07 | P0 | Choose a POI, go back to the main menu, load an existing save, attack an enemy base. | The base map is normal, **not** turned into a POI (the start context is dropped on load by `SessionState`; the log shows `Game start context is empty`). |
| L-11 | P1 | Choose a POI at game setup, back out to the main menu through the setup pages, then start a **new** game on a normal tile. Separately: play a game started at a POI, quit to the main menu, start another new game. | Both start normally, with no leftover POI context. The second new game shows the planetary loader again if it is enabled (R9). |
| L-08 | P1 | Enter a faction POI by caravan, by pods and by **shuttle**. | All three offer options (drop at edge / center, shuttle landing). Relations drop as with a vanilla settlement attack. |
| L-09 | P1 | Clear a POI, leave with little loot / with heavy loot. | The owner is restored with a cooldown / the POI is abandoned or destroyed. |
| L-10 | P2 | Military base sizes: sample POI types. | Fewer "large military" than before; the distribution looks plausible. |

## X — Compatibility & stability (P1, P2 with mods)

| ID | Prio | Test | Expected |
|---|---|---|---|
| X-01 | P1 | Generate 10 maps with a cache of many blueprints (the test-session approach). | No `Failed to spawn` storms, no minified graphic errors, no hediff Scribe errors. |
| X-02 | P2 | With Dubs Bad Hygiene. | No freeze; pipes aren't overwritten (`overlaps a Dubs Bad Hygiene pipe`). |
| X-03 | P2 | With Biotech: blueprints with xenogerms or wastepacks. | Xenogerms have genes; no wastepacks. |
| X-04 | P2 | With a large mod list (the compatibility rolling set, see docs). | Same as X-01, plus the planetary transfer (R8: the analyzer computes costs and wall strength on its thread, and DefRoles initializes there) without exceptions. Also run the transfer while the game is unpaused, then pause and unpause during it. |
| X-05 | P1 | Restored pawns (corpses, sleeping pawns in caskets). | Apparel fits; no missing hediff errors; corpses are saved, and save/reload works. |
| X-06 | P1 | Wall attachments (lamps, shelves on walls). | They are on walls, not floating. |

## G — Debug (P2)

| ID | Prio | Test | Expected |
|---|---|---|---|
| G-01 | P2 | Every RealRuins debug action once. | It works; settings are restored after the samplers (log level, wealth log) — check the config after closing the settings window (R16). |
| G-02 | P2 | DEV: Explore and Reset on an abandoned base and a POI, twice. | Explore generates and enters; Reset restores the scavenging state, faction and goodwill. Then load a **different** save (and, separately, go to the main menu and load again) and press Reset on a site with the same ID: no state from the other game is restored (R9). |
| G-03 | P2 | Staging toggle on: the planetary list and uploads go to staging. Turn it off afterwards. | The URLs in the log change. |

---

## Exit criteria

- All P0 pass, or a fix is merged and the test is re-run (R1 and R2 need P-02 and P-03).
- P1: no unexplained failures; decisions are recorded for C-03, S-05, W-07 and R5 (default caps).
- `driver errors` over the whole run: 0 RealRuins errors that aren't explained.
- Record the results in a table next to this file (`playtest_results.md`): ID, result, log excerpt, screenshot path.
