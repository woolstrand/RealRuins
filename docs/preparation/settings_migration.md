# User-facing settings: changes and migration

Baseline: last release `b3124b5` (2026-05-17). Current: `development` @ `15ee008`.

## Where settings live, and what "opening an old save" means

- **All RealRuins settings are global mod settings.** They are stored in `Config/Mod_<id>_RealRuins_ModSettings.xml`, not in savegames. Opening an old save doesn't read or change any setting. The settings are migrated once, when the new version first starts and reads the old config file.
- Reading an old config: `Scribe_Values.Look` with a default. A **missing key gets the default** (no error). A **removed key** is still in the XML until the next write, and RimWorld ignores it without any message. Settings that need special migration: **none**.
- The settings **import string** (Base64, Network & Cache tab) uses a whitelist `switch`. Unknown keys from an old string are skipped, and keys missing from an old string keep their current value. Importing an old string into the new version works, and so does importing a new string into the old version.
- **Verdict for all settings below: no setting is lost, nothing crashes, no error pops up.** The things to decide on are *behavior changes under the same values* and *new defaults that change gameplay for players who update* (marked ⚠).

What *does* live in savegames (world objects, comps, arrival actions) is covered in the last section.

---

## 1. Settings that existed before

| Setting (config key) | Tab | Still there? | Renamed? | How it works now vs before | Migration |
|---|---|---|---|---|---|
| `offlineMode` | Network & Cache | yes | no | Unchanged. Snapshots are stored locally as before. | safe |
| `allowDownloads` | Network & Cache | yes | no | Unchanged. | safe |
| `allowUploads` | Network & Cache | yes | no | Now also controls the new **pending upload queue**: if uploads are off at startup, queued snapshots are **deleted**. The game-over snapshot follows this setting too. | safe |
| `diskCacheLimit` | Network & Cache | yes | no | Unchanged limit. "Reset to defaults" now resets it as well (before, it was skipped). The new planetary cache table is a display only. | safe |
| `logLevel` | Debug | yes | no | Unchanged. Several messages moved from Error to Warning, so there's less red at the default level. | safe |
| `debugKeepSnapshotsAfterUpload` | Debug | yes | no | Kept files are now moved to the system temp folder (out of the pending queue) instead of being left where they were. | safe |
| `debugTileFocusId`, `debugExtras` | Debug | yes | no | Unchanged. | safe |
| `caravanReformType` (0 auto, 1 instant, 2 manual) | Events & Advanced | yes | no | ⚠ **Behavior changed.** **Manual:** before, it ran the raid lifecycle but nothing read `mapExitLocked`, so it locked nothing. Now reforming is **really blocked** until the raids end (gizmo disabled, the `CanFormOrReformCaravanNow` patch), and the player leaves by walking off the map edge. **Instant:** the abandoned base no longer switches to the player faction once cleared. **All modes:** the raid lifecycle (`FightingWaves` → … → "you can leave" letter) now runs in every mode, not only in manual. | value safe; see the save notes (§4) |
| `startWithoutRuins` | Map generation | yes | no | Unchanged. Separately, the starting map **never** gets animal or mechanoid inhabitants now, whatever this setting is. | safe |
| `preserveStandardRuins` | Map generation | yes | no | Behavior refined. **On:** vanilla ruins run once (before, they could run twice: once from the patch and once from inside the real ruins genstep). **Off:** only the vanilla `ScatterRuinsSimple` def is suppressed. Other gensteps of the same class (for example underground ruins on cave maps) are no longer removed. | safe |
| `forceMultiplier` | Events & Advanced | yes | no | ⚠ **Wider effect.** It now also scales the raiders at **caravan small ruins** (before, that event used vanilla threat scale only). At 0: no animal inhabitants (`powerMax <= 0`), no caravan raiders, no raid triggers. The abandoned base is now **still generated** at 0 (before, 0 skipped the whole scatter, so the map had no ruins at all). | safe |
| `ruinsCostCap` ("absolute wealth cap") | Events & Advanced | yes | no (key and UI label unchanged) | ⚠ **Scope narrowed and documented.** It now applies only to the **abandoned base (quest and old event)**: through `RuinsWealthLimits.For(AbandonedBase)`, `QuestNode_FindBlueprint` and `QuestNode_GenerateRuinsObject`. Caravan ruins and POIs have their own caps now (below). It is multiplied by colony wealth if scaling is on. The search minimum stays `min(100k, cap)`. The quest falls back to the cap when no cost was stored, so ruins no longer expire instantly. | safe |
| `useRuinsForcesGenerationV2` | Events & Advanced | yes | no | Same logic. The reference wealth is now kept separately (`referenceWealth`) instead of overwriting `currentCapCost`, and `destructionLevel` is saved. Old saves fall back to `currentCapCost` (see §4). | safe |
| `disableFriendlyRaids` | Events & Advanced | yes | no | Unchanged. Additionally, a new patch stops non-hostile "friendly" ruin raiders from stealing the player's things and gravship cargo. | safe |
| `enableAbandonedRuinsFoundEvent` | Events & Advanced | yes | no | ⚠ **Meaning moved.** The incident `Quest_PristineRuins` is retired (`baseChance 0`; firing it by hand only sends a letter). This toggle now gates the **`RealRuins_AbandonedBase` quest** (`QuestNode_FindBlueprint.TestRunInt`). Before, the quest was **not** gated by it. Players who had it **off** stop getting the quest now. Players who had it **on** get the quest more often (weight 1.0 → 1.5) instead of the incident. Consider renaming the label to "Pristine ruins quest". | safe |
| `enableCaravanFoundRuinsEvent` | Events & Advanced | yes | no | Toggle unchanged. ⚠ The event itself changed: the main ruin is now capped (`caravanRuinsCostCap`), the minimum blueprint cost dropped from 25k to `min(15k, cap)`, and raiders are scaled by the ruin's value (`CaravanRuinsForcesGenerator`). | safe |
| `spawnBlacklist`, `materialBlacklist`, `fallbackMaterial` | Events & Advanced | yes | no | Same parsing (newline or comma). Now parsed once per scatter run (`PipelineSettings`). The text fields now show values set by import or reset (the old buffer used to write stale text back). | safe |
| `defaultScatterOptions.densityMultiplier` (`so.densityMultiplier`) | Map generation | yes | no | ⚠ Chunk count now rounds **up** (`Math.Ceiling`), so a small density on a small map still gives 1 chunk. | safe |
| `so.minRadius`, `so.maxRadius` | Map generation | yes | no | Unchanged. | safe |
| `so.deteriorationMultiplier`, `so.scavengingMultiplier` | Map generation | yes | no | Unchanged. | safe |
| `so.itemCostLimit` | Map generation | yes | no | Refined: a container is judged by its **own** value. Contents above the limit are taken out and the container stays (before, a bookcase full of expensive books could be removed entirely). 0 and 1000 still mean "no limit". | safe |
| `so.hostileChance` | Map generation | yes | no | Same chance. Not applied on the starting map. ⚠ Animal inhabitants reworked: meat eaters only, at most 5, capped budget, and they become wild after 2–4 days. | safe |
| `so.enableProximity` | Map generation | yes | no | ⚠ **Behavior changed.** Before, proximity changed chunk *size* only (and could zero the count far from settlements). Now it also changes chunk *count* (denser near settlements, sparser far away), normalized so the average covered area over distances 4–16 stays the same. A map on a settlement or site tile keeps the plain count. | safe |
| `so.disableSpawnItems`, `so.wallsDoorsOnly` | Map generation | yes | no | ⚠ **Scope narrowed.** They still apply to regular ruins, caravan ruins and **ruined** POIs. They **no longer** apply to the **abandoned base** or to **intact** POIs (before, both got them through `defaultScatterOptions.Copy()`). | safe |
| `so.decorationChance`, `so.trapChance` | (not in UI) | yes | no | Unchanged. | safe |
| `planetaryRuinsOptions.*` (`allowOnStart`, `downloadLimit`, `transferLimit`, `excludePlainRuins`, `abandonedLocations`, `disableSpawnFriendlyLocations`) | Planetary | yes | no (only UI translation keys renamed: `RealRuins.PlanetaryRuinsSettings.Caption` → `RealRuins.PlanetarySettings.Caption`, `MapsModuleButton` → `PlanetarySettings.MapsModuleButton`) | `downloadLimit` now counts **new** blueprints only. Ones already on disk for this world aren't downloaded again and don't use up the limit. The transfer only processes blueprints that are on disk. The tech level cap also limits the faction of a POI. | safe |
| `allowInstantCaravanReform` | — | **removed** | — | Was already dead at baseline (saved but never read, not in UI). Replaced long ago by `caravanReformType`. | safe (ignored key) |
| `so.claimableBlocks` | — | **removed** | — | Was dead at baseline (set but never read). Its intent now lives in `claimEverything` (internal, POI takeover only). | safe (ignored key; skipped on import) |
| `so.enableInstantCaravanReform` | — | **removed** | — | Was dead at baseline. | safe (ignored key) |

