#!/bin/bash

# Script to decompile RimWorld assemblies for reference
# This creates read-only decompiled source code for debugging and understanding RimWorld's implementation

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DLL_CACHE="$PROJECT_ROOT/DLL_Cache"
DECOMPILED_DIR="$PROJECT_ROOT/DecompiledSources"

# Ensure ilspycmd is in PATH
export PATH="$PATH:$HOME/.dotnet/tools"

# Check if ilspycmd is available
if ! command -v ilspycmd &> /dev/null; then
    echo "Error: ilspycmd not found. Installing..."
    dotnet tool install -g ilspycmd --version 7.2.1.6856
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

echo "Decompiling RimWorld assemblies..."
echo "Source: $DLL_CACHE"
echo "Destination: $DECOMPILED_DIR"
echo ""

# Create output directories
mkdir -p "$DECOMPILED_DIR/Assembly-CSharp"
mkdir -p "$DECOMPILED_DIR/Assembly-CSharp-firstpass"

# Decompile Assembly-CSharp.dll (main RimWorld game code)
if [ -f "$DLL_CACHE/Assembly-CSharp.dll" ]; then
    echo "Decompiling Assembly-CSharp.dll..."
    ilspycmd -p -o "$DECOMPILED_DIR/Assembly-CSharp" "$DLL_CACHE/Assembly-CSharp.dll"
    echo "✓ Assembly-CSharp.dll decompiled"
else
    echo "⚠ Warning: Assembly-CSharp.dll not found in $DLL_CACHE"
fi

# Decompile Assembly-CSharp-firstpass.dll
if [ -f "$DLL_CACHE/Assembly-CSharp-firstpass.dll" ]; then
    echo "Decompiling Assembly-CSharp-firstpass.dll..."
    ilspycmd -p -o "$DECOMPILED_DIR/Assembly-CSharp-firstpass" "$DLL_CACHE/Assembly-CSharp-firstpass.dll"
    echo "✓ Assembly-CSharp-firstpass.dll decompiled"
else
    echo "⚠ Warning: Assembly-CSharp-firstpass.dll not found in $DLL_CACHE"
fi

# Make all decompiled files read-only to prevent accidental modification
echo ""
echo "Making decompiled sources read-only..."
find "$DECOMPILED_DIR" -type f -name "*.cs" -exec chmod 444 {} \;
echo "✓ All decompiled sources are now read-only"

# Update version info in README
if [ -f "$DLL_CACHE/Assembly-CSharp.dll" ]; then
    DECOMPILE_DATE=$(date +"%Y-%m-%d %H:%M:%S")
    # Try to get RimWorld version from DLL if possible (this is a placeholder)
    GAME_VERSION="Unknown (check RimWorld installation)"
    
    # Update README with decompilation info
    sed -i.bak "s/\*\*Game Version\*\*: \[To be filled after decompilation\]/**Game Version**: $GAME_VERSION/" "$DECOMPILED_DIR/README.md"
    sed -i.bak "s/\*\*Decompiled Date\*\*: \[To be filled after decompilation\]/**Decompiled Date**: $DECOMPILE_DATE/" "$DECOMPILED_DIR/README.md"
    rm -f "$DECOMPILED_DIR/README.md.bak"
fi

echo ""
echo "✓ Decompilation complete!"
echo "  Decompiled sources are in: $DECOMPILED_DIR"
echo "  Remember: These files are READ-ONLY reference code only!"
