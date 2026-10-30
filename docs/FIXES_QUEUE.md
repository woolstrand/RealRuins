# RealRuins — Fixes Queue

Single list of known bugs, code smells and design weaknesses. Entries are grouped by severity and subsystem.

## How to maintain this file

- Each section has its own numbering that starts from 1 and a severity prefix: **C** critical, **H** high, **M** medium, **L** low, **U** unverified.
- IDs are never reused or renumbered. Add new items at the end of the matching section with the next free number.
- When an item is fixed, delete its description and leave only the heading, e.g. `### H3. Fixed`. This keeps the other IDs stable and the file short.
- Remove the `Fixed` stubs only when the user explicitly asks for a cleanup.
- An unverified item (U) should be confirmed or rejected against the code before work starts. Once confirmed, move it to the right severity section with a new ID.
- Items marked **Note** need developer input or extra analysis before changing code.

---

## CRITICAL — Data loss or incorrect behavior

### C1. Fixed

---

### C2. Fixed

---

## HIGH — Logic errors and stability risks

### H1. Fixed

---

### H2. Fixed

---

### H3. Fixed

---

### H4. Fixed

---

### H5. Fixed

---

### H6. Fixed

---

### H7. Fixed

---

### H8. Fixed

---

### H9. Fixed

---

## MEDIUM — Correctness and reliability

### M1. Fixed

---

### M2. Fixed

---

### M3. Fixed

---

### M4. Won't fix
Intended randomness, see the comment in `BlueprintAnalyzer.supposedType()`.

---

### M5. Fixed

---

### M6. Moved
Not a bug: this is how POI wealth tracking works now, and it needs a rework. See [IMPROVEMENTS_AND_FEATURES.md](IMPROVEMENTS_AND_FEATURES.md), item M.

---

### M7. Won't fix
Intended: same as vanilla `SettlementUtility.AffectRelationsOnAttacked`, see the comment in `CaravanArrivalAction_VisitRealRuinsPOI`.

---

### M8. Fixed

---

### M9. Fixed

---

### M10. Fixed

---

### M11. Fixed

---

### M12. Fixed

---

### M13. Fixed

---

### M14. Fixed

---

## LOW — Code quality, performance, maintainability

### L1. `RemoveWall()` O(w×h) Per Call Inside O(w×h) Loops
**File**: `Source/Classes/Scattering/Internal/Blueprint.cs`, used by `DeteriorationProcessor` and `ScavengingProcessor`
**What**: `RemoveWall()` scans the whole map to merge room numbers and is called for every removed wall tile inside loops that iterate all cells.
**Impact**: O(w²×h²) worst case for dense ruins; about 8.1 billion iterations on a 300×300 blueprint with heavy deterioration.
**Fix**: Keep a reverse index (room ID to list of cells) for O(room_size) merges, or defer room renumbering until after the full pass.

---

### L2. Multiple `DefDatabase` Lookups for the Same Def Across Pipeline Stages
**What**: The same `defName` is resolved independently in `BlueprintLoader`, `BlueprintPreprocessor` and `BlueprintTransferUtility`.
**Impact**: Triple or more lookup overhead per item per run.
**Fix**: Cache the resolved `ThingDef` on `ItemTile` after the first lookup in `BlueprintLoader`.

---

### L3. Fixed

---

### L4. `GenerateForces()` Code Duplication in Defender Generators
**File**: `CitizenForcesGeneration.cs` and `MilitaryForcesGenerator.cs`
**What**: `GenerateForces()` is near-identical in both classes; only `GenerateStartingParty()` differs. `BattleRoyaleForcesGenerator` has a similar trigger budget loop.
**Fix**: Move the common loop into `AbstractDefenderForcesGenerator`, with customizable parameters as virtual properties.

---

### L5. Code Duplication Across Arrival Actions
**What**: `TransportPodsArrivalAction_VisitRuins` / `_VisitRuinsPOI` and `CaravanArrivalAction_VisitAbandonedBase` / `_VisitRealRuinsPOI` share about 60–70% of their code.
**Fix**: Extract a shared base class or utility for the common arrival logic.

---

### L6. Fixed

---

### L7. Fixed

---

### L8. Fixed

---

### L9. Fixed

---

### L10. Fixed

---

### L11. Fixed

---

### L12. Won't fix
Failing loudly on a RimWorld rename is preferred over silent fallbacks.

---

### L13. Fixed

---

### L14. Removed
Not relevant: the postfix already returns right away unless the window is `Dialog_AdvancedGameConfig`.

---

### L15. Fixed

---

### L16. Fixed

---

### L17. Fixed

---

### L18. Fixed

---

## UNVERIFIED — Reported but not yet confirmed in code

No open items. U1–U7 were checked on 2026-10-30: U2, U3, U5 and U7 were moved to L15–L18, U6 to M14, and U1 and U4 were rejected (RimWorld forces `en-US` culture on the main thread and import cannot fail halfway; `Rand.Range` returns min when min ≥ max).