## 2. New settings

All of them are missing from old configs, so updating players get the default.

| Setting | Tab | Default | What it does | ⚠ Gameplay impact for updating players |
|---|---|---|---|---|
| `ruinsWealthPerMap` | Map generation | **5,000** (≥100,000 means unlimited) | Wealth budget shared by all regular ruin chunks of one map (`RuinsWealthBudget`) | ⚠ **Yes.** Was unlimited. Regular ruins get noticeably poorer. |
| `ruinsWealthScaleWithProximity` | Map generation | off | Multiplies the budget by the proximity count factor | no |
| `ruinsWealthIgnoreStructure` | Map generation | on | Plain walls, doors and floors (≤50 per tile) don't count toward the budget | no (it softens the cap) |
| `ruinsWealthCountDamage` | Map generation | off | Counts the actual value after spawning (quality, HP) instead of full price | no |
| `ruinsScaleWithColonyWealth` | Events & Advanced | off | All four caps × max(0.5, colony wealth / reference) | no (off). When turned on: at game start colony wealth is 0, so the caps are **halved** on the starting map. |
| `ruinsReferenceColonyWealth` | Events & Advanced (visible only when scaling is on) | 100,000 | Reference for the scaling above | — |
| `caravanRuinsCostCap` | Events & Advanced | **25,000** (≥100,000 means unlimited) | Cap on the caravan event's main ruin | ⚠ **Yes.** Was uncapped. |
| `poiRuinsCostCap` | Events & Advanced | **15,000** (≥100,000 means unlimited) | Cap on POIs spawned **as ruins** (the minimum of this and the old Gaussian roll) | ⚠ Mild: the Gaussian roll often went above 15k on big blueprints. |
| `spawnRoofs` | Events & Advanced | on | Off means no roofs on any ruins (including intact POIs) | no |
| `techLimitEnabled` | Tech level (new tab) | off | Master switch for the tech level cap | no |
| `techLimitMaxLevel` | Tech level | Medieval | Cap (Neolithic to Ultra) | — |
| `techLimitStrict` | Tech level | on (strict) | Limited mode: an item one level above has a 10% chance to stay, two levels 1%, and so on | — |
| `techLimitRuins` / `techLimitPOIs` / `techLimitEvents` | Tech level | on / on / on | Which groups the cap applies to | — |
| `useStagingEnvironment` | Debug | off | Use the staging server | ⚠ Visible without dev mode and saved; see R6 in `regressions_list.md` |
| `debugLogRuinsWealth` | Debug | off | Wealth log, regardless of log level | no |

