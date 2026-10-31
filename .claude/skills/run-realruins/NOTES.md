# RealRuins in-game testing notes

Accumulated knowledge from interactive test sessions. Add to it whenever something took
searching to figure out. Keep entries short; no screen coordinates (they change per launch).

## Getting to things

- **New map with ruins, fast:** main menu → **Dev quicktest**. Uses the normal map generator
  (RealRuins scatter runs), skips scenario/storyteller screens.
- **Dev toolbar** (top center, dev mode only): 4th icon = **Debug actions**. It opens on the
  *Settings* tab - switch to **Actions/tools**. Typing into its search box via `type` didn't register
  reliably; clicking entries works.
- **RealRuins debug actions:** only "Focus on tile" (`Source/Classes/Utility/DebugTools/DebugActionsRealRuins.cs`).
  Quests/events are fired as incidents instead.
- **Ruins quest ("Pristine ruins found nearby"):** incident `Quest_PristineRuins`
  (`Defs/Storyteller/Incidents.xml`, worker `IncidentWorker_RuinsFound`). It's targetTag `World`:
  go to the **World** tab first, then Debug actions → **Do incident (World)...**. From a map you only
  get "Do incident (Map)", which can't fire it.
- **Force one blueprint everywhere (debug):** set `RealRuins.SingleFileName` (`Source/Classes/RealRuins.cs`)
  to a full path of a cached `.bp`; `SnapshotStoreManager` then returns it for every random pick (scatter
  *and* quests). Mark the edit `// TEMP-TEST` and revert + rebuild after. Cached blueprints are gzipped XML
  (`gzip -dc < X.bp`) - handy to see what a ruin contains.
- **Cut pawn/corpse noise:** temporarily `return null` for `pawn`/`corpse` at the top of
  `BlueprintTransferUtility.MakeThingFromItemTile` (also `// TEMP-TEST`).
- **Planetary POI start:** New colony → scenario → storyteller (just press Next, the defaults are fine) → Generate world → the "Load blueprints
  for planet" window opens on site selection → **Load automatically** (downloads by seed, places POIs) → Close → click a
  POI icon (inspector shows Faction) → Next → choose mode (abandoned / own / **attack**) → confirm → Start.
- **Attack a faction base:** spawn a caravan next to it, select caravan, right-click base → **Attack X (Dev: instantly)**.
  Some planetary POIs look like vanilla settlements (e.g. "Temporary camp") - check the inspector before attacking.
- **Small ruins event:** select a caravan on the world map → Debug actions → **Do incident (<caravan>)...** →
  `SmallRuinsFound` → **Investigate** in the dialog. Generates a 120x120 map and moves the caravan onto it.
- **Caravan on demand:** world view → Debug actions → **T: Spawn Random Caravan** → click a tile.
- **Reach a site instantly:** select the caravan, right-click the site → **Enter ruins (Dev: instantly)**.
  The camera stays on the world; open the "Caravan arrived at ruins" letter → **Jump to location**.
- **Letters:** click the small icon to the right of the label, not the text. Then
  **Jump to location** in the letter.

## Traps

- Every entry in the Do incident list is shown `[NO]` (even vanilla). Clicking still fires it.
- `esc` closes the world view as well as windows - you land back on the colony map.
- Hovering a DLC icon on the main menu covers the menu buttons with a big panel; `move` away first.
- The debug log window auto-opens on any logged exception; close it with its X.
- Letter icons shift as new letters arrive and sometimes open a beat late; re-screenshot.
- Window size/position differed between launches - always screenshot before clicking.
- Launch `RimWorld.app`. The csproj `StartProgram` points at `RimWorldMac.app/...`, which has no binary.
- `Player.log` is recreated each launch (old → `Player-prev.log`).
- RimWorld 1.7 is a new engine (Waylith/.NET, not Unity): Windows exe is `RimWorld.exe` (1.6 was
  `RimWorldWin64.exe`), log moved to `Logs/Player.log`, and **every** log line is followed by a stack
  trace - logs grow by tens of thousands of lines. Always filter (`logsince`, `errors`); never dump.
- First 1.7 launch can rewrite `ModsConfig.xml`. If a mod throws while loading play data the game logs
  "Resetting mods config and trying again" and drops to Core - then RealRuins is gone from the list.
