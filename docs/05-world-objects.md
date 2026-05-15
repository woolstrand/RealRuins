# World Objects, Incidents & Arrival Actions

> **AI AGENT NOTE**: If you add new world object types, change how ruins are discovered, modify the AbandonedBase lifecycle, or change arrival action behavior, update this file.

## Overview

Ruins appear on the world map as two types of objects:
1. **Incident-based** (`AbandonedBaseWorldObject`, `SmallRuinsWorldObject`) — spawned by incident workers, can be discovered by caravans
2. **POI-based** (`RealRuinsPOIWorldObject`) — pre-placed during planetary ruins setup, persistent on the world map

**Files:**
- `Source/Classes/Incidents/AbandonedBaseWorldObject.cs`
- `Source/Classes/Incidents/SmallRuinsWorldObject.cs`
- `Source/Classes/Incidents/IncidentWorker_RuinsFound.cs`
- `Source/Classes/Incidents/IncidentWorker_CaravanFoundRuins.cs`
- `Source/Classes/Incidents/CaravanArrivalAction_VisitAbandonedBase.cs`
- `Source/Classes/Incidents/TransportPodsArrivalAction_VisitRuins.cs`
- `Source/Classes/DynamicMapObjects/RealRuinsPOIWorldObject.cs`
- `Source/Classes/DynamicMapObjects/CaravanArrivalAction_VisitRealRuinsPOI.cs`
- `Source/Classes/DynamicMapObjects/TransportPodsArrivalAction_VisitRuinsPOI.cs`
- `Source/Classes/Components/RuinedBaseComp.cs`
- `Source/Classes/Components/RealRuinsPOIComp.cs`
- `Source/Classes/Components/FormCaravanFromRuinsComp.cs`
- `Source/Classes/Triggers/RaidTrigger.cs`
- `Source/Classes/Triggers/TrippingTrigger.cs`
- `Source/Classes/Thoughts/ThoughtWorker_ScavengingRuins.cs`

---

## AbandonedBaseWorldObject

Extends `MapParent`. Represents a large ruins site on the world map.

**Visual**: `Material` computed from faction color. Cached in `cachedMat`; reset on faction change.

**`GetFloatMenuOptions()`**: Delegates to `CaravanArrivalAction_VisitAbandonedBase.GetFloatMenuOptions()`.

**`GetTransportersFloatMenuOptions()`**: Delegates to `TransportPodsArrivalAction_VisitRuins`.

**`ShouldRemoveMapNow()`**: Removes map and world object together when no pawn is blocking removal. Sends `successSignal` only if non-null and non-empty.

**Signal safety**: Both signals (`successSignal`, `expireSignal`) are NullOrEmpty-guarded before firing. This prevents accidentally matching global RimWorld signals. A code comment explicitly documents why this guard is necessary (ghost "are leaving" message bug).

**Dev gizmos**: Three debug buttons (gated behind `Prefs.DevMode`) to test signal edge cases.

---

## SmallRuinsWorldObject

Minimal `MapParent` for small caravan-found ruins. Removes map and world object together when no pawn blocks. Only custom logic is `GetInspectString()` returning a localized description.

---

## IncidentWorker_RuinsFound

Fires a `StandardLetter` and spawns an `AbandonedBaseWorldObject` on the world map. The code comment notes it was once removed and then "RESTORED."

**`CanFireNowSub()`**: Requires `enableAbandonedRuinsFoundEvent`, `CanFireLargeEvent()` (≥250 stored blueprints), and existence of a non-hostile faction.

**`TryExecuteWorker()`**:
1. Selects (or falls back to) a random non-hostile faction
2. Finds tile 5–30 world-tiles away
3. Creates `AbandonedBaseWorldObject` — sets faction to `null` on world object (faction used only for letter text)
4. Calls `BlueprintFinder.FindRandomBlueprintWithParameters()` (min area 6400, min density 1%, max 50 attempts)
5. Sets `RuinedBaseComp.blueprintFileName` and calls `StartScavenging(capped_cost)`
6. Computes letter lifetime: `Math.Pow(cost/1000, 0.41) × 1.1` days (power curve)
7. Sends letter referencing faction leader

---

## IncidentWorker_CaravanFoundRuins

Fires when a caravan discovers small ruins nearby.

**`CanFireNowSub()`**: Checks `enableCaravanFoundRuinsEvent`, `CanFireMediumEvent()` (≥30 blueprints), and caravan incident viability.

**`TryExecuteWorker()`**:
- If target is a `Map`: delegates to vanilla `TravelerGroup` incident
- For caravans: shows `Dialog_NodeTree` with "Investigate" / "Move On"
- "Investigate": queues `LongEventHandler` to generate a 120×120 map using `SmallRuinsWorldObject`, then enters caravan at edge