### Decision needed (R5)
Three new caps change the game for everyone who updates, without them doing anything. Options:
1. Keep them, and say so clearly in the changelog and Workshop description ("ruins are now balanced by default, set the sliders to max for the old behavior").
2. Migrate: in `ExposeData`, if `Scribe.mode == LoadingVars` and the config already existed (for example `diskCacheLimit` node present) but `ruinsWealthPerMap` is missing, set the three caps to "unlimited". New installs keep the balanced defaults.
3. A one-time letter after the first load with the new version, linking to the settings.

---

## 3. Settings UI changes (no stored data involved)

- New tab **Tech level** between Events & Advanced and Planetary.
- New controls: Map generation (wealth per map + 3 checkboxes), Events & Advanced (colony scaling + reference, roofs, caravan cap, POI cap), Debug (wealth log, staging), Network & Cache (planetary cache table with a "Clear" button per world).
- Text and number fields now refresh after import or "reset to defaults" (`BufferedTextField`, `ReadableLabeledTextInput` with a synced value).
- The import/export dialog: the export text area is now read-only (edits are discarded).
- Translation keys renamed: the whole planetary loader moved to the `RealRuins.PlanetaryLoader.*` prefix. Non-English files were updated (no obsolete keys left). Missing translations fall back to English.

---

