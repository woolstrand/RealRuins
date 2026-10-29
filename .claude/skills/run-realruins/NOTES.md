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
  still generates. Seen: `CleaningStation` NRE in `MakeThingFromItemTile` (2026-10-29, unfixed).

## Session log

- 2026-10-29: quicktest scatter (25/25 chunks), Quest_PristineRuins, random caravan, instant enter -
  all worked. Only error: CleaningStation NRE above.
