#!/bin/bash

# Mod base folder - uses $HOME for cross-user compatibility
MOD_BASE_FOLDER="$HOME/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins"

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "Post-build: Copying mod files to RimWorld..."
echo "Target: $MOD_BASE_FOLDER"

# Create mod directory if it doesn't exist
mkdir -p "$MOD_BASE_FOLDER"

# --- common ---
echo "Copying common files..."
cp -R About "$MOD_BASE_FOLDER"
cp -R LoadFolders.xml "$MOD_BASE_FOLDER/LoadFolders.xml"
cp -R Assemblies "$MOD_BASE_FOLDER"
cp -R Defs "$MOD_BASE_FOLDER"
cp -R Languages "$MOD_BASE_FOLDER"
cp -R Patches "$MOD_BASE_FOLDER"
cp -R Textures "$MOD_BASE_FOLDER"

# --- versioned folders ---
echo "Copying versioned folders..."
cp -R 1.1 "$MOD_BASE_FOLDER"
cp -R 1.3 "$MOD_BASE_FOLDER"
cp -R 1.4 "$MOD_BASE_FOLDER"
cp -R 1.5 "$MOD_BASE_FOLDER"
cp -R 1.6 "$MOD_BASE_FOLDER"

echo "Post-build copy completed successfully!"
