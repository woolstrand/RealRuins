# Changes since the last release, by group

Baseline `b3124b5` (2026-05-17) → `development` `15ee008` (2026-11-08). The commit hashes are there so you can look up the details.

## Suggested grouping

Your list: storage & cache, tech level, wealth control, difficulty control, scattering control (regular / event / planetary), debug.
**What it misses** (each has enough content for its own changelog section):

1. **Server & uploads.** This is separate from local storage: what leaves the player's machine. New API host, staging, a retry queue, the game-over snapshot, never uploading POI maps.
2. **Map access & caravans.** Arrival by caravan, pods and shuttle, the caravan reform modes, the manual-mode exit lock.
3. **World & POIs.** New POI types, the classifier, icons, taking over a POI at game start. This is wider than "planetary scattering": it's about what appears on the world map and who owns it.
4. **Compatibility & stability.** Mod compatibility (DBH, minified things, wall attachments, xenogerms), pawn restore fixes, threading, the .NET 10 crash. Players read this section first.
5. **Game versions & packaging.** RimWorld 1.7 support, the 1.6/1.7 shared DLL, LoadFolders, mod icon.
6. **Save compatibility.** Removed world component, RuinedBaseComp fallbacks, retired incident. Needed in the changelog ("old saves load fine").
7. **Settings UI & localization.** New tab, field refresh after import/reset, translation proofreading and key renames.
8. *(internal, not for the changelog)* **Architecture.** ScatterContext, ScatterOptionsFactory, GenStep_RealRuinsSite. Worth listing for yourself because it's where regressions come from.

"Difficulty control" works better as **Threats & defenders** (inhabitants, raiders, raid triggers, thieves). The difficulty settings themselves (force multiplier, threat scale) are a small part of it.

---

## 1. Storage & cache
- Per-world planetary blueprint cache control: a table on the Network & Cache tab (seed, coverage, map size, blueprint count, size) with a Clear button for each world. `f054836`
- The cache version counter, so the UI rescans only when something changed. `f054836`
- The planetary loader downloads only blueprints that aren't on disk yet; the download limit counts new ones only. `9af57b1`
- Fixed the shrinking planetary blueprint pool when restarting a game with the same seed. `9af57b1`
- The cache isn't trimmed when the limit is unlimited (C2). `c4ac50e`
- Background store thread is guarded against exceptions (H3). This fixes the 1.7/.NET 10 crash on load. `7ed6386`. *Still open: M15, a race between cache scanning and deletion.*
- `LoadSomeSnapshots` stops retrying once retries run out (H2). `6f90283`
- A download counts as loaded only once the file is on disk; completion is reported once. `30871ba`, `0a9faae`
- Comment on why `Compressor.ZipFile` deletes the original (H7). `481e097`

## 2. Server & uploads
- New production API host `api.woolstrand.art`, and a staging environment (debug toggle). `8dbb43a` ⚠ R1
- The seed is URL-escaped in requests. `9daf3c5`
- Pending upload queue: snapshots wait in `RealRuinsPendingUploads` until the server confirms them, and are retried on the next start (max 10, max 14 days old). `26d9506`
- Game-over snapshot: on "everyone is dead" (`GameEndTick`) and on the game-over dialog. It ignores the 3-hour limit and falls back to any home map. `26d9506` ⚠ R3
- Raided planetary POI maps are never uploaded. `c26e44e`
- Snapshots save the real chronological age instead of the biological one (C1). `24dd45a`

## 3. Tech level (new feature)
- Tech level cap for ruins: `TechLevelFilter` works out an era for every def from its own tech level, research, recipe and ingredients. Strict or limited strategy. Groups: regular ruins, POIs, events. `c045666`
- Walls and doors above the cap become plain ones (rooms stay closed). Other things are removed, and so is container content above the cap. `c045666`
- POI faction max tech level follows the cap. `c045666`
- POI types fit the era: City → Village, Factory/Research/PowerPlant → Workshop, Military → Castle, Communication → Outpost, and so on. `592984a`, `0e8c7d4`
- New settings tab. `c045666`

