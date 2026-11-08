# Architecture regressions and release risks

Scope: everything on `development` since the last released build, **`b3124b5` (2026-05-17)**, up to `15ee008` (2026-11-08), 102 commits.
Pre-existing problems are listed only where the new code touches them, and they are marked *(pre-existing)*.

Severity:
- **S1 Blocker**: can break the mod for everyone, or crashes. Verify or fix before release.
- **S2 High**: wrong behavior that players will hit, or a data/privacy risk.
- **S3 Medium**: broken separation of concerns or leaking state that will cause bugs later. Some have a visible symptom now.
- **S4 Low**: cut corners, inconsistencies, cleanup.

Items already tracked in `docs/FIXES_QUEUE.md` are only referenced, not repeated.


## Status (2026-11-09)

| Item | Decision | Commit |
|---|---|---|
| R1 | Fine: `api.woolstrand.art` is live on the backend | — |
| R2 | Accepted for 1.6 and 1.7 (same API, only the graphics core differs); documented in the csproj | `043d772` |
| R3 | Merge leftover, not intentional: fixed (checks the uploaded map, and the game-over fallback skips POI maps) | `db18ce6` |
| R4 | Fixed: `StillValid` rejects a missing target, null-safe labels | `9acd31b` |
| R5 | Accepted; a popup will be added later | — |
| R6 | Accepted | — |
| R7 | Fixed: roofs and wealth options come through `PipelineSettings` | `0a0ef49` |
| R8 | Fixed: abstract stat calls checked and documented as safe; DefRoles lazy init made thread-safe | `9d0a13e` |
| R9 | Fixed: `SessionState` resets statics on load, new game and main menu; owns the starting map flag | `0f998c3` |
| R10 | Accepted (1.6 and 1.7 content is identical) | — |
| R11 | Intended behavior; documented at `ClearPlanetaryCacheFolder` | `043d772` |
| R13 | Fixed: `MemberwiseClone` | `290ae20` |
| R14 | Fixed: `CaravanReformMode` + `CaravanReformPolicy` | `a50b298` |
| R12, R15–R23 | Open | — |

---

## S1 — Blockers (verify before release)

### R1. Production API host changed
`Utility/APIService.cs`. Commit `8dbb43a` ("added staging", described as "forgot to commit this a long time ago").
`APIRoot` changed from `https://woolstrand.art/` to `https://api.woolstrand.art/`. The staging switch also changed the production URL.
- **Risk:** if `api.woolstrand.art` isn't live for production traffic (DNS, TLS cert, routes `maps/random`, `maps/seed/`, upload), every player loses downloads, uploads and planetary ruins after updating. Players who don't update keep using the old host.
- **Action:** check that both hosts serve the same API before publishing. Keep the old host working for some time.

### R2. One DLL built for 1.7 is copied into 1.6
`Source/RealRuins.csproj` (OutputPath `..\1.7\Assemblies`, plus a `Copy` target into `1.6\Assemblies`).
The assembly is compiled against 1.7's `Assembly-CSharp`. If it uses any member that 1.6 doesn't have (new overloads, `Xenogerm`/`StealAIUtility` signatures, `GetShuttleFloatMenuOptions`, `FoundationAt`, `PlanetTile` APIs), 1.6 fails with `MissingMethodException` or `TypeLoadException`. That can happen at Harmony `PatchAll` (the whole mod is dead) or later at runtime.
- **Action:** run the game once on 1.6 (smoke test P-01 in the playtest plan). If that isn't possible, build 1.6 separately against 1.6 references.

---

## S2 — High

