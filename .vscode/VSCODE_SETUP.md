# VS Code Setup for RealRuins Development (Windows)

## Prerequisites

Before you can build and run the RealRuins mod, ensure you have the following installed:

1. **Visual Studio Code** - https://code.visualstudio.com/
2. **.NET SDK 7.0 or later** - https://dotnet.microsoft.com/en-us/download
3. **C# Dev Kit extension** - Install from VS Code Extensions (ms-dotnettools.csharp)
4. **RimWorld** - Installed on your system

## Initial Setup

### 1. Install Required VS Code Extensions

Open VS Code and install these extensions:
- **C# Dev Kit** by Microsoft (ms-dotnettools.csharp)
- **Debugger for .NET** (included with C# Dev Kit)

Or run this command in the terminal:
```bash
code --install-extension ms-dotnettools.csharp
```

### 2. Update RimWorld Mod Path

The `postbuildwin.bat` script needs to know where your RimWorld mods folder is located.

By default, it uses: `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Mods\RealRuins`

**If your RimWorld installation uses a different mod path (e.g., Steam library location), edit `postbuildwin.bat`:**
- Open `postbuildwin.bat` in VS Code
- Find the line: `set "MOD_PATH=%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Mods\RealRuins"`
- Change the path to your actual RimWorld mods directory
- Save the file

### 3. Verify .NET Installation

Open a terminal and verify .NET is installed:
```bash
dotnet --version
```

Should output version 7.0 or higher.

## Building the Project

### Option 1: Build Only
1. Press `Ctrl+Shift+B` (or go to **Terminal → Run Build Task**)
2. Select **"Build Debug (1.6)"** or **"Build Release (1.5)"**

### Option 2: Build and Copy to RimWorld
1. Open the Command Palette (`Ctrl+Shift+P`)
2. Type **"Tasks: Run Task"**
3. Select **"Build and Copy to RimWorld (Debug)"**

This will compile the code and automatically copy the mod files to your RimWorld mods folder.

### Option 3: Manual Build with Terminal
```bash
cd Source
dotnet build -c Debug
cd ..
.\postbuildwin.bat
```

## Running and Debugging

### Launch with Debugger
1. Press `F5` (or go to **Run → Start Debugging**)
2. Select **"Launch RimWorld (Debug)"**
3. This will build the project and launch RimWorld

**Note:** You may need to adjust the RimWorld executable path in `.vscode/launch.json` if it's not in the default location.

### Manual Debugging
1. Launch RimWorld normally
2. In VS Code, press `Ctrl+Shift+D` (Debug view)
3. Click **".NET Core Attach"** and select the RimWorld process

## Project Structure

- **Source/** - C# source code
  - **Classes/** - Main code classes
  - **Properties/** - Assembly info
- **1.1/, 1.3/, 1.4/, 1.5/, 1.6/** - Version-specific assemblies and definitions
- **About/** - Mod metadata (About.xml, Preview.png)
- **Defs/** - Game definitions (MapGenerators, Storyteller, etc.)
- **Languages/** - Localization files
- **Patches/** - XML patches for compatibility
- **Textures/** - Mod textures
- **.vscode/** - VS Code configuration files

## Troubleshooting

### Build Fails with "CS0006: Metadata file not found"
The project references RimWorld DLLs that may not be available. Ensure:
- RimWorld is installed properly
- Reference paths in `Source/RealRuins.csproj` are correct
- Run `dotnet restore` to fetch NuGet packages

### Copy to RimWorld Fails
- Verify the `MOD_PATH` in `postbuildwin.bat` is correct
- Ensure RimWorld is not running (it locks mod files)
- Check that you have write permissions to the mods folder
- Run VS Code as Administrator if needed

### Debugger Won't Attach
- Ensure RimWorld is running
- Check that the executable path in `.vscode/launch.json` is correct
- Make sure you built in Debug mode before launching

### Missing References in IDE
1. Open **Command Palette** (`Ctrl+Shift+P`)
2. Type "Reload Window" and press Enter
3. Wait for IntelliSense to rebuild

## Common Tasks

### Clean Build
```bash
Tasks: Run Task → "Clean Build"
```

### Just Copy Files to RimWorld (without building)
```bash
Tasks: Run Task → "Copy to RimWorld"
```

### View Build Output
- Look at the **Terminal** panel (``Ctrl+` ``)
- Or **Output** panel (`Ctrl+K Ctrl+O`)

## Converting from Visual Studio

If you were previously using Visual Studio:
- **Ctrl+B** in VS Code builds the project (instead of Ctrl+Shift+B in VS)
- **F5** launches the debugger (same as VS)
- **Terminal** replaces the Output window
- Use the **Problems** panel for build errors (like Error List in VS)

## Notes

- The post-build script copies compiled DLLs to version-specific folders (1.3, 1.4, 1.5, 1.6)
- Different build configurations output to different version folders:
  - Debug → 1.6/Assemblies/
  - Release → 1.5/Assemblies/
- The `.vscode` folder contains workspace-specific settings that are automatically applied