## 4. Wealth control
- Ruins cost uses the game's market value and counts container contents. `7ec9dd1`
- A unified cost cut: the most valuable things go first. Walls are replaced in regular ruins and dismantled in scavenged sites (`cutDismantlesWalls`). `93c8abb`
- Wealth budget per map for regular ruins (`ruinsWealthPerMap`, default 5k), with a reserve per chunk. `9f75968`, `daead1e` ⚠ R5
- Options: scale with proximity, ignore basic structure, count actual (damaged) value. `daead1e`
- Caravan ruins: cost cap (`caravanRuinsCostCap`, default 25k), minimum blueprint cost lowered to 15k. `8024d10`, `9625b97`
- Abandoned POI cap (`poiRuinsCostCap`, default 15k). `ec1b06d`
- Scaling of all caps with colony wealth (`ruinsScaleWithColonyWealth`, reference wealth, 0.5× floor). `b30f04c`, `ec1b06d`
- Scavengers take things out of containers instead of carrying the whole container away. `b35893e`
- The item cost limit judges containers by their own value. `7ec9dd1`
- Abandoned base: map wealth on arrival no longer overwrites the scavenging cap (`referenceWealth`). `9daf3c5`
- Wealth descriptions are shared (`WealthUtility`).

## 5. Threats & defenders (your "difficulty control")
- Animal inhabitants reworked: meat-eating species fit for manhunter packs, at most 5, a capped budget, released into the wild after 2–4 days (`LordJob_AnimalInhabitants`). `dbe03de`
- No animals or mechanoids on the starting map (regular ruins and POI attack start). `26d9506`, `dbe03de`
- Caravan ruins raiders scaled by the ruin's value, at most 5,000 points (`CaravanRuinsForcesGenerator`); `forceMultiplier` now applies. `9625b97`
- Threat scale 0 means true zero (no inhabitants). Included in the baseline (`e27e4b3`), and the animal generator now follows it as well.
- Forces generators merged and fixed: raid trigger spawning and starting parties moved into the base class, and the starting-party loop no longer hangs on maps without spawn cells. `2b84012`
- Mechanoid force loop fixed. `26d9506`
- Defenders are picked with `TryRandomElementByWeight` (no crash on empty lists). `2704294`
- Raid triggers: the def uses the rare ticker, with a log of the expected fire time. `9daf3c5`
- Friendly (non-hostile) ruin raiders don't steal the player's items or gravship cargo. `26d9506`
- Abandoned base with force multiplier 0 still gets its ruins (it used to get none). `fdff6b3`
- Abandoned base V2 raids: `destructionLevel` is saved; old saves recalculate it.

## 6. Scattering control

### 6a. Regular ruins (generic maps)
- Proximity now affects the chunk **count** (normalized), not only the size. `fdff6b3`
- Chunk count rounds up. `fdff6b3`
- Ruins avoid quest-critical areas (`MapGenerator.UsedRects`). `dce7ab8`
- GenStep order 301 → 760. `dce7ab8`
- No real ruins on the starting POI map. `a0d70db`
- "Preserve standard ruins" fixed (vanilla runs once). Other `ScatterRuinsSimple` gensteps (underground) are no longer suppressed. `a0d70db`

### 6b. Event ruins (abandoned base quest, caravan ruins)
- The Pristine Ruins incident is retired: `baseChance 0`, and firing it sends a letter. The quest replaces it (weight 1.5, not affected by points). `39915ec`
- The event toggle gates the quest; quest TestRun is cheaper (no world object, no blueprint loading). `39915ec`
- Quest: a failed blueprint search no longer expires the ruins at once. `39915ec`
- Content filters (walls/doors only, no items) no longer apply to the abandoned base. `fd7e23b`
- Caravan ruins: see sections 4 and 5.

### 6c. Planetary (POIs)
- Loader: blueprints are prepared on a background thread and world objects are created on the main thread, a few ms per frame. Closing the window doesn't stop the transfer. `0a9faae`, `30871ba`
- Generic POI classification: `DefRoles`, `BlueprintAnalyzerResult`, `POIClassifier`. `0e8c7d4`, `cefee27`
- New types: Village, Castle, Workshop, Shipwreck, with icons. `592984a`, `4cd4c6c`, `3e97aeb`
- Military base size bands lowered. `dd3d5c1`
- Content filters apply to ruined POIs only, not to intact ones. `fd7e23b`

### 6d. Shared pipeline (all scatterers)
- Roofs: unsupported roofs are removed after generation (`RoofSupportUtility`). `e1059bd`. New "spawn roofs" setting. `9bffdc7`
- Containers are filled after spawning, one item at a time; only items are settle-ticked; contents of minified things aren't ticked. `2011bd3`, `9b07e75`, `2bf6b3a`
- Toxic wastepacks are never spawned. `26d9506`

