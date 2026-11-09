# Error-log popups during the playtest (2026-11-09, builds 8709136, 4d3ad0b)

| # | When / action | Errors | Caused by RR? | Verified without mod |
|---|---|---|---|---|
| 1 | 12:28, Dev quicktest right after the map was generated (RT-01a) | `Could not execute post-long-event action … InvalidOperationException: Object is not tracked by the Unity world` in `SampleSustainer.TryMakeAndPlay` ← `SubSustainer.StartSample` (a sound sustainer started at the end of map generation) | No RR frames. Unknown which thing started the sustainer (could be a ruin object) | pending |
| 2 | 12:51, loading old save `PT-old-S3-auto` (RT-02) | 5× `<name> had a beard. Removed.` (vanilla `Pawn_StyleTracker` post-load check) | **Yes, old build data**: pawns restored by the old build were generated male and switched to female. Fixed for new pawns in `8709136`; vanilla removes the beards on load, the re-saved game loads clean | n/a: the data is in an old-build save; cause confirmed from the code |
| 3 | 12:55, loading old save `PT-old-S5-poi` (RT-02) | 23× `Hediff_Injury has null part after loading`, 1× `Hediff_MissingPart has null part after loading`, 12× `had a beard. Removed.` | **Yes, old build data**: hediffs of pawns restored by the old build (part not resolved; the new build validates parts, `ValidateAndFixPartBody`) and beards as in #2. Vanilla drops them on load; the re-saved game loads clean | n/a: data in an old-build save |

The debug log opens by itself on every `Log.Error`. Each popup is listed here; ones not clearly caused by RealRuins data must be repeated **without the mod** (Harmony + Core + DLCs only) to rule out an indirect side effect.

Popup #1, to verify: Dev quicktest without RealRuins, several times. If the same `SampleSustainer` / "not tracked by the Unity world" error shows up, it is the game. If not, repeat with RealRuins and the ruins of that map to find the thing that starts the sound.