**Dead code**: `TryFindEntryCell()` is defined but never called.

---

## CaravanArrivalAction_VisitAbandonedBase

Handles caravan arrival at `AbandonedBaseWorldObject`.

- `Arrived()`: queues `LongEventHandler` if no map exists, then calls `DoEnter()`
- `DoEnter()`: generates/gets map, sends `ThreatBig` letter, enters caravan drafted if faction is hostile
- `CanEnter()`: always returns `true` — no cooldown or condition check
- Uses `CaravanArrivalActionUtility.GetFloatMenuOptions()` with lambda for action factory

---

## TransportPodsArrivalAction_VisitRuins

Transport pod arrival at ruins (any non-POI ruins site).

- `GeneratesMap = true`
- `ShouldUseLongEvent()` = true only if no map exists
- `Arrived()`: gets/generates map, sends `ThreatBig` letter, notifies hostile map
- `AffectRelationsIfNeeded()`: uses faction-context letter selection (contrast: this version hardcodes `ThreatBig`)

---

## RealRuinsPOIWorldObject

The richest world object. Handles icon rendering, faction dynamics, wealth tracking, and map lifecycle for persistent POI ruins.

### Visual System
- `poiType` drives icon texture: `poi-{type}` for small icon; expanding icon also keyed by type
- `Material` computed from faction color; cached in `cachedMat`, reset on faction change

### `PostMapGenerate()`
Captures `wealthOnEnter` by summing `ThingComponentsMarketCost × stackCount` for all map things. Stores `originalFaction`.

### `Tick()`
When the map is loaded and `!AnyHostileActiveThreatToPlayer`: transfers ownership to `Faction.OfPlayer`. Note: runs every tick — safe because `SetFaction` is idempotent, but potentially wasteful.

### `ShouldRemoveMapNow()` — Faction Restoration Logic
Computes `ratio = (wealthOnEnter - currentWealth) / blueprintCost`:
- `ratio < 0.1`: Restore faction (player did minimal damage → location "reclaims")
- `0.1 ≤ ratio < 0.3`: 30% abandon, 70% restore
- `ratio ≥ 0.3`: Destroy world object

Cooldown = `max(4, difference / 2000)` days.

**Wealth measurement**: Uses `ThingComponentsMarketCost × stackCount` for **all** map things — intentionally different from vanilla `WealthWatcher` (which counts only player-owned things).

### Gizmos
Shows `SettleCommand` when map is active and world selector shows this object, allowing the player to settle at the POI.

### Known Issues
- `wealthOnEnter` initialized to 1 (non-zero to avoid division-by-zero); `blueprintCost` also defaults to 1 — if comp is null, all damage ratios are inflated
- `CurrentMapWealth()` and `RuinedBaseComp.GetTotalMapWealth()` use different calculation methods (see **RuinedBaseComp** below)

---

## CaravanArrivalAction_VisitRealRuinsPOI

Key differences from AbandonedBase version:
- Label is context-sensitive: "Enter" for null/player faction, "Attack" for others
- `DoArrivalAction()`: sends neutral or `ThreatBig` letter depending on faction; drafts colonists only if hostile
- `CanEnter()`: checks `EnterCooldownComp` — POI can be on cooldown after previous visit
- `AffectRelationsIfNeeded()`: sets goodwill to `GoodwillToMakeHostile` (forces war) silently at caravan arrival

**Issue**: The goodwill drop to hostile fires before the letter is sent. The player has no advance warning that visiting will trigger war.

---

## TransportPodsArrivalAction_VisitRuinsPOI

- `GeneratesMap = false` (map already exists or will be generated by landing)
- Letter type is faction-aware
- `AffectRelationsIfNeeded()`: same goodwill-to-hostile pattern

**Code duplication**: ~70% of this class is identical to `TransportPodsArrivalAction_VisitRuins`. Candidates for a shared base class.

---

## RuinedBaseComp

The most complex component. Implements the full lifecycle state machine for `AbandonedBaseWorldObject`.

### State Machine
```
Inactive → WaitingForArrival → FightingWaves → WaitingForEnemiesToBeDefeated
→ WaitingTimeoutAfterEnemiesDefeat → WaitingToBeInformed → InformedWaitingForLeaving
→ ScavengedCompletely
```

### Off-Map Scavenging (`CompTick()` when no map)
- `raidersActivity` grows stochastically each tick (`Rand.Range(-0.1, 0.25) × capCost/100000`)
- Marauder raids trigger on activity threshold or random chance, reducing `currentCapCost`
- When `currentCapCost < 20000` (small chance) or ≤ 100: transitions to `ScavengedCompletely`
- Creates emergent time pressure: unvisited ruins are eventually looted by NPCs

