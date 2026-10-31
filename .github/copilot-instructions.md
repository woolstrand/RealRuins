# RealRuins — Copilot Instructions

## What This Project Is

RealRuins is a RimWorld mod (C#, .NET 4.7.2, Harmony patching) that:
1. Periodically uploads the player's base as an XML "blueprint" to a central server
2. Downloads blueprints from other players and places them as ruins on newly generated maps
3. Creates persistent Points of Interest (POIs) on the world map from other players' bases

**Game Version**: 1.7 (current build target). Source in `Source/Classes/`. Output: `1.7/Assemblies/RealRuins.dll`.

## Knowledge Base

Detailed documentation is in `docs/`. Read the relevant file before making changes to a subsystem:

| File | Covers |
|------|--------|
| [docs/01-architecture-overview.md](docs/01-architecture-overview.md) | Full system map, data flows, namespace layout, cross-cutting concerns |
| [docs/02-snapshotting-upload.md](docs/02-snapshotting-upload.md) | Blueprint capture (SnapshotGenerator), upload/download (SnapshotManager, APIService), local cache (SnapshotStoreManager), compression |
| [docs/03-blueprint-pipeline.md](docs/03-blueprint-pipeline.md) | Blueprint data model, XML loading, preprocessing, deterioration, scavenging, map placement (BlueprintTransferUtility), pawn restoration, defender forces |
| [docs/04-scattering.md](docs/04-scattering.md) | ScatterOptions, RuinsScatterer, GenStep_ScatterRealRuins, GenStep_ScatterPOIRuins, proximity system |
| [docs/05-world-objects.md](docs/05-world-objects.md) | AbandonedBaseWorldObject, SmallRuinsWorldObject, RealRuinsPOIWorldObject, incidents, caravan/pod arrival actions, RuinedBaseComp state machine, triggers, thoughts |
| [docs/06-planetary-ruins.md](docs/06-planetary-ruins.md) | Pre-game wizard (Page_PlanetaryRuinsLoader), blueprint classification (BlueprintAnalyzer), POI factory (RealRuinsPOIFactory), blueprint pool management |
| [docs/07-settings-ui.md](docs/07-settings-ui.md) | Settings pages, RealRuins_ModSettings, import/export serialization — including known critical bugs |
| [docs/08-utility-debug.md](docs/08-utility-debug.md) | Extensions, CoroutineManager, FactionSelector, SimpleJSON, Debug logging system, startup sequence |
| [docs/FIXES_QUEUE.md](docs/FIXES_QUEUE.md) | Single list of known bugs, weak points and cleanups (IDs like C1, H2, M3, L4, U5), with maintenance rules |

## Known Bugs and Fixes Queue

Before changing a subsystem, check [docs/FIXES_QUEUE.md](docs/FIXES_QUEUE.md) for open items in that area. When you find a new bug or weak point, add it there. When you fix one, replace its description with a `Fixed` stub as described at the top of that file.

## Build

Never build unless explicitly asked. Build target: `Source/RealRuins.csproj` → `1.7/Assemblies/RealRuins.dll`.

## Important Rules

- All settings fields in `RealRuins_ModSettings` are `public static` — no instance needed
- `ScatterOptions.Default` is a mutable singleton — use `Copy()` before modifying
- `PlanetaryRuinsInitData.shared` is valid only during new-game initialization flow
- Harmony patches live in `RealRuins.cs` as nested inner classes — one class per patch
- When adding a new source file, add it to `Source/RealRuins.csproj` (the csproj uses `<Compile Include="Classes\**\*.cs" />` glob — new files in subdirectories are picked up automatically)
- Never change `DecompiledSources/`, it is the original game code which is a read-only reference only; use Harmony patches to modify behavior
