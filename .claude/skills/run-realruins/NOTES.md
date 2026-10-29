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
- **Small ruins event:** incident `SmallRuinsFound` (targetTags Caravan / Map_TempIncident) - not tried yet.
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
- Once the game exited cleanly ("Log closed", no crash report) ~1 min after a quicktest click that
  left no log trace; cause unknown. Check `logsince` before assuming a crash.

## Log signals (`[RealRuins][<part>]`)

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
  `PortableReactor` NRE (2026-10-29, Windows/1.7, unfixed).
- `[BlueprintPawnDecoder]: Pawn decoding failed: NullReferenceException` + `[BlueprintTransfer]: Failed to
  spawn item corpse` - corpses from blueprints made with modded races. Came with ~1.4k
  `Could not load reference to Verse.BodyDef named Android4Tier` (Android mod not loaded) and
  `Tried to restore null body part`. ~100 per ruins map; map still generates (unfixed).
- `[Scatter]: dist 16+, dens 0 ... Spawning 0 ruin chunks` - **not a bug**: beyond 16 tiles from the
  nearest settlement scatter is skipped with 50% chance (`GenStep_ScatterRealRuins`). Retry another map.
- `[BlueprintTransfer]: [2] Failed to load, recovering...` → `[2.1] Recovered!` - loader's normal
  fallback for slightly broken blueprint XML. `errors` counts these; ignore if followed by Recovered.
- Entering a ruins site: `[Event]: Major event entering.` → `Will generate map now` → `[ForceGen]` lines
  → `[Generic]: Generated map. Cap cost ...` → `[Event]: Map generated`. Wait for the last one.

## Session log

- 2026-10-29: quicktest scatter (25/25 chunks), Quest_PristineRuins, random caravan, instant enter -
  all worked. Only error: CleaningStation NRE above.
- 2026-10-29 (Windows 11, RimWorld 1.7.4990, first Windows run): build via VS 2022 Build Tools,
  launch, quicktest (scatter ran, coin flip gave 0 chunks), Quest_PristineRuins (letter sent, site on
  world map), random caravan, instant enter → ruins map generated. Errors: modded-corpse NREs and
  PortableReactor NRE above. Found and fixed driver `waitlog` never returning on big logs (grep -q +
  pipefail SIGPIPE) - affected macOS too.