### R3. The game-over snapshot checks the wrong map
`Utility/SnapshotManager.cs:226`. `UploadMapSnapshot(Map map, …)` checks `Find.CurrentMap.Parent is RealRuinsPOIWorldObject` instead of `map.Parent`.
- `SaveGameOverSnapshot()` passes a fallback home map when the player isn't looking at a home map. If the world view is open at game over (`Find.CurrentMap == null`), this throws an NRE inside a Harmony postfix of `GameEnder.GameEndTick`, and the final snapshot is lost.
- If the fallback picks a cleared POI map (player faction, so `IsPlayerHome`), the POI filter checks the current map instead. Someone else's blueprint is uploaded again as if it were the player's colony (`c26e44e` intended to prevent exactly this).
- **Fix:** use `map.Parent`.

### R4. Old saves with a caravan travelling to an abandoned base or POI
`Incidents/CaravanArrivalAction_VisitRuinsBase.cs`. At baseline, `CaravanArrivalAction_VisitAbandonedBase` and `CaravanArrivalAction_VisitRealRuinsPOI` had no parameterless constructor and didn't save `mapParent`. Now they load fine, but `mapParent` is null, so:
- `ReportString` of the POI action (`mapParent.Label`) throws every time the caravan inspector is drawn (error spam).
- `Arrived()` throws an NRE on `mapParent.HasMap` when the caravan arrives.
- **Fix:** override `StillValid(Caravan, PlanetTile)` in the base class to return false when `mapParent == null || !mapParent.Spawned`. Vanilla then drops the arrival action. Also null-guard `ReportString`.

### R5. New wealth caps are on by default for existing players
`RealRuins_ModSettings`: `ruinsWealthPerMap = 5000`, `caravanRuinsCostCap = 25000`, `poiRuinsCostCap = 15000`. These keys are missing from old config files, so updating players get the defaults. Regular ruins were unlimited before, caravan ruins were uncapped (with a 25k **minimum**), and ruined POIs only had the Gaussian roll.
- **Effect:** right after the update, ruins get much poorer with no visible reason. This is a product decision, not a bug, but it needs one of: a changelog entry, a one-time letter, or treating a missing key as "unlimited" for configs that existed before. Details are in `settings_migration.md`.

### R6. The debug tab is visible to every player, and the staging switch is saved
`Settings/RealRuinsSettingsWindow.cs` adds `DebugSettingsPage` with no `Prefs.DevMode` check. `useStagingEnvironment` is saved to the config file.
- A player who tries the toggle keeps sending uploads to staging and downloading staging blueprints in every later session, with no indication outside the debug tab. Their uploads are lost from production.
- **Fix:** show the tab only in dev mode, or don't save `useStagingEnvironment` (reset it on startup). At least show a warning on the main tab while it's on.

---

## S3 — Medium

### R7. Pipeline internals still read mod settings directly, bypassing ScatterOptionsFactory
The refactor (`ScatterOptionsFactory`, `PipelineSettings`, `RuinsWealthLimits`) says "the only place which decides… nothing else reads the cap settings". The new features broke that rule right away:
- `Scattering/Internal/BlueprintTransferUtility.cs:727` reads `RealRuins_ModSettings.spawnRoofs`.
- `BlueprintTransferUtility.cs:873` (`WealthLimitCost`) reads `ruinsWealthCountDamage`.
- `Scattering/Internal/ScavengingProcessor.cs:184` reads `ruinsWealthIgnoreStructure`.
- `Scattering/Internal/Tiles.cs:28`: the **data model** (`Tile.CountsForWealthLimit`) depends on a user setting.

Effects: a scatterer can't override these (for example, intact POIs always follow `spawnRoofs`, which may not be wanted for a taken-over base), debug samplers can't vary them, and the values can change halfway through a generation if the settings window is open. **Fix:** move them into `PipelineSettings` (or `ScatterOptions` if they should be per scatterer) and pass them through `ScatterContext`.

