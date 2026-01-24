# Real Ruins Project Context

## Overview
Real Ruins is a RimWorld mod that periodically uploads player bases to a server database and populates new maps with ruins from other players' abandoned bases. Players encounter procedurally-degraded versions of real bases as discoverable ruins.

**Game Version Support**: 1.0, 1.1, 1.2, 1.3, 1.4, 1.5, 1.6

## Project Structure

### Important Directories
- **Source/Classes/** - Mod source code (C#)
- **DecompiledSources/** - Read-only reference code from RimWorld. Use Harmony patches rather than modifying this.
- **DecompiledResources/** - Game strings and localization data
- **Defs/** - XML configuration files for map generators, quests, storytellers, and triggers
- **1.x/** - Version-specific mod files (Assemblies, Defs, Patches) for each RimWorld version

## Core Mod Architecture

### Main Classes
- **RealRuins_Mod** - Entry point; defines mod settings categories and UI labels
- **RealRuins_ModSettings** - Persists user preferences (offline mode, downloads/uploads, cache limits, force multipliers, logging)
- **RealRuins_Mod.cs** - Settings page UI implementation

### Key Subsystems

#### Snapshotting (Upload Pipeline)
- **SnapshotManager** - Handles map snapshot creation and upload scheduling
- **SnapshotGenerator** - Captures map state into blueprints
- **SnapshotStoreManager** - Manages local blueprint cache on disk

#### Blueprint Distribution (Download Pipeline)
- **APIService** - Server communication (upload/download blueprints)
- **BlueprintLoader** - Loads full or partial blueprints from disk
- **ScatterOptions** - Configuration for blueprint placement (position, size, partial load)

#### Blueprint Processing
- **BlueprintPreprocessor** - Initial blueprint validation/preparation
- **DeteriorationProcessor** - Simulates age/damage on blueprints
- **ScavengingProcessor** - Simulates looting (removes resources, damages items)
- **BlueprintTransferUtility** - Transfers processed blueprint to map

#### Incidents & Generation
- **Scattering/** - Handles ruin placement on generated maps
- **Incidents/** - Generates ruin discovery events
- **Quest/** - Quest system integration
- **DynamicMapObjects/** - Ruin site management
- **Triggers/** - Conditions for ruin generation

### Key Settings
- **offlineMode** - No server communication
- **allowDownloads/allowUploads** - Enable remote blueprint sync
- **diskCacheLimit** - Local storage limit (MB)
- **forceMultiplier** - Scale enemy forces on ruin maps
- **ruinsCostCap** - Maximum blueprint material value
- **preserveStandardRuins** - Add real ruins alongside vanilla ruins
- **logLevel** - 0=all, 1=warnings, 2=errors (default)

## Dependencies
- **Harmony** - For Harmony patches to modify game behavior

## Building
Never build the project unless explicitly asked to. 

## Notes
- Blueprint format supports both full map snapshots and partial regions
- Deterioration simulates realistic aging (rust, decay, item damage)
- Scavenging simulates other factions looting before player discovery
- XML patches override vanilla map generators and world object definitions