- **Harmony must be active.** Without `brrainz.harmony` the only `0Harmony` is the ancient 1.2 copy in the
  mod's root `Assemblies/` (loaded for every version via `LoadFolders.xml` `""`), and loading fails with
  `Could not load type 'HarmonyLib.HarmonyPatch' from assembly '0Harmony, Version=1.2.0.1'`.
- The game may **restart itself** shortly after `launch` returns (new pid, fresh log, mod list changed
  back to the full user list). Check `status`/pid before clicking; a click during the reload is lost.
- Two copies of RealRuins can be installed (`Mods/RealRuins` and the Workshop one); the local `Mods`
  copy is the one the build deploys to. The log's "Adding Woolstrand.RealRuins(<path>)" lines show both.
- Windows: the driver must detach the game's stdio on launch, or anything piping driver output hangs
  until the game exits (fixed in `platform/win.sh`; keep it that way).
- **Debug log "Auto-open" resets to ON every launch.** The first logged error after launch (even a harmless one) pops the
  log over the screen and swallows the next clicks. Toggle "Auto-open is ON" off once per launch.
- **The site of a fresh ruins event is selected** right after firing it; its own "Jump to location" gizmo (bottom bar)
  centres it reliably. The newest letter is the bottom one, but an unrelated letter arriving at the same time shifts the
  stack and the letter's jump can take you home instead.