### R8. Engine calls on the background thread of the planetary loader
`RealRuinsPOIFactory.PreparePOI` runs on a raw thread. It now calls `ThingDef.AbstractMarketValue` (`Tiles.cs`, `CostOf`, added in `7ec9dd1`) and `GetStatValueAbstract(MaxHitPoints)` (`DefRoles.WallStrength`) through the analyzer. Stat workers and stat parts aren't thread-safe by contract, and some read `Find.*` state.
`DefRoles.ResolveCategories` uses a lazy init with a non-volatile flag (harmless, but the same pattern).
The move to "prepare off-thread, commit on main" (`0a9faae`) is a good fix. The remaining off-thread engine calls are the weak spot, and with large mod lists they can produce rare corrupted costs or exceptions. **Fix (cheap):** warm the caches on the main thread before the worker starts (cost and wall strength for every def found in the blueprints), or compute costs during commit.

### R9. Static state with an unclear lifetime is patched at the symptoms
- `PlanetaryRuinsInitData.shared` now has stale-context guards in two places (`MapGenerator_GenerateMap_Patch`, `Page_SelectStartingSite_DoNext_Patch`, `2729fb2`, `4da4873`) instead of one clear point that ends its lifetime (cleared on game init, load, and return to the main menu).
- `DevSiteTools.savedStates` is a static `Dictionary<int, object>` keyed by world object ID. It survives loading a different save, where IDs repeat, so "DEV: Reset" can restore another game's state. It's dev only, but it holds `Faction` references, which leaks a whole `Game`.
- `GameStartUtility.GeneratingStartingMap` depends on `Game.InitData` being cleared at the right moment, which is an undocumented engine detail.
- **Fix:** one `GameComponent` (or a `Game.FinalizeInit` and `LoadGame` hook) that resets all mod statics.

### R10. 1.6 and 1.7 content is fully duplicated
`1.6/Defs`, `1.6/Patches`, `1.7/Defs`, `1.7/Patches` are byte-identical copies, and the DLL is copied as well. Every def fix now has to be made twice (or three times, with root `Defs/` for ≤1.5), and they will drift. **Fix:** in `LoadFolders.xml` let `v1.7` load the `1.6` folder (or create a shared `1.6+` folder) and keep `1.7/` only for differences.

### R11. Clearing the planetary cache can delete the folder of the running game
`Settings/NetworkCacheSettingsPage.cs` and `SnapshotStoreManager.ClearPlanetaryCacheFolder`. The confirmation dialog doesn't check whether the folder belongs to the game that's currently loaded. After clearing, every POI on the world map points to a missing blueprint. `GenStep_ScatterPOIRuins.MakePlan` returns null and the player enters an empty map, with only a warning in the log. **Fix:** disable the button for `CurrentGamePath()`, or warn in the dialog.

### R12. Cache scans and deletion race (extends FIXES_QUEUE M15)
M15 is still open. The new code adds more scanners: `GetPlanetaryCacheFolders()` runs every time the cache version changes while the settings page is open, and it wraps the **whole** loop in a single try/catch. One file deleted by the download thread or by trimming aborts the scan, and the table silently shows only part of the list. Fix together with M15: catch per file.

### R13. `ScatterOptions` still mixes user options with per-scatterer internals, and `Copy()` is maintained by hand
The H4 bug (`costCap` and others missing from `Copy`) was exactly this. This release adds `claimEverything`, `avoidUsedRects`, `cutDismantlesWalls`, `maxTechLevel`, `techLimitStrict`. They are all copied correctly now, but the pattern is still fragile. **Fix:** `MemberwiseClone()` (every field is a value type, string or immutable def reference), or split the class into `UserScatterOptions` (saved) and `ScatterOptions` (runtime).

### R14. Caravan reform mode logic is spread over four places
`caravanReformType` (magic ints 0, 1, 2) is now read in `FormCaravanFromRuinsComp.GetGizmos`, `FormCaravanComp_CanFormOrReform_Patch`, `DialogFormCaravan_Patch` and `RuinedBaseComp.mapExitLocked`. The comments explain the interactions well, but a change to one mode now needs four edits. **Fix:** an enum, plus one `CaravanReformPolicy` helper that answers "can reform now / show the button disabled / use regular forming".

---

## S4 — Low

