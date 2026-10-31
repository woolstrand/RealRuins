# RealRuins — Possible Improvements & New Features

This document lists potential improvements (optimizations, refactors, fixes) and new feature ideas. Items are organized by theme. Effort estimates are rough (S=small/hours, M=medium/days, L=large/week+).

---

## BUG FIXES

Bugs and weak points are tracked in [FIXES_QUEUE.md](FIXES_QUEUE.md). They are not new features but should be addressed first since they affect correctness.

---

## OPTIMIZATIONS

### A. Cache `ThingDef` Resolution Across Pipeline Stages
**Effort**: M  
Cache the resolved `ThingDef` and `TerrainDef` on `ItemTile`/`TerrainTile` after first lookup in `BlueprintLoader`. Eliminates 2–3 redundant `DefDatabase` lookups per tile per pipeline run. On a 200×200 blueprint with 5,000 tiles, this saves tens of thousands of dictionary lookups.

### B. Optimize `Blueprint.RemoveWall()` with a Reverse Room Index
**Effort**: M  
`RemoveWall()` performs a full O(w×h) map scan per call. Maintain a dictionary `roomId → List<IntVec2>` so room merges are O(room_size) instead of O(total_area). This converts the O(w²×h²) deterioration+scavenging loops to O(w×h × avg_room_size), which is dramatically faster on large dense blueprints.

### C. Cache Signs & Memorials Reflection Lookups
Done: Signs & Memorials support removed (FIXES_QUEUE L3).

### D. Cache `NetworkCacheSettingsPage` Disk Stats
Not needed: the values are already cached (FIXES_QUEUE L6).

### E. Defer Blueprint Stats Calculation
**Effort**: S  
`UpdateBlueprintStats(includeCost: true)` is called twice during the pipeline (in `BlueprintFinder` and again after preprocessing). Move the cost-including version to only after preprocessing since the preprocess pass changes the item set. One call instead of two.

---

## RELIABILITY IMPROVEMENTS

### F. Atomic `Compressor.ZipFile()` via Temp File
**Effort**: S  
Write compressed data to `{path}.tmp` first, then `File.Move()` to replace the original atomically. The original is only deleted once the new file is safely written. Eliminates data loss risk on write failure.

### G. Fix `Scribe.mode` in `PawnRestoreUtility` with `try/finally`
**Effort**: S  
Wrap the `Scribe.mode` mutation in `try/finally` to guarantee it's always reset to `Inactive`, even if an exception fires mid-deserialization.

### H. `BlueprintRecoveryService` Decompress-Before-Repair
**Effort**: S  
Decompress the `.bp` file first, repair the XML text, re-compress, and write back. Currently the service operates on compressed bytes as if they were text, making it inoperative.

### I. Thread-Safe `SnapshotManager` Download Queue
**Effort**: S  
Replace `List<string>` + `.Pop()` with `ConcurrentQueue<string>`. Replace `snapshotTimestamps` with `ConcurrentDictionary<string, DateTime>`. Eliminates the race conditions identified in the weak points report.

### J. Reduce Scope of Harmony Reflection Dependencies
**Effort**: M  
Add null checks on all `AccessTools.Field()`/`AccessTools.Method()` results in `RealRuins.cs`. Log a clear warning if any accessor fails to resolve. Wrap the dependent code in a null check. This makes the mod degrade gracefully rather than throwing NullReferenceExceptions when RimWorld changes its internal field names.

---

## ARCHITECTURE IMPROVEMENTS

### K. Extract Shared Base Class for Arrival Actions
**Effort**: M  
`TransportPodsArrivalAction_VisitRuins` and `_VisitRuinsPOI`, and `CaravanArrivalAction_VisitAbandonedBase` and `_VisitRealRuinsPOI`, share ~60–70% of their code. A shared abstract base class with virtual methods for the varying parts (letter type, faction logic, cooldown check) would eliminate ~200+ lines of duplication.

### L. Consolidate `GenerateForces()` into Base Class
**Effort**: S  
`CitizenForcesGeneration.GenerateForces()` and `MilitaryForcesGenerator.GenerateForces()` are near-identical. Move the common loop to `AbstractDefenderForcesGenerator` with virtual properties for customizable parameters.