## 4. Savegame state (not settings, but it's what "open an old save" touches)

| Item | Change | Old save result |
|---|---|---|
| World component `RealRuins.MapRuinsStore` | Class removed | Stripped from the XML before load by `World_ExposeComponents_RemoveLegacyStore_Patch`, no error. **Test M-03.** |
| `RuinedBaseComp` (abandoned base) | New saved fields `referenceWealth`, `allTriggersFiredTick`, `destructionLevel`, `baseSmallRaidChance` | Missing → defaults. `ReferenceWealth` falls back to `currentCapCost` (old saves overwrote it with map wealth on arrival). `destructionLevel` 0 is recalculated, so raids don't stop for good. ⚠ In **auto and instant** modes the raid lifecycle now runs too, so an old save whose ruin map is still open will move through the states and may send the "you can leave" letter soon after loading. ⚠ In instant mode, a base that already switched to the player faction stays that way (nothing switches it back). |
| `RaidTrigger` | Ticker Normal → Rare | `ticksLeft` already counted rare ticks, so timing is kept. |
| `TrippingTrigger` def/class | Removed | Never spawned, so it can't be in saves. |
| `RealRuinsPOIComp.poiType` | 4 new types appended | Ordinals are unchanged. Old POIs keep their old classification. They aren't reclassified, and the tech cap doesn't rename them. |
| `RealRuinsPOIWorldObject.wealthOnEnter` | Wealth is now market value with contents (was ingredient cost) | Only matters if the save was made **while a POI map was open**. The comparison mixes units, and the new value is usually higher, so the owner is restored more often. Harmless. |
| `CaravanArrivalAction_VisitAbandonedBase` / `_VisitRealRuinsPOI` | Now save `mapParent` and have a parameterless constructor | ⚠ **A caravan travelling to such a site in an old save:** at baseline this already failed to load the action (no constructor). Now it loads with a null target and throws NRE in the inspector and on arrival. **R4, test M-05.** |
| `TransportPodsArrivalAction_VisitRuins` / `_VisitRuinsPOI` | Moved into a base class with the same Scribe keys | Safe. |
| Animal inhabitants | New `LordJob_AnimalInhabitants` | Animals already on old maps keep the vanilla `LordJob_DefendPoint` (old behavior, they never go wild). |
| `Quest_PristineRuins` incident | `baseChance 0`, the worker only sends a letter | The def still exists, so the storyteller history resolves. Abandoned bases it created keep working. |
| `RealRuins_AbandonedBase` quest | Weight 1.5, `affectedByPoints=false`, new gates | Quests already active are unaffected. |
| GenStep order `ScatterRealRuins` 301 → 760 | Map generation only | Only new maps. |