## 7. World & POIs (game start, ownership)
- POI takeover at game start: the player owns everything (`claimEverything`), nothing is forbidden, and the fog is cleared. `dd3d5c1`
- A starting POI chosen for another tile is discarded. `4da4873`
- A stale starting-POI context is ignored in a running game (it used to turn an attacked enemy base into a "starting POI"). `2729fb2`
- POI owner and attack: relations hit is the same as vanilla (M7, won't fix).

## 8. Map access & caravans
- Caravan, pod and shuttle arrival actions merged into base classes. Caravan arrival target is saved. Shuttles can land at ruins and POIs. `c646c69`
- Caravan reform modes: manual mode really locks reforming until the raids are over (leave by walking off the edge). The raid lifecycle runs in every mode. Instant mode no longer switches the faction. `1b44239`
- Caravan reform verification fixes. `1b44239`

## 9. Compatibility & stability
- Dubs Bad Hygiene: things that would overlap a pipe of the same mode are skipped, and the wipe guard checks the right thing. `bc4db57`, `957c5a4`
- Minified things: skipped when the inner thing isn't minifiable or its graphic fails. `e21d50d`, `30e24d5`, `fd95f21`
- Wall attachments are kept until their wall spawns; detached ones are removed. `c9a7235`
- Stacked items named like walls aren't treated as walls. `46b95ef`
- A missing item replaced by a wall or door gets its stack count reset. `87b9eb6`
- Pawn-spawning buildings (hives and the like) aren't restored. `9d41dc4`
- Pawn restore: apparel the pawn can't wear is skipped; missing `sourceHediffDef` and injuries on parts that can't be hit are skipped; missing body types and name mismatches are handled; corpses' inner pawns are saved. `a469108`, `36f0a7f`, `256599b`, `8bfd9af`
- Scribe mode is restored after hediff deserialization (H6, then narrowed). `7f66155`, `a346555`
- Xenogerms without a gene set are initialized. `26d9506`
- Things with stuff that can't make them have their stuff cleared. `fdff6b3`
- Thread safety: `ThreadSafeRand`, `CoroutineManager.RunOnMainThread`, timers replaced by coroutines. `30871ba`, `0a9faae`
- BlueprintAnalyzer returns early when there's no internal area (H8). `b9f9b54`
- Signs & Memorials sign text support removed. `5ff7641` (R19)

## 10. Game versions & packaging
- RimWorld 1.7 support (a `1.7/` folder; LoadFolders). `22173fd`, `f8b00a7`, `1eb38f4`
- One DLL built for 1.7 and mirrored into 1.6. `f3b2913`, csproj. ⚠ R2
- Per-version About.xml copies removed. `3bfe302`
- Mod icon. `840286e`

## 11. Save compatibility
- The legacy `MapRuinsStore` world component is stripped on load. `38c32d3`
- Sleeping or dead colonists are counted as the colony's in takeover mode. `38c32d3`
- RuinedBaseComp fallbacks for old saves (reference wealth, destruction level). `9daf3c5`
- The retired incident def is kept so the storyteller history resolves.

## 12. Settings UI & localization
- Tech level tab; new wealth, roofs and debug controls.
- Text and number fields refresh after import or reset. `5ff7641`
- The import/export text area is read-only.
- Malformed French and Traditional Chinese XML fixed. `81d10c1`
- English and Russian proofread, obsolete keys removed, planetary loader keys renamed to `RealRuins.PlanetaryLoader.*`. `38eaf65`, `15ee008`, `92566e9`
- Japanese update (pre-baseline translator PR, in the baseline).

## 13. Debug
- Debug actions: Generate pristine ruins quest, Sample ruins wealth, Sample POI types (main menu and in game), Simulate random quest rolls, Toggle ruins wealth log, Focus on tile. `dbd02c8`, `93726ea`, `0e8c7d4`
- DEV: Explore / Reset gizmos on abandoned bases and POIs (`DevSiteTools`).
- Wealth measurement log and report, estimated gap by def. `cd433d0`, `e13a213`
- Debug settings: wealth log, staging. ⚠ R6
- Windows support for the agent testing tools and skills. `a2bdac6`, `ba98922`, `fd7e23b`

## 14. Internal architecture (not for the changelog)
- `ScatterContext` (options + `PipelineSettings` + `ScatterState`) replaces runtime state that was stored inside `ScatterOptions`. `fd7e23b`
- `ScatterOptionsFactory` is the single place each scatterer builds its options; `defaultScatterOptions` is never mutated. `fd7e23b`
- `GenStep_RealRuinsSite` base for abandoned base, caravan ruins and POI. `fdff6b3`
- `RuinsWealthLimits` and `RuinsWealthBudget`. `ec1b06d`, `9f75968`
- Dead code removed (SRTS patch, `PostOpen` probe, `TrippingTrigger`, old `BlueprintAnalyzer`, `Constants`). `5ff7641`, `1eb38f4`
- Docs: `docs/01–08`, FIXES_QUEUE, IMPROVEMENTS_AND_FEATURES, PROBLEMATIC_ITEMS, PROJECT_CONTEXT.
