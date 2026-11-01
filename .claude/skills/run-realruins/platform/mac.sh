# macOS backend for driver.sh (sourced). Steam install, RimWorld.app, CGEvent input via mac-input.swift.
# Defines: GAME_DIR LOG and plat_* functions used by driver.sh.

GAME_DIR="${RR_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/RimWorld}"
# Launch RimWorld.app: the csproj StartProgram points at RimWorldMac.app/..., which has no binary.
GAME_APP="$GAME_DIR/RimWorld.app"
GAME_BIN_PATTERN="RimWorld.app/Contents/MacOS/RimWorld"
LOG="$HOME/Library/Application Support/Ludeon Studios/RimWorld by Ludeon Studios/Logs/Player.log"
INPUT_BIN="$OUT/input"
ZOOM_X=770 ZOOM_Y=500   # default zoomout cursor point (screen points)

plat_input() {
  if [[ ! -x "$INPUT_BIN" || "$PLAT_DIR/mac-input.swift" -nt "$INPUT_BIN" ]]; then
    swiftc -O "$PLAT_DIR/mac-input.swift" -o "$INPUT_BIN" >&2
  fi
  "$INPUT_BIN" "$@"
}

plat_build()  { msbuild Source/RealRuins.csproj -v:m -nologo; }
plat_pid()    { pgrep -f "$GAME_BIN_PATTERN" | head -1 || true; }
plat_launch() { open "$GAME_APP"; }
plat_kill()   { kill ${2:+-9} "$1"; }   # plat_kill PID [force]
plat_mtime()  { stat -f %m "$1"; }

plat_focus() {
  osascript -e "tell application \"System Events\" to set frontmost of (first process whose unix id is $1) to true" >/dev/null
  sleep 0.3
}

# Full-screen capture, downscaled to POINT size so image pixel coords == click coords.
plat_ss() {
  local out="$1" w
  read -r w _ < <(plat_input screensize)
  screencapture -x "$out.full.png"
  sips -Z "$w" "$out.full.png" --out "$out" >/dev/null
  rm -f "$out.full.png"
}

plat_mouse() { plat_input "$@"; }        # click|rclick|move X Y
plat_scroll() { plat_input scroll "$1"; }
plat_type()  { plat_input type "$1"; }

# Virtual keycodes (macOS): names map to codes, numbers pass through.
plat_key() {
  local k="$1"; shift
  case "$k" in
    esc) k=53 ;; enter|return) k=36 ;; space) k=49 ;; tab) k=48 ;; backspace) k=51 ;;
    left) k=123 ;; right) k=124 ;; down) k=125 ;; up) k=126 ;;
  esac
  plat_input key "$k" "$@"
}
