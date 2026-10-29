---
name: run-realruins
description: Build the RealRuins mod, run/launch RimWorld with it on macOS, drive the game by hand (click, type, dev-mode debug actions), take screenshots and read Player.log. Use to test or verify RealRuins changes in-game - ruins on a new map, ruins quests, entering ruins with a caravan.
---

# Run RealRuins in RimWorld (macOS)

RealRuins is a RimWorld mod. Gameplay is too variable for scripted e2e tests, so testing is
**interactive**: the driver only launches the game, sends input, takes screenshots and reads the
log - **you** look at each screenshot, decide the next click, and judge whether the mod behaved.
Expect every session to play out differently (map layout, letters, window position).

Paths are relative to the repo root. Driver: `.claude/skills/run-realruins/driver.sh`.

**Before testing, read [NOTES.md](NOTES.md)** - how to reach things in the game, what the mod's
log lines mean, and traps already found. **After testing, add to it** anything non-obvious you had
to figure out (where a menu lives, which incident to fire, what a log line means, what misled you).
Record knowledge, not coordinates - coordinates change every launch.

## Prerequisites

- Steam running; RimWorld at `~/Library/Application Support/Steam/steamapps/common/RimWorld`;
  `woolstrand.realruins` active; Dev mode on (`<devMode>True</devMode>` in
  `~/Library/Application Support/RimWorld/Config/Prefs.xml`).
- `swiftc` (Xcode CLT) and Mono `msbuild`.
- The terminal running Claude has **Screen Recording** and **Accessibility** permissions.

## Build

```bash
.claude/skills/run-realruins/driver.sh build
```

Writes `1.7/Assemblies/RealRuins.dll`; `postbuild.sh` copies the mod into the game's
`Mods/RealRuins`. The game loads the DLL only at startup - rebuild with the game closed (or relaunch).

## Drive

```bash
D=.claude/skills/run-realruins/driver.sh
$D launch                 # opens RimWorld.app, returns once RealRuins has loaded
$D ss menu                # screenshot -> $TMPDIR/realruins-run/menu.png ; Read it
$D click X Y              # X Y = pixel coords in the latest screenshot (ss downscales to points)
M=$($D logmark)           # mark the log before an action...
$D logsince $M RealRuins  # ...then read what the mod logged (stack-trace lines stripped)
$D errors $M              # ERROR / Exception / "Failed to" lines since the mark, counted
$D quit
```

Also: `rclick X Y`, `move X Y`, `type TEXT`, `esc`, `key CODE [cmd|shift|ctrl|alt]`,
`scroll DY [N]`, `zoomout`, `waitlog REGEX MARK SECS`, `status`, `log` (prints log path).
No args prints the list.

Loop: `ss` → Read the image → one or a few inputs → `ss` again. Check the log after anything
that triggers mod code; it is the ground truth, the screenshot is for navigation and a visual check.