- **Clicking World while already on the world map switches to the home map.** After ticking a ruin, check whether the
  caravan was lost (then you're already on the world, the site is gone) before clicking World.
- **Pristine ruins blueprints get evicted from the cache** soon after use (only the `.bp.xml` meta stays). Copy a `.bp` you
  want to re-test in single-file mode out of the cache right away; `SingleFileName` accepts any path.
- **Single-file mode stores every download into `RealRuins/jeluder.bp`** (`SnapshotStoreManager`, debug leftover): parallel
  downloads then log `Failed storing blueprint ... jeluder.bp ... used by another process`. Harmless; the file can be
  deleted afterwards.
- Subscribing to workshop mods works from outside the game: `cmd //c start "" steam://url/CommunityFilePage/<id>` opens the
  page in the Steam client; the driver's input helper takes any pid, so `input.exe ss|click <steamwebhelper pid> ...`
  screenshots and clicks the Steam window. Steam downloads while the game is closed; activate via `Config/ModsConfig.xml`.
- Planetary loader: with >500 blueprints for the seed (seed `shotgun` has 579) a "whole lot of blueprints" dialog
  pops up first - pick "Get all" or "Get just 500". The seed list request sometimes fails ("Could not load maps
  list") - just press Load automatically again. Two `shotgun` blueprints always fail to download; their
  "not available locally" skips are expected.
- Planetary loader transfer: ends with `[POI]: Transfer finished, N sites created` and a green Close button. It keeps
  running if the window is closed. The old vanilla NRE in `ExpandableLandmarksUtility` (POIs created off the
  main thread) is fixed (H9); if it shows up again, it's a regression.
- POI map generation: `GenStep_ScatterPOIRuins.Finish` clears `PlanetaryRuinsInitData` (incl. `startingPOI`) **before**
  later gensteps such as the regular scatter run - later steps can't use it to recognize the starting POI map.
- **Reaching the site of a fired ruins event:** open its letter and use **Jump to location** before closing it - closing
  dismisses the letter and the event is not in the Quests tab, so the site is then hard to find. Small world icons near
  the colony are easy to mistake for it.
- `esc` on the world map closes the world view; a debug click made right after can then land on the colony map's menu
  (different actions). With a caravan selected, "Do incident" targets the caravan, not the World - deselect first.
- A "T: ..." debug tool (e.g. Spawn Random Caravan) stays active after the first click; the next world click spawns again.
- **Speed 3 is unavailable while hostiles are on the map** (the click is ignored); use speed 2 or the dev speed.
- **Log stops at ~420k lines**: `Reached max messages limit. Stopping logging to avoid spam.` - a long fight can trigger it.
  Mark/check the log before long runs; after it, `logsince` returns nothing and only a relaunch helps.
- Ctrl+V for paste: `key 86 ctrl` (Windows VK code; `key v ctrl` fails).
- Once the game exited cleanly ("Log closed", no crash report) ~1 min after a quicktest click that
  left no log trace; cause unknown. Check `logsince` before assuming a crash.

## Log signals (`[RealRuins][<part>]`)

Spawn failures found while testing go into `docs/PROBLEMATIC_ITEMS.md` (with log excerpt, code location,
blueprint id) - that file also states the spawn philosophy: spawn what can be spawned, discard the rest quietly.

- `[StartGame]: Starting tile` / `[[MapGen]]: Game start context is empty` - printed on every map
  generation, not just game start.
- `[Scatter]: Spawning N ruin chunks` → per chunk `[Store]: Acceptable blueprint ...` +
  `[BlueprintTransfer]: Transferred blueprint of size AxB ...`. Count of Transferred ≈ N means scatter worked.
- `[Store]: Non-qualified XML at path ..., removing` - the blueprint file is **deleted** from the local
  cache (`deleteLowQuality` setting); a few per map is normal.
- `[Event]: Event preparations completed, blueprint is ready and stored, letter sent.` - quest created.
- `[Event]: Major event entering.` … `[ForceGen]: Spawned trigger ...` … `[Generic]: Generated map.
  Cap cost X, actual cost Y` → `[Event]: Map generated` - ruins site map built, defenders placed.
- `[BlueprintTransfer]: Failed to spawn item <Def> because of ...` - caught per-item failure; the map
  still generates. Seen: `CleaningStation` NRE in `MakeThingFromItemTile` (2026-10-29, unfixed);
  `PortableReactor` NRE (2026-10-29, Windows/1.7; fixed 2026-10-30 - missing inner defs now skipped).
- `[BlueprintPawnDecoder]: Pawn decoding failed` / `Failed to spawn item corpse` / `Could not load reference
  to Verse.BodyDef named X` / `Tried to restore null body part` - modded-race pawns (unknown kind or body).
  Fixed 2026-10-30 (unknown single-name kinds discarded, hediff parts remapped to the pawn's body or
  skipped); if they reappear, it's a regression. The decoder's skip reasons are `Debug.Extra` - not in
  the log by default.
- `[Scatter]: dist D, dens X (count xM), scale xS ...` + `Spawning N ruin chunks` - M is the proximity count multiplier
  (≈1.7 at ≤4 tiles from a settlement, ≈0.5 at 16+); no cap, N follows the density setting. Distance 1 = a settlement or
  site on/next to the tile.
- POI map generation logs `Spawning POI: Preselected file name`, `Selected N for POIs`, `Generating N starting parties` -
  there is **no** `Generated map` line for POIs (that one is the abandoned base's `RuinedBaseComp`), don't `waitlog` for it.
- `[Scatter]: dist 16+, dens 0 ... Spawning 0 ruin chunks` - **not a bug**: beyond 16 tiles from the
  nearest settlement scatter is skipped with 50% chance (`GenStep_ScatterRealRuins`). Retry another map.
- `[BlueprintTransfer]: [2] Failed to load, recovering...` → `[2.1] Recovered!` - loader's normal
  fallback for slightly broken blueprint XML. `errors` counts these; ignore if followed by Recovered.
- Entering a ruins site: `[Event]: Major event entering.` → `Will generate map now` → `[ForceGen]` lines
  → `[Generic]: Generated map. Cap cost ...` → `[Event]: Map generated`. Wait for the last one.

## Session log

- 2026-10-31 (Windows, 1.7): mod compatibility run. Subscribed 24 popular 1.7 content mods (Vanilla Expanded furniture/
  weapons/armour/cooking/brewing/books/plants/fishing/settlers/memes etc.) and activated them plus the already installed Dubs
  Bad Hygiene and Rimatomics (~57 active mods). 30 random Pristine Ruins (quest → caravan → instant enter → 45 s at speed 1 →
  T: Destroy Site) plus 5 single-file re-runs. Load and scatter clean. Fixed and verified: missing modded `sourceHediffDef`
  on injuries, injuries on unhittable parts (Waist), minified things whose graphic throws (VFE Security bear trap),
  stack count on wall/door replacements; spawn failures now log the full stack. Open: PROBLEMATIC_ITEMS #4, #6-#10.
- 2026-10-31 (Windows, 1.7): settings import/export (L11, M14, numeric buffers), quicktest scatter (25/25), pristine ruins in
  manual reform mode: reform gizmo disabled with the new reason, thought "Very high wealth ruins" for a 622k site, raid triggers
  fired within 35 ticks of the logged expectation (L8). Found and fixed: map generation aborted by wall lamps without a wall
  (`PowerConnectionMaker.TryConnectToAnyPowerNet` NRE in `FinalizeInit`); now "Removed N of M wall attachments without a
  supporting wall" in the log. Planetary loader with seed `rr test/1#x` got a normal "no maps" answer (L15). Waves/unlock not
  observed (team kidnapped at speed 1).

- 2026-10-29: quicktest scatter (25/25 chunks), Quest_PristineRuins, random caravan, instant enter -
  all worked. Only error: CleaningStation NRE above.
- 2026-10-29 (Windows 11, RimWorld 1.7.4990, first Windows run): build via VS 2022 Build Tools,
  launch, quicktest (scatter ran, coin flip gave 0 chunks), Quest_PristineRuins (letter sent, site on
  world map), random caravan, instant enter → ruins map generated. Errors: modded-corpse NREs and
  PortableReactor NRE above. Found and fixed driver `waitlog` never returning on big logs (grep -q +
  pipefail SIGPIPE) - affected macOS too.
- 2026-10-30 (Windows, 1.7): verified the missing-inner-def fix with a forced blueprint (EEDE39B6) and
  pawns disabled: quicktest scatter 18 chunks, quest → caravan → enter, 0 errors. `waitlog` fix confirmed.
  No game self-restart this time.
- 2026-10-30 (Windows, 1.7): verified the modded-race pawn fix with forced blueprint EEDE39B6 via
  quest → random caravan → instant enter: 177 corpses instantiated, 0 errors (was ~1.6k). Scatter coin flip
  gave 0 chunks again. Clicking the dev toolbar right after closing a window can reopen Debug actions on the
  *Settings* tab, so a click meant for an action toggles a setting - screenshot before each click there.
- 2026-10-30 (Windows, 1.7): verified the site genstep refactor (`GenStep_RealRuinsSite`). forceMultiplier set to 0 in
  `Config/Mod_RealRuins_RealRuins_Mod.xml` (backed up and restored): Pristine ruins quest → ruins spawned, no defenders.
  SmallRuinsFound on a caravan next to a settlement: medium blueprint met min cost 25k, regular chunks dist 1 (35 chunks by the curve at the time; since
  then the coverage cap was replaced by a normalized count multiplier, see docs/04-scattering.md).
  POI path not tested in-game. Quicktest scatter coin flip gave 0 chunks twice; the first quicktest click was lost because
  the mod was still loading snapshots - wait for the `[Store]: Loading snapshot` lines to stop.
  Found `VFEM_CobblestoneWall` stuff error (PROBLEMATIC_ITEMS #2).
- 2026-10-30 (Windows, 1.7): verified site-step virtual methods, on-tile count limit, planetary POI. New game on a POI
  (attack): POI blueprint loaded once, citizen defenders + starting party, no new errors (M11 pawn-group error is old).
  Attacking a pirate base: dist 1, count x1. Small ruins: raid party present (red pawns). The settlement "DEV: Generate"
  gizmo gave dist 16 on a base tile - use a caravan attack instead to test on-tile maps. Found M11, M12 (FIXES_QUEUE).
- 2026-10-30 (Windows, 1.7): verified M8 (DoNext without the static flag: dialog → attack → second pass falls through) and
  M12 (no regular chunks on a starting POI map; first try failed because `startingPOI` is already cleared, now checks the
  map generator for `GenStep_ScatterPOIRuins`). M11 skip path not hit - both starts had ~4.5k points. M10 not checked
  visually (vanilla ruins don't log). Found PROBLEMATIC_ITEMS #3-#5 (bookcase contents, MinifiedThing tick, Waist injury).
- 2026-10-30 (Windows, 1.7): verified H1 + H9 on the planetary loader, seed `shotgun`: 579 fresh downloads, window closed
  mid-transfer (173/579) and sites kept appearing → 393 sites, 0 exceptions; only the 2 failed downloads were missing
  locally (completion now waits for disk writes). Second world, same seed: Close turned green on completion.
