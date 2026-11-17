# Playtest plan for the release

Baseline `b3124b5` → `development` `15ee008`; fixes after the first run in `8709136` (see RT). The changes are listed in `changes_by_group.md`, the risks in `regressions_list.md` (R#), and settings and saves in `settings_migration.md`.
The tests are run with the `run-realruins` skill (`.claude/skills/run-realruins/driver.sh`). Read its `NOTES.md` before the run and add to it after.

## 0. Conventions

- **ID**: group letter + number. **Prio**: P0 = release blocker, P1 = must pass, P2 = should pass.
- **Log check**: `M=$(driver logmark)` before each action, then `driver logsince $M RealRuins` and `driver errors $M`. Unless a test says otherwise, every test also requires **0 new errors/exceptions** from RealRuins.
- Default test settings unless stated: Dev mode on, RealRuins **log level = all**, `debugLogRuinsWealth` on (it gives `Chunk …` and map summary lines with `limit=`), 1.7, Harmony + RealRuins only. Storyteller Cassandra, Strive to Survive, unless stated.
- After each test group: restore defaults (Network & Cache → reset), unless the next test needs the state.
- **Error popups**: the debug log opens by itself on any error. Record every popup in `playtest_popups.md` (what was done, the error, RealRuins frames or not), even when it looks unrelated, and later repeat the action without the mod (RT-08).
- **Game versions**: 1.6 and 1.7 are the same for the mod's interaction with the game, so there are no 1.6 tests. The only compatibility that is tested is upgrading **the mod** (old config, old saves: group M).
- **Who runs what**: the groups P–G and RT are run with the driver. Tests that need long play, fights, precise mouse work, visual judgment, other mods or system changes are in **H** (hand tests, run by the user), grouped into combined runs.

## 1. Fixtures to prepare (once, before testing)

| ID | Fixture | How |
|---|---|---|
| FX-1 | **Old build** to create old data | Best: the version currently on the Steam Workshop (what players have). If it can't be used: `git worktree add ../rr-baseline b3124b5`, build, and deploy under the same packageId. Make sure only one copy is active. |
| FX-2 | **Old config** with non-default values | With the old build: set every old option to a non-default value (log level warnings, force ×1.5, cost cap 50k, caravan reform = manual, density 2.0, radius 10–30, scav 0.5, deterioration 0.5, item limit 500, hostile chance 0.3, walls/doors only on, proximity off, blacklist `Hive, Sarcophagus`, material blacklist `Gold`, fallback `Steel`, abandoned event **off**, caravan event on, preserve standard ruins on, planetary download limit 300, transfer limit 50). Save. Copy `Config/Mod_*RealRuins*.xml` to the fixtures folder, plus the **export string** from the old Import/Export dialog. |
| FX-3 | **Old saves** made with the old build | S1: a plain colony. S2: an abandoned base (quest) **map open, mid-fight**, with raid triggers left (manual mode). S3: the same in **auto** mode, map open, all triggers fired, enemies dead. S4: a **caravan travelling** to a POI, and a second one travelling to an abandoned base. S5: a game started with **planetary ruins**, with an owned POI map **open** (entered, defenders killed). S6: a save that contains `MapRuinsStore` (any game from an old version). S7: pods in flight to ruins. |
| FX-5 | Blueprint cache | At least 500 blueprints in the cache (the "Network & Cache" count). Generic ruins need ≥10. |

---

## P — Packaging & startup (P0)

| ID | Prio | Test | Expected |
|---|---|---|---|
| P-01 | P0 | Clean start on 1.7 with Harmony + RealRuins. Main menu. | `RealRuins started patching`, no Harmony errors, no `MissingMethod`/`TypeLoad`. Mod icon visible in the mod list. |
| P-03 | P0 | **Server host (R1).** Online mode, fresh cache folder (rename the old one). Start the game and wait. | Blueprints download (the cache count grows), no `Could not load maps list`. Repeat with planetary: `Get count` returns a number. |
| P-04 | P1 | 1.7 game: Defs load once. Dev console → no duplicate def warnings for `RaidTrigger`, `ScatterRealRuins`, `RealRuins_AbandonedBase`. | No `Duplicate def` / `TrippingTrigger` errors. |

## M — Migration (P0)

| ID | Prio | Test | Expected |
|---|---|---|---|
| M-01 | P0 | Put the FX-2 config in place, start the new build, open every settings tab. | Every old value is shown unchanged (see the table in `settings_migration.md` §1). New settings show their defaults: per map 5,000; caravan 25,000; POI 15,000; roofs on; tech level off. No errors. Removed keys (`allowInstantCaravanReform`, `claimableBlocks`) cause no message. |
| M-02 | P1 | Import the FX-2 **export string** into the new build (after a reset). | All old values restored, new ones unchanged, no error. The text fields (blacklists) show the imported text **without reopening** the window. |
| M-03 | P0 | Load **S6** (MapRuinsStore). | Loads with no error about `RealRuins.MapRuinsStore`. Save again, then reload: the node is gone from the XML. |
| M-04 | P0 | Load **S1**, play 1 day, save, reload. | No errors. Regular ruins on new maps work (form a caravan, settle a new tile, or use dev "Generate map"). |
| M-05 | P0 | Load **S4** (caravans travelling). Select each caravan (inspect pane), let it arrive. | R4 fixed: no NRE in the inspector or on arrival. Old caravans may load already stopped ("Waiting") without vanilla's message: accepted, the player sends them again. |
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
| S-05 | P1 | **R6:** turn dev mode off. Is the Debug tab visible? Is the staging toggle visible? | Record it. If staging is reachable without dev mode, report it (release decision). |

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
| W-07 | P2 | Caravan cap at its minimum (R20). | Decided: the minimum is now 1,000 (at 0 the ruin was empty but still had raiders). Retest: RT-04. |
| W-08 | P1 | Abandoned base: cost cap 50k, quest via the debug action, look at the inspect string, DEV: Explore. | The wealth description matches the cap (dev mode shows the number). Map wealth ≈ ≤ cap. Golden walls may be dismantled (`cutDismantlesWalls`). |
| W-09 | P1 | POI cap: new planetary game with POIs as ruins (abandoned %, 100). Enter 3 abandoned POIs. | Each ≤ 15k. |
| W-10 | P1 | Scale with colony wealth on, reference 100k. (a) The starting map. (b) A colony worth about 300k (dev: spawn silver): new map and quest. | (a) Limits ×0.5. (b) ×3 (log or inspect string). |

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

## R — Scattering: regular ruins (P1)

| ID | Prio | Test | Expected |
|---|---|---|---|
| R-01 | P1 | Proximity on: maps at distance 1–3, 8, and 16+ from a settlement. | More and bigger chunks near settlements, fewer far away; 50% of maps at 16+ have none. The log shows `count x…`. |
| R-02 | P1 | Proximity off. | A flat count by density. |
| R-03 | P1 | A quest site map (for example an item stash or a prisoner rescue quest). | Ruins don't overlap the quest's buildings or items (`UsedRects`). |
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
| L-11 | P1 | Failed in the first run, fixed: retest as RT-01. Choose a POI at game setup, back out to the main menu through the setup pages, then start a **new** game on a normal tile. Separately: play a game started at a POI, quit to the main menu, start another new game. | Both start normally, with no leftover POI context. The second new game shows the planetary loader again if it is enabled (R9). |
| L-10 | P2 | Military base sizes: sample POI types. | Fewer "large military" than before; the distribution looks plausible. |

## X — Compatibility & stability (P1, P2 with mods)

| ID | Prio | Test | Expected |
|---|---|---|---|
| X-01 | P1 | Generate 10 maps with a cache of many blueprints (the test-session approach). | No `Failed to spawn` storms, no minified graphic errors, no hediff Scribe errors. |
| X-03 | P2 | With Biotech: blueprints with xenogerms or wastepacks. | Xenogerms have genes; no wastepacks. |
| X-05 | P1 | Restored pawns (corpses, sleeping pawns in caskets). | Apparel fits; no missing hediff errors; corpses are saved, and save/reload works. |

## G — Debug (P2)

| ID | Prio | Test | Expected |
|---|---|---|---|
| G-01 | P2 | Every RealRuins debug action once. | It works; settings are restored after the samplers (log level, wealth log) — check the config after closing the settings window (R16). |
| G-02 | P2 | DEV: Explore and Reset on an abandoned base and a POI, twice. | Explore generates and enters; Reset restores the scavenging state, faction and goodwill. Then load a **different** save (and, separately, go to the main menu and load again) and press Reset on a site with the same ID: no state from the other game is restored (R9). |
| G-03 | P2 | Staging toggle on: the planetary list and uploads go to staging. Turn it off afterwards. | The URLs in the log change. |

---

## RT — Retests after fixes (`8709136`, `4d3ad0b`)

| ID | Prio | Fix | Test | Expected |
|---|---|---|---|---|
| RT-01 | P0 | Stale starting POI (L-11) | (a) Choose a faction POI with "attack", go Back through every page to the main menu, then Dev quicktest. (b) Same, then "New colony" and start on a normal tile. (c) Re-run L-04, L-05, L-06, L-07. | (a) Normal map, log `Discarding starting POI of another game or tile`, no `null relation` errors. (b) Normal start. (c) Still pass: a POI start still generates the POI map. |
| RT-02 | P0 | Duplicate load ids in saves | Load S2, S3 and S5; save each, reload. New build: generate several maps with restored pawns (corpses, caskets), save, reload. Open a restored pawn's Health → Log tab. | No `LogEntry_… in loaded object directory`, `Hediff_… Id already used` or `had a beard` errors, neither on the first load nor after the re-save. The baked combat log lines are still shown. |
| RT-03 | P1 | Upload 3-hour slot | Offline mode. Fresh colony with a home area < 300 cells: save. Paint the home area to ≥ 300 cells, save again at once. | The first save makes nothing; the second writes `local-….bp` (not blocked for 3 hours). |
| RT-04 | P1 | Caravan cap minimum 1,000 | Config with `caravanRuinsCostCap` 0; open the Advanced tab; fire the caravan ruins incident. | Shown and used as 1,000: `Caravan ruins worth …` ≤ 1,000 and the ruin isn't empty. (Dragging the slider: H-5.) |
| RT-05 | P2 | Wealth cap slider rewrite | `ruinsCostCap` 50000 in the config: open Advanced, close the settings. | The config still has `50000`. (Dragging the slider: H-5.) |
| RT-06 | P1 | Planetary loader on the site page | New game, planetary on start: the loader opens; transfer; close it. Next, then Back to the site page. Click "Planetary ruins". Then the same with "allow on start" off. Then change the map size in Advanced. | Back on the page, the loader doesn't open by itself. "Planetary ruins" sits between "Factions" and "Next" and opens the loader, which offers "Remove all spawned locations" for the existing sites. Option off: no button, vanilla's 4 buttons. Map size change: the loader opens with cleanup, as before. No `layout not recognized` warning in the log. |
| RT-07 | P1 | Truncated snapshots (`4d3ad0b`) | Uploads on (staging) or offline mode, a home area ≥ 300 cells **with a colonist and some buildings** in it; save. Decompress the kept/local `.bp` (`gzip -dc`). | Complete XML ending with `</snapshot>`, pawns included; it parses. |
| RT-08 | P1 | Error popups | Every popup in `playtest_popups.md` marked "pending": repeat the action without RealRuins. | Same error without the mod (game issue) or a RealRuins cause found. |

## H — Hand tests (run by the user)

These are hard to drive with screenshots and clicks (long play, fights, sliders, visual checks, other mods, system changes) but quick by hand. One run covers several IDs. The defaults from §0 apply (dev mode on, log level all); the setup says what differs. Report per ID: pass / fail / what you saw, plus any red errors.

| Run | Covers | Setup | Steps | Expected |
|---|---|---|---|---|
| H-1 | M-06, M-07, E-04 | Old saves `PT-old-S2-manual` (manual reform) and `PT-old-S3-auto` (auto) in the Saves folder. | S2: once, walk a pawn off the map edge while raids still come. Fight off the raids until they stop, wait for the "you can leave" letter, then reform. S3: play a day or two, note any lifecycle letter, reform. | S2: the reform button is disabled with the manual text while raids can come; walking off the edge works; after the letter, reforming works. S3: no errors; any letter is fine (`settings_migration.md` §4); reforming works. |
| H-2 | E-05, E-06, D-08 | A colony with a few armed colonists. Debug action "Generate pristine ruins quest", then DEV: Explore. Reform mode **instant** first, then **auto** (Advanced tab). | Instant: kill the raiders, then reform. Auto: during a raid press Reform, choose a route, pick some colonists and loot, keep others drafted, let the caravan leave. Over a few raids, loot the base heavily. | Instant: blocked with "There are still some enemies" while hostiles are there, works after; the site stays unowned. Auto: formation starts under fire, caravaners haul while drafted pawns fight, the caravan leaves by the edge. Raids get rarer as the loot goes down (`Recalculating raid chance` in the log). |
| H-3 | D-07 | Any colony with ruins near player items (gravship cargo, or items dropped next to a ruin). | Dev: a raid by a **friendly** faction on that map; then a hostile one for comparison. Watch what the raiders pick up. | Friendly raiders don't take player-owned or substructure items. Hostile raiders behave like vanilla. |
| H-4 | L-08, L-09, X-06, R-04, R-06 (on) | A new planetary game (loader on start). For R-04: "Preserve standard ruins" on, then off; with Odyssey also a cave/underground map. | Enter a faction POI by caravan, by transport pods and by shuttle. Clear one POI and leave with little loot; clear another and leave with most of it. On the maps, look at wall lamps and wall shelves. Look for vanilla ruins on new maps with the preserve option on and off. Spawn roofs on (R-06): walk pawns through roofed ruin rooms, build or deconstruct a wall next to one. | Every entry way offers its options (edge / center drop, shuttle landing); relations drop as for a vanilla settlement attack. Little loot: the owner comes back, with a cooldown. Heavy loot: the POI is abandoned or destroyed. Wall attachments sit on walls. Vanilla surface ruins appear with the option on and not with it off; underground vanilla ruins stay. No roof collapses in ruins. |
| H-5 | S-04, RT-04, RT-05, S-06 | Settings window. | Drag the caravan and POI cap sliders to both ends; drag the absolute wealth cap slider. Then switch the game language to Russian, Japanese, French, German and Chinese in turn and open every tab and the planetary loader. | The caravan cap stops at 1,000 at the low end; both caps show "Unlimited" at the top. The wealth cap follows the drag. No raw `RealRuins.…` keys in any language; untranslated strings show in English. |
| H-6 | W-11, W-12 | Item cost limit 200 (Scattering tab). | Generate new maps until a few ruins have bookcases, shelves or other containers; inspect them. | Containers are partly emptied rather than gone. Expensive contents are removed; the container and cheap contents stay. |
| H-7 | X-02, X-04, T-07 | Your usual large mod list, with Dubs Bad Hygiene and a tech mod with its own materials if you have them. | New game; generate several maps (settle, caravan). A planetary game with the transfer, pausing and unpausing during it. Turn on the tech cap (Medieval, limited) for a couple of maps. | No freeze; DBH pipes aren't overwritten; no `Failed to spawn` storms; the transfer finishes without exceptions; modded things get a sensible era (no above-cap modded items). |
| H-8 | U-02 | Uploads on, offline mode off. Block `api.woolstrand.art` in the hosts file (needs admin). | Play a home map with a home area ≥ 300 cells, save, quit. Unblock, start the game again. | After the save the file stays in `RealRuinsPendingUploads`; on the next start `Retrying pending upload`, success, and the folder is emptied. |

## Exit criteria

- All P0 pass, or a fix is merged and the test is re-run (R1 needs P-03). All RT pass.
- P1: no unexplained failures; decisions are recorded for C-03, S-05 and R5 (default caps).
- `driver errors` over the whole run: 0 RealRuins errors that aren't explained.
- Record the results in a table next to this file (`playtest_results.md`): ID, result, log excerpt, screenshot path. H results go in the same table.
