#!/bin/bash
# Drive RimWorld (macOS, Steam install) for testing RealRuins.
# Usage: driver.sh <command> [args]   — run with no args for the command list.
set -euo pipefail

SKILL_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$SKILL_DIR/../../.." && pwd)"
GAME_APP="$HOME/Library/Application Support/Steam/steamapps/common/RimWorld/RimWorld.app"
GAME_BIN_PATTERN="RimWorld.app/Contents/MacOS/RimWorld"
LOG="$HOME/Library/Application Support/Ludeon Studios/RimWorld by Ludeon Studios/Logs/Player.log"
OUT="${RR_OUT:-${TMPDIR:-/tmp}/realruins-run}"
INPUT_BIN="$OUT/input"
mkdir -p "$OUT"

die() { echo "error: $*" >&2; exit 1; }
pid() { pgrep -f "$GAME_BIN_PATTERN" | head -1 || true; }

input() {
  if [[ ! -x "$INPUT_BIN" || "$SKILL_DIR/input.swift" -nt "$INPUT_BIN" ]]; then
    swiftc -O "$SKILL_DIR/input.swift" -o "$INPUT_BIN" >&2
  fi
  "$INPUT_BIN" "$@"
}

focus() {
  local p; p=$(pid); [[ -n "$p" ]] || die "game not running"
  osascript -e "tell application \"System Events\" to set frontmost of (first process whose unix id is $p) to true" >/dev/null
  sleep 0.3
}

# Wait until a regex appears in Player.log after line $2 (default 1), timeout $3 seconds.
wait_log() {
  local pat="$1" from="${2:-1}" timeout="${3:-120}" t=0
  while (( t < timeout )); do
    if [[ -f "$LOG" ]] && tail -n +"$from" "$LOG" | grep -q -E "$pat"; then return 0; fi
    sleep 1; t=$((t+1))
  done
  die "timed out after ${timeout}s waiting for /$pat/ in Player.log"
}

loglines() { [[ -f "$LOG" ]] && wc -l < "$LOG" | tr -d ' ' || echo 0; }

cmd="${1:-help}"; shift || true
case "$cmd" in
  build)
    # Compiles into 1.7/Assemblies and runs postbuild.sh (copies mod into the game's Mods folder).
    cd "$REPO" && msbuild Source/RealRuins.csproj -v:m -nologo ;;
  launch)
    # Starts the game (Steam must be running) and blocks until the main menu is reachable.
    [[ -n "$(pid)" ]] && { echo "already running (pid $(pid))"; exit 0; }
    started=$(date +%s)
    open "$GAME_APP"
    # Player.log is recreated per run (old one -> Player-prev.log). Wait for the fresh file
    # so we don't match the previous run's lines, then for RealRuins load + snapshot store.
    for _ in $(seq 1 60); do
      [[ -f "$LOG" ]] && (( $(stat -f %B "$LOG") >= started )) && break; sleep 1
    done
    wait_log "RealRuins finished patching" 1 180
    wait_log "\[RealRuins\]\[Store\]" 1 60
    # The store keeps loading snapshots (~170 ms each) after the menu appears; wait for the log to go quiet.
    for _ in $(seq 1 120); do
      (( $(date +%s) - $(stat -f %m "$LOG") >= 5 )) && break; sleep 1
    done
    echo "running (pid $(pid))" ;;
  quit)
    p=$(pid); [[ -z "$p" ]] && { echo "not running"; exit 0; }
    kill "$p"; for _ in $(seq 1 20); do [[ -z "$(pid)" ]] && break; sleep 0.5; done
    [[ -z "$(pid)" ]] && echo "stopped" || { kill -9 "$p"; echo "killed"; } ;;
  status) p=$(pid); [[ -n "$p" ]] && echo "running (pid $p)" || echo "not running" ;;
  focus) focus ;;
  ss)
    # Full-screen capture, downscaled to POINT size so image pixel coords == click coords.
    name="${1:-shot-$(date +%H%M%S)}"
    read -r w _ < <(input screensize)
    screencapture -x "$OUT/$name.full.png"
    sips -Z "$w" "$OUT/$name.full.png" --out "$OUT/$name.png" >/dev/null
    rm -f "$OUT/$name.full.png"
    echo "$OUT/$name.png" ;;
  click|rclick|move) focus; input "$cmd" "$1" "$2" ;;
  key)    focus; input key "$@" ;;
  esc)    focus; input key 53 ;;
  type)   focus; input type "$1" ;;
  scroll) focus; for _ in $(seq 1 "${2:-1}"); do input scroll "$1"; done ;;
  zoomout) focus; input move "${1:-770}" "${2:-500}"; for _ in $(seq 1 10); do input scroll -5; done ;;
  logmark) loglines ;;
  logsince)
    # Log lines after a mark, without the per-message stack trace lines. Optional grep -E filter.
    from="${1:-1}"; filt="${2:-.}"
    tail -n +"$from" "$LOG" | grep -v -E '^\s+at |^\s*$|^\[Ref ' | grep -E "$filt" | cut -c1-300 || true ;;
  errors)
    from="${1:-1}"
    tail -n +"$from" "$LOG" | grep -E 'ERROR|Exception|Failed to' | cut -c1-300 | sort | uniq -c || true ;;
  waitlog) wait_log "$1" "${2:-1}" "${3:-120}" ;;
  log) echo "$LOG" ;;
  *)
    cat <<EOF
driver.sh commands (coords are screen points, read straight off an 'ss' screenshot):
  build                     msbuild + postbuild copy into game Mods folder
  launch | quit | status    start game and wait for main menu / stop it
  ss [name]                 screenshot -> prints PNG path (in \$RR_OUT, default \$TMPDIR/realruins-run)
  click X Y | rclick X Y | move X Y
  key CODE [cmd|shift|ctrl|alt]   macOS virtual keycode (53=Esc, 36=Return, 49=Space)
  esc | type TEXT | scroll DY [N] | zoomout [X Y]
  logmark                   current Player.log line count (use as a mark)
  logsince MARK [REGEX]     log since mark, stack lines stripped
  errors MARK               ERROR/Exception/Failed lines since mark, counted
  waitlog REGEX [MARK] [SECS]
EOF
    ;;
esac