### M. Rework POI Destruction Ratio
**Effort**: S–M  
**File**: `Source/Classes/DynamicMapObjects/RealRuinsPOIWorldObject.cs`, `ShouldRemoveMapNow()` / `CurrentMapWealth()`
POI and pristine-ruins wealth formulas differ, but that is fine: the two systems never compare against each other's numbers, and each is consistent with itself. `RuinedBaseComp` uses `GetTotalMapWealth` on both sides, and the inspect-string thresholds in both systems are applied to blueprint costs. The weak spot is inside the POI: `ratio = (wealthOnEnter - CurrentMapWealth()) / approximateSnapshotCost`.
- The numerator counts every thing on the map: pawns, corpses, plants, rocks, chunks and filth, priced with `ThingComponentsMarketCost`. For things without a cost list that is `BaseMarketValue`.
- The denominator is `BlueprintAnalyzer.totalItemsCost`: only blueprint items, measured on the full snapshot before deterioration.
So killing or driving off defenders (pawns removed, corpses added at a different price), chopping plants and mining all count as "stolen". Deterioration makes the denominator larger than what actually spawned.
**Current effect**: The outcome on leaving (owner restores, may abandon, or POI destroyed, at the 0.1/0.3 thresholds) is noisy. A fight-heavy visit with little looting can destroy a POI. A heavily deteriorated POI is hard to destroy by looting. The enter cooldown (`difference / 2000` days) is affected the same way. The effect is on balance only.
**Effect of the rework**: POI fate tracks what the player actually took. This changes how often POIs get destroyed, so balance changes; check the 0.1/0.3 thresholds again afterwards. There is no save impact if `wealthOnEnter` keeps its meaning. Old saves store the old value, which mixes slightly for maps that are open when the update is loaded.
**Possible approach**: In `CurrentMapWealth()`, count only things that blueprints can contain: skip `Pawn`, `Corpse`, `Plant`, `Filth`, `Mote`/`Projectile`, and mineable/natural rock (`def.building?.isNaturalRock`, `def.mineable`). Use the same filter on enter and on leave. Then either divide by `wealthOnEnter` instead of `approximateSnapshotCost`, so both sides measure the spawned map, or keep the blueprint cost only as a lower bound. Dividing by `wealthOnEnter` is the smaller and more robust change.

### N. Move `POIType` Enum into `RealRuins` Namespace
Done (FIXES_QUEUE L13).

### O. Remove Dead Code
Done (FIXES_QUEUE L7, L9).

---

## GAMEPLAY IMPROVEMENTS

### P. Blueprint Quality Scoring for Download Prioritization
**Effort**: M  
Currently blueprints are downloaded randomly. The API already has enough metadata to pre-filter. Client-side, blueprints could be scored based on size, estimated cost, and biome match before placement. Weight the random selection toward higher-quality blueprints rather than uniform random.
**Rejected**: Blueprint containts modded items. Backend does not know what mods were used and what mods are the client using. There is no way to determine anything before downloading and processing a blueprint by a specific game instance with a specific mod set.

### Q. Biome-Aware Blueprint Matching
**Effort**: M  
The API returns `biomeName` in `PlanetTileInfo`. Currently the biome is only used in the planetary ruins strict-filter option. Standard scatter could use biome similarity weighting when selecting blueprints from the local cache — preferring blueprints from similar biomes (matching climate) when available, but falling back to any blueprint if none match. Would make ruins feel more contextually appropriate.

### R. Gradual Ruin Discovery / Fog of War on Ruins Maps
**Effort**: L  
Instead of revealing the full ruins map immediately on entry, implement a "fog of discovery" where unexplored rooms stay hidden. This would make exploring the ruins more engaging. Could use the `wallMap` room data from the blueprint pipeline to reveal room by room.
**Rejected**: Already is a part of the base game

### S. Configurable Faction Matching
**Effort**: M  
Allow players to configure whether ruins should belong to factions that are relevant to their current game (e.g., factions the player has already met) versus any random faction. Would make faction-related lore (art descriptions, letters) feel more grounded.

### T. Blueprint Reputation / Rating System
**Effort**: L  
Allow players to rate blueprints they encounter (thumbs up/down via a UI gizmo on the ruins map). Upload ratings alongside the blueprint. Server-side, weight random blueprint selection toward higher-rated blueprints. Would gradually improve the average quality of ruins encountered.
**Rejected**: Already implemented on backend, but barely used.

### U. Persistent Ruin History
**Effort**: M  
Track which ruins the player has visited across saves (by `snapshotId`) in a separate save slot. Show "already visited" indicator in the letter or world map tooltip. Prevent the same base from appearing as ruins more than once per player.

### V. Modded Content Graceful Handling
**Effort**: M  
When a blueprint references a def that doesn't exist (mod removed), currently items are silently dropped. Optionally: substitute with a visually similar vanilla item (e.g., a modded power conduit → vanilla conduit) based on a category/tag lookup instead of just skipping. This would make ruins feel more complete even when source mods differ.

