# RealRuins — Fixes Queue

Single list of known bugs, code smells and design weaknesses. Entries are grouped by severity and subsystem.

## How to maintain this file

- Each section has its own numbering that starts from 1 and a severity prefix: **C** critical, **H** high, **M** medium, **L** low, **U** unverified.
- IDs are never reused or renumbered. Add new items at the end of the matching section with the next free number.
- When an item is fixed, delete its description and leave only the heading, e.g. `### H3. Fixed`. This keeps the other IDs stable and the file short.
- When an item turns out not to be a real issue, do the same: delete its description and leave only the heading, e.g. `### L2. Not an issue`. Put the explanation in a short code comment at the place that triggered the report, not in this file and not at every related call site. The comment stops future reviews from reporting the same false positive.
- Remove the `Fixed` and `Not an issue` stubs only when the user explicitly asks for a cleanup.
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

### M4. Fixed

---

### M5. Fixed

---

### M6. Moved
Not a bug: this is how POI wealth tracking works now, and it needs a rework. See [IMPROVEMENTS_AND_FEATURES.md](IMPROVEMENTS_AND_FEATURES.md), item M.

---

### M7. Won't fix
Intended: same as vanilla `SettlementUtility.AffectRelationsOnAttacked`, see the comment in `POIArrivalUtility`.

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

### M15. Snapshot cache size scan races with file deletion
**File:** `Utility/SnapshotStoreManager.cs` — `RecalculateFilesSize()`, `CheckCacheSizeLimits()`

`RecalculateFilesSize()` lists the cache root and then reads each file's `FileInfo.Length`. It runs on the download thread after every `WriteSnapshotFile`. At the same time the main thread can delete root files: `CheckCacheSizeLimits()` (startup, startup hook, settings save), `BlueprintFinder` removing bad blueprints, `CheckCacheContents()` and `ClearCache()`. If a file is deleted between the listing and the size read, the call throws `FileNotFoundException`.

User report on the 1.7 release (`1eb38f4`): the game crashed on load with `FileNotFoundException` at `FileInfo.get_Length()` <- `RecalculateFilesSize()` <- `StoreBinaryData` thread. The race is older than the port. 1.7 runs on .NET 10, where an unhandled exception on any thread ends the process, while 1.6's Mono only logged it.

The H3 guard stops the crash, but two problems remain:
- The file is already written, yet the exception makes `onStored(false)` fire, so `SnapshotManager` counts a good download as failed and the progress/completion counts are off.
- `CheckCacheSizeLimits()` has the same list-then-stat loop (plus `File.Delete`) and no guard.

**Fix:** in both loops, skip a file that disappears (catch `FileNotFoundException` for each file). Move `RecalculateFilesSize()` out of the write's success path so that a failed size count cannot mark a good write as failed.

---

## LOW — Code quality, performance, maintainability

### L1. Not an issue

---

### L2. Not an issue

---

### L3. Fixed

---

### L4. Fixed

---

### L5. Fixed

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