### `PostMapGenerate()`
Overrides `currentCapCost` with `max(actual_map_wealth, 150000)` to prevent underscaled forces. Sets `destructionLevel = actualCost / capCost`.

### `DoRareTask()` (every 60 ticks)
- V2 forces: if hostile count < `15 × destructionLevel × forceMultiplier` AND total < 90: triggers small patrol raid (500–2500 pts)
- Reform type 2: drives state machine through `FightingWaves → ... → InformedWaitingForLeaving`

### `CheckTriggers()`
Iterates `triggersCache` (pre-built list of `RaidTrigger` things). When all triggers have fired, transitions to `WaitingForEnemiesToBeDefeated` after `unlockHysteresisTimeout` (1000 ticks) safety delay.

### `TriggerRaid()`
Fires `RaidEnemy` incident directly via `IncidentDef.Worker.TryExecute()` with `FactionSelector.GetRandomFaction()`.

### `GetInspectString()`
Wealth and activity described in 5 tiers each (threshold arrays). Only shown when `WaitingForArrival`.

### Wealth Calculation
`GetTotalMapWealth()` counts only `CountAsResource` things — differs from `RealRuinsPOIWorldObject.CurrentMapWealth()` which counts all things with market cost. The two systems produce different numeric values for the same map.

### Known Issues
- V1 faction-transfer code (`caravanReformType == 1` path) runs every tick alongside V2 system — redundant/conflicting ownership logic
- `mapExitLocked` property exposed but never called from within this class (external use only)

---

## RealRuinsPOIComp

`WorldObjectComp` storing static metadata for a POI.

**Persisted fields**: `blueprintName`, `gameName`, `originX`, `originZ`, `poiType` (as int), `militaryPower`, `approximateSnapshotCost`, `bedsCount`, `mannableCount`.

**Note**: `POIType` enum is declared at global scope (no namespace) in this file — pollutes the global namespace.

---

## FormCaravanFromRuinsComp

Extends vanilla `FormCaravanComp`. Overrides `CanFormOrReformCaravanNow`:
- `caravanReformType == 1` (instant reform): blocks if there are active hostiles or no free colonists
- Other modes: always `true`

**`new` shadow**: Uses `new` keyword (not `override`) to hide the base property. Callers with a `FormCaravanComp` reference would call the base. Correctness depends on concrete type usage.

---

## RaidTrigger

A `Thing` placed on the map. After a countdown (`|Rand.Gaussian(0, 100)|` ticks, half-normal), fires a `RaidEnemy` incident and destroys itself.

**`IAttackTarget`** implementation is vestigial — `TargetPriorityFactor = 0` and `ThreatDisabled = true`, so it's invisible to AI targeting. Comment notes the "step-on-trigger" mechanic was abandoned.

**`Tick()` override** calls manual 250-tick gating before delegating to `TickRare()` — redundant (the 250-tick pattern duplicates what `TickRare` does automatically). But is there TickRare for trigger? It's not always available.

**`ExposeData()`**: Persists `faction`, `value`, `ticksLeft` — allows save/load to resume countdowns.

**Issue**: All triggers placed simultaneously fire in a tight cluster (all initialized with similar random values from the same distribution).

---

## TrippingTrigger

A `Thing` placed on the map. Deals 1–10 blunt damage (70% chance) to any pawn stepping on it, then destroys itself. Runs every tick — potentially expensive at scale (O(triggers × thingListSize) per tick).
Is it used? Check for usage and report.

---

## ThoughtWorker_ScavengingRuins

Generates mood thoughts based on `RuinedBaseComp.currentCapCost`. Five stages:
- Stage 0 (< 10,000): negative mood
- Stage 1 (10,000–25,000): mild positive
- Stage 2 (25,000–100,000): moderate positive
- Stage 3 (100,000–500,000): strong positive
- Stage 4 (> 1,000,000): extreme positive

**Gap bug**: Values between 10,000 and 25,000 produce `ThoughtState.Inactive` (neither stage 0 nor stage 1). May be intentional or an off-by-one error.

---

## XML Def Registration

- `1.6/Defs/ThingDefs_Triggers/` — `RaidTrigger` and `TrippingTrigger` ThingDefs
- `1.6/Defs/Thoughts/` — `ThoughtDef` for scavenging ruins
- `1.6/Defs/Storyteller/` — storyteller settings for incident pacing
- `1.6/Patches/WorldObjects_Patch.xml` — patches vanilla world object defs to add components