---

## NEW FEATURES

### W. "Ruin Reconstruction" Quest Line
**Effort**: L  
A multi-stage quest where the player is hired by a faction to restore a specific ruin to livable condition. Success criteria: bring ruin map wealth above a threshold. Could use the existing `wealthOnEnter`/`blueprintCost` infrastructure. Reward: permanent faction goodwill boost + resources.

### X. Blueprint Preview in World Map Tooltip
**Effort**: M  
When hovering over a `RealRuinsPOIWorldObject`, show a tiny ASCII-art or icon-grid preview of the blueprint layout using the `wallMap` data. Would give players a sense of the base size/shape before committing to entering.

### Y. Asymmetric Multiplayer Ruins (Synchronous Sharing)
**Effort**: L  
For local co-op or LAN play: one player's current save can be directly used as the ruins blueprint for another player in the same session (peer-to-peer transfer, bypassing the server). Requires a direct transfer API endpoint or local discovery mechanism.

### Z. Dynamic Scavenging Display on World Map
**Effort**: M  
Show the `currentCapCost` decay visually on the `AbandonedBaseWorldObject` world object icon — e.g., a shrinking wealth bar or color shift from gold to grey as the ruins are scavenged by NPCs over time. Currently the state is only visible when the letter is still active or the player opens the info dialog.

### AA. "Leave a Message" Feature
**Effort**: M  
Allow players to attach a short note (up to ~140 characters) to an uploaded blueprint. The note would be preserved in the XML header and displayed in the ruins discovery letter when another player finds the ruins. Creates a social connection between players ("These are the ruins of [player name]'s colony: [message]").

### BB. Ruins Difficulty Scaling by Player Wealth
**Effort**: M  
When placing ruins on a map, scale the `forceMultiplier` dynamically based on the visiting player's current colony wealth (from RimWorld's `WealthWatcher`). Richer colonies face tougher ruins defenders. Currently the multiplier is a global static setting. This would create appropriate challenge scaling without manual tuning.

### CC. Multi-Layer Ruins (Odyssey DLC Integration)
**Effort**: L  
The `PlanetTileInfo` DTO already has a `tileLayer` field, suggesting server-side multi-layer support exists. Full multi-layer ruins would record the underground layer(s) separately and place them on the corresponding underground map layers when a player enters the ruins. Would make ruins from Odyssey bases feel truly multi-floor.

### DD. Blueprint Aging Visualization
**Effort**: M  
Add a way for players to see how "old" a ruin is in the discovery letter or inspect string — calculated from `dateShift`. Ranges like "ancient ruins" (very old) vs "recently abandoned" (mild deterioration). This information is already computed but not surfaced to the player.

### EE. Custom Upload Metadata Tags
**Effort**: M  
Let players tag their uploaded blueprints before upload (e.g., "megabase", "bunker", "early base", "art installation"). The API could then support filtering by tag, letting players who want ruins of a specific type configure a preference. Server-side change required but simple.

### FF. Ruins Salvage Efficiency Stat
**Effort**: S  
Add a tracked statistic: "Total resources recovered from ruins" (using `wealthOnEnter` data). Show in a custom info tab or in the end-game stats screen. Simple data collection on map exit, surfaced to the player for a sense of accomplishment.

---

## TECHNICAL DEBT REDUCTION

### GG. Upgrade to Newer C# Features
**Effort**: S–M  
The project targets C# 7.2 (LangVersion = 7.2 in csproj). Upgrading to 8.0 or 9.0 (still compatible with .NET 4.7.2 assembly output via Roslyn) would enable null-coalescing assignment (`??=`), pattern matching improvements, and `switch` expressions that would simplify many multi-branch patterns throughout the code.

### HH. Structured Configuration Object for `BlueprintTransferUtility`
**Effort**: S  
The constructor takes many implicit flags derived from `ScatterOptions`. Extracting these into a named configuration struct would make the constructor more readable and testable.

### II. `ScatterOptions` User-Configured vs Runtime Field Split
**Effort**: M  
`ScatterOptions` mixes user-configured, persisted fields and runtime state in the same class with no structural enforcement. Consider splitting into `ScatterConfig` (persisted, user-configured) and `ScatterContext` (runtime, transient) to make the boundaries explicit.

### JJ. Replace `Scribe.saver.DebugOutputFor()` in `SnapshotGenerator`
**Effort**: L  
`EncodePawn` uses RimWorld's save system (`Scribe.saver.DebugOutputFor()`) as a crude XML serializer. This is a private API that could break on any update. Writing a proper manual serialization routine for pawn data would be more robust, though significantly more code.
