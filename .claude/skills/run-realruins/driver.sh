#!/bin/bash
# Drive RimWorld (Steam install, macOS or Windows/Git Bash) for testing RealRuins.
# Usage: driver.sh <command> [args]   — run with no args for the command list.
# Platform specifics (paths, build, input, screenshots) live in platform/<mac|win>.sh.
set -euo pipefail

SKILL_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PLAT_DIR="$SKILL_DIR/platform"
REPO="$(cd "$SKILL_DIR/../../.." && pwd)"
OUT="${RR_OUT:-${TMPDIR:-${TEMP:-/tmp}}/realruins-run}"
command -v cygpath >/dev/null && OUT="$(cygpath -u "$OUT")"   # Git Bash: TMPDIR is a Windows path
mkdir -p "$OUT"

die() { echo "error: $*" >&2; exit 1; }

case "$(uname -s)" in
  Darwin) PLATFORM=mac ;;
  MINGW*|MSYS*|CYGWIN*) PLATFORM=win ;;
  *) die "unsupported platform $(uname -s)" ;;
esac
# shellcheck source=platform/mac.sh
source "$PLAT_DIR/$PLATFORM.sh"

pid() { plat_pid; }
focus() { local p; p=$(pid); [[ -n "$p" ]] || die "game not running"; plat_focus "$p"; }

# Wait until a regex appears in Player.log after line $2 (default 1), timeout $3 seconds.
wait_log() {
  local pat="$1" from="${2:-1}" timeout="${3:-120}" t=0
  while (( t < timeout )); do
    # No grep -q: it exits on the first match, tail dies of SIGPIPE and pipefail turns the match into a failure.
    if [[ -f "$LOG" ]] && tail -n +"$from" "$LOG" | grep -E "$pat" >/dev/null; then return 0; fi
    sleep 1; t=$((t+1))
  done
  die "timed out after ${timeout}s waiting for /$pat/ in Player.log"
}

loglines() { [[ -f "$LOG" ]] && wc -l < "$LOG" | tr -d ' ' || echo 0; }

# The csproj builds into one version folder (OutputPath, e.g. 1.7). If the installed game is another
# version, it loads <that version>/Assemblies instead - so also put the fresh DLL there, in the
# deployed copy under the game's Mods folder only (the repo's other version folders stay untouched).
deploy_for_game_version() {
  local built ver dll dest
  built=$(grep -o -m1 '<OutputPath>[^<]*' "$REPO/Source/RealRuins.csproj" | sed -E 's#.*\.\.[\\/]([0-9.]+)[\\/].*#\1#')
  [[ -f "$GAME_DIR/Version.txt" ]] || { echo "note: $GAME_DIR/Version.txt not found, skipping version check"; return; }
  ver=$(grep -o -E '^[0-9]+\.[0-9]+' "$GAME_DIR/Version.txt")
  [[ "$ver" == "$built" ]] && return
  dll="$REPO/$built/Assemblies/RealRuins.dll"
  dest="$GAME_DIR/Mods/RealRuins/$ver/Assemblies"
  [[ -d "$dest" ]] || { echo "note: game is $ver but no $dest folder; not copying"; return; }
  cp "$dll" "$dest/RealRuins.dll"
  echo "note: game is $ver, build targets $built - copied fresh RealRuins.dll into Mods/RealRuins/$ver/Assemblies"
}