### R15. Debug instrumentation in hot paths
`CostTiming` (a static `Interlocked` counter) runs on every cost calculation in `Blueprint.UpdateBlueprintStats` and in `GenStep_ScatterRealRuins`. A `RuinsWealthReport` is created inside `BlueprintTransferUtility.Transfer`. Debug code is woven into production code, with a small cost. Suggest guarding it with `RuinsWealthReport.Enabled` or `[Conditional]`.

### R16. Debug actions temporarily overwrite global settings
`DebugActionsRealRuins` ("Sample ruins wealth") and `POITypeSampler` change `logLevel` and `debugLogRuinsWealth` and restore them afterwards. If `WriteSettings` runs in between (the settings window is closed), the temporary values are saved. Better: a local override flag in `Debug` instead of changing the setting.

### R17. All of `RealRuins.cs` was reformatted to Allman braces
The rest of the codebase uses K&R. The whole-file reformat (`git diff -w` shows most of the 447 changed lines are braces) makes blame and future merges noisy and mixes styles in one project.

### R18. A dead option in the planetary loader UI
`Page_PlanetaryRuinsLoader.areaStrict` ("Area filtering" checkbox) does nothing. *(pre-existing: it went to an unused `itemsStrict` parameter; now it isn't passed at all.)* Remove it or wire it up.

### R19. Signs & Memorials text support removed as "dead code"
`5ff7641` removed both writing (`SnapshotGenerator`) and restoring (`BlueprintTransferUtility`) of `SaM.CompText`. The code did work when that mod was loaded. Removing it is consistent with the "no mod-specific special cases" rule, but it's a feature removal and should be in the changelog.

### R20. A cost cap of 0 on the caravan/POI sliders leaves almost nothing
`AdvancedSettingsPage` sliders go from 0 to 100,000. 0 becomes `ToCostCap(0) = 1`, which strips the ruin down to basic structure, and the caravan `minimumCostRequired` becomes 0. The per-map slider starts at 1,000, so these two are inconsistent with it. Suggest a 1,000 minimum, or treat 0 as "no ruins" explicitly.

### R21. `RuinsWealthLimits` reads live game state
`RuinsWealthLimits.For()` sums `Find.Maps` and `Find.WorldObjects.Caravans` inside an options factory, so the same call gives different options depending on hidden state. It's documented and called on the main thread only, but it is the reason the quest's blueprint search (`QuestNode_FindBlueprint`) and the site's map generation (`ForAbandonedBase.minimumCostRequired`) can disagree on the cap days later. That's acceptable; just be aware of it.

### R22. A POI's leave cooldown changes the shared def props *(pre-existing)*
`RealRuinsPOIWorldObject.ShouldRemoveMapNow` sets `cooldownComp.Props.durationDays`. `Props` belong to the def, so the last value applies to every POI. Not new, but it's in a file that changed a lot and should be fixed on the next pass.

### R23. Leftover map generation side effects *(pre-existing, kept by the refactor)*
`GenStep_RealRuinsSite.Generate` and `GenStep_ScatterRealRuins` call `Find.TickManager.Pause()` during map generation, so generating a map has a global side effect on game time. The new base class was the natural place to remove it.

---

## Not regressions (checked and fine)

- `TrippingTrigger` removal: the def was never spawned, so old saves can't contain it. Root `Defs/` (≤1.5) still has it, but it's overridden by path in `1.6/` and `1.7/`.
- The `MapRuinsStore` world component removal is handled by `World_ExposeComponents_RemoveLegacyStore_Patch`.
- `POIType` new values are appended at the end, so saved ordinals stay valid. There is a comment saying so.
- `RaidTrigger` changed from a Normal to a Rare ticker. `ticksLeft` already counted rare ticks, so saved triggers keep their timing.
- Transporter arrival actions keep the same Scribe keys (`site`, `arrivalMode`) in the new base class.
- Translations: non-English files don't have obsolete keys left after the renames. Missing keys fall back to English (between 58 for Japanese and 129 for French).
