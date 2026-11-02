---
name: run-realruins
description: Build the RealRuins mod, run/launch RimWorld with it on macOS or Windows, drive the game by hand (click, type, dev-mode debug actions), take screenshots and read Player.log. Use to test or verify RealRuins changes in-game - ruins on a new map, ruins quests, entering ruins with a caravan.
---

# Run RealRuins in RimWorld (macOS / Windows)

RealRuins is a RimWorld mod. Gameplay is too variable for scripted e2e tests, so testing is
**interactive**: the driver only launches the game, sends input, takes screenshots and reads the
log - **you** look at each screenshot, decide the next click, and judge whether the mod behaved.
Expect every session to play out differently (map layout, letters, window position).

Paths are relative to the repo root. Driver: `.claude/skills/run-realruins/driver.sh` - one bash entry
point with the same commands on both platforms. Platform specifics live in `platform/`:
`mac.sh` + `mac-input.swift` (CGEvent), `win.sh` + `win-input.cs` (Win32). On Windows run it from
**Git Bash** (the Bash tool), not PowerShell. `driver.sh where` prints the resolved game/log paths.

**Before testing, read [NOTES.md](NOTES.md)** - how to reach things in the game, what the mod's
log lines mean, and traps already found. **After testing, add to it** anything non-obvious you had
to figure out (where a menu lives, which incident to fire, what a log line means, what misled you).
Record knowledge, not coordinates - coordinates change every launch.

## Prerequisites

Both: Steam running; RimWorld 1.7 installed via Steam; **Harmony** (`brrainz.harmony`) and
`woolstrand.realruins` active, Harmony first; Dev mode on (`<devMode>True</devMode>` in
`Config/Prefs.xml`).

**macOS**
- Game: `~/Library/Application Support/Steam/steamapps/common/RimWorld` (override: `RR_GAME_DIR`).
- Config/log: `~/Library/Application Support/Ludeon Studios/RimWorld by Ludeon Studios/` (`Config/`, `Logs/Player.log`).
- `swiftc` (Xcode CLT) and Mono `msbuild`.
- The terminal running Claude has **Screen Recording** and **Accessibility** permissions.

**Windows**
- Game: found via Steam's `libraryfolders.vdf` (override: `RR_GAME_DIR`). 1.7 runs `RimWorld.exe`.
- Config/log: `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\` (`Config\`, `Logs\Player.log`).
- **VS 2022 Build Tools** with the .NET desktop build workload and the .NET Framework 4.7.2 targeting pack:
  `winget install Microsoft.VisualStudio.2022.BuildTools --override "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --add Microsoft.Net.Component.4.7.2.TargetingPack --includeRecommended"`.
  The input helper is compiled with the .NET Framework `csc.exe` that ships with Windows.
- `postbuildwin.bat` copies into a **hard-coded** `F:\SteamLibrary\...\Mods\RealRuins` - edit it if the
  game lives elsewhere.

## Build

```bash
.claude/skills/run-realruins/driver.sh build
```

Writes `1.7/Assemblies/RealRuins.dll`; the postbuild script (`postbuild.sh` / `postbuildwin.bat`) copies
the mod into the game's `Mods/RealRuins`. If the installed game is a different version than the build
folder, the driver also copies the DLL into that version's folder of the deployed mod (says so).
The game loads the DLL only at startup - rebuild with the game closed (or relaunch).

## Drive

```bash
D=.claude/skills/run-realruins/driver.sh
$D launch                 # starts the game, returns once RealRuins has loaded
$D ss menu                # screenshot -> $TMPDIR/realruins-run/menu.png ; Read it
$D click X Y              # X Y = pixel coords in the latest screenshot
M=$($D logmark)           # mark the log before an action...
$D logsince $M RealRuins  # ...then read what the mod logged (stack-trace lines stripped)
$D errors $M              # ERROR / Exception / "Failed to" lines since the mark, counted
$D quit
```

Also: `rclick X Y`, `move X Y`, `type TEXT`, `esc`, `key KEY [cmd|shift|ctrl|alt]` (named keys like
`enter`/`space`/arrows, or a raw macOS keycode / Windows VK code), `scroll DY [N]`, `zoomout`,
`waitlog REGEX MARK SECS`, `status`, `log`, `where`. No args prints the list.

Screenshot coordinates: on macOS `ss` captures the whole main screen downscaled to points; on Windows
it captures only the **game window's client area** (multi-monitor safe), downscaled to at most 1600 px
wide (`RR_SS_MAXW`), and clicks are mapped back. Either way: read coords off the image, pass them as-is.

Loop: `ss` → Read the image → one or a few inputs → `ss` again. Check the log after anything
that triggers mod code; it is the ground truth, the screenshot is for navigation and a visual check.

`launch` can take minutes. Run it with the Bash tool's `run_in_background` (or under `timeout`) so a hang
can't block you, and check `logsince 1 RealRuins` if it seems stuck.
