#!/bin/bash

# Mod base folder - uses $HOME for cross-user compatibility
MOD_BASE_FOLDER="$HOME/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins"

# --- common ---
cp -R ../About "$MOD_BASE_FOLDER"
cp -R ../LoadFolders.xml "$MOD_BASE_FOLDER/LoadFolders.xml"
cp -R ../Assemblies "$MOD_BASE_FOLDER"
cp -R ../Defs "$MOD_BASE_FOLDER"
cp -R ../Languages "$MOD_BASE_FOLDER"
cp -R ../Patches "$MOD_BASE_FOLDER"
cp -R ../Textures "$MOD_BASE_FOLDER"

# --- versioned folders ---
cp -R ../1.1 "$MOD_BASE_FOLDER"
cp -R ../1.3 "$MOD_BASE_FOLDER"
cp -R ../1.4 "$MOD_BASE_FOLDER"
cp -R ../1.5 "$MOD_BASE_FOLDER"
cp -R ../1.6 "$MOD_BASE_FOLDER"