cmd="${1:-help}"; shift || true
case "$cmd" in
  build)
    # Compiles into <OutputPath>/Assemblies and runs postbuild (copies mod into the game's Mods folder).
    cd "$REPO" && plat_build && deploy_for_game_version ;;
  launch)
    # Starts the game (Steam must be running) and blocks until the main menu is reachable.
    [[ -n "$(pid)" ]] && { echo "already running (pid $(pid))"; exit 0; }
    started=$(date +%s)
    plat_launch
    # Player.log is recreated per run (old one -> Player-prev.log). Wait for the fresh file
    # so we don't match the previous run's lines, then for RealRuins load + snapshot store.
    for _ in $(seq 1 60); do
      [[ -f "$LOG" ]] && (( $(plat_mtime "$LOG") >= started )) && break; sleep 1
    done
    wait_log "RealRuins finished patching" 1 180
    wait_log "\[RealRuins\]\[Store\]" 1 60
    # The store keeps loading snapshots (~170 ms each) after the menu appears; wait for the log to go quiet.
    for _ in $(seq 1 120); do
      (( $(date +%s) - $(plat_mtime "$LOG") >= 5 )) && break; sleep 1
    done
    echo "running (pid $(pid))" ;;
  quit)
    p=$(pid); [[ -z "$p" ]] && { echo "not running"; exit 0; }
    plat_kill "$p"; for _ in $(seq 1 20); do [[ -z "$(pid)" ]] && break; sleep 0.5; done
    [[ -z "$(pid)" ]] && echo "stopped" || { plat_kill "$p" force; echo "killed"; } ;;
  status) p=$(pid); [[ -n "$p" ]] && echo "running (pid $p)" || echo "not running" ;;
  focus) focus ;;
  ss)
    # Screenshot whose pixel coords are exactly the coords click/move take.
    name="${1:-shot-$(date +%H%M%S)}"
    [[ "$PLATFORM" == win ]] && focus   # win captures the window from screen; it must be on top
    plat_ss "$OUT/$name.png"
    echo "$OUT/$name.png" ;;
  click|rclick|move) focus; plat_mouse "$cmd" "$1" "$2" ;;
  key)    focus; plat_key "$@" ;;
  esc)    focus; plat_key esc ;;
  type)   focus; plat_type "$1" ;;
  scroll) focus; for _ in $(seq 1 "${2:-1}"); do plat_scroll "$1"; done ;;
  zoomout)
    # Point the cursor at the map (default: roughly screen/window center) and scroll out.
    focus; plat_mouse move "${1:-$ZOOM_X}" "${2:-$ZOOM_Y}"; for _ in $(seq 1 10); do plat_scroll -5; done ;;
  logmark) loglines ;;
  logsince)
    # Log lines after a mark, without the per-message stack trace lines. Optional grep -E filter.
    from="${1:-1}"; filt="${2:-.}"
    tail -n +"$from" "$LOG" | tr -d '\r' | grep -v -E '^\s+at |^\s*$|^\[Ref ' | grep -E "$filt" | cut -c1-300 || true ;;
  errors)
    from="${1:-1}"
    # Stack lines and 1.7's leading timestamps are dropped so identical messages count together.
    tail -n +"$from" "$LOG" | tr -d '\r' | grep -v -E '^\s+at ' | grep -E 'ERROR|Exception|Failed to' \
      | sed -E 's/^[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9:.]+ +//' | cut -c1-300 | sort | uniq -c | sort -rn || true ;;
  waitlog) wait_log "$1" "${2:-1}" "${3:-120}" ;;
  log) echo "$LOG" ;;
  where) echo "platform=$PLATFORM"; echo "game=$GAME_DIR"; echo "log=$LOG"; echo "out=$OUT" ;;
  *)
    cat <<EOF
driver.sh commands (coords = pixel coords read straight off the latest 'ss' screenshot):
  build                     msbuild + postbuild copy into game Mods folder (+ game-version DLL copy)
  launch | quit | status    start game and wait for main menu / stop it
  ss [name]                 screenshot -> prints PNG path (in \$RR_OUT, default \$TMPDIR/realruins-run)
  click X Y | rclick X Y | move X Y
  key KEY [cmd|shift|ctrl|alt]   KEY = esc|enter|space|tab|backspace|left|right|up|down or a raw
                            platform keycode (macOS virtual keycode / Windows VK code)
  esc | type TEXT | scroll DY [N] | zoomout [X Y]
  logmark                   current Player.log line count (use as a mark)
  logsince MARK [REGEX]     log since mark, stack lines stripped
  errors MARK               ERROR/Exception/Failed lines since mark, counted
  waitlog REGEX [MARK] [SECS]
  log | where               Player.log path / resolved platform paths
EOF
    ;;
esac
