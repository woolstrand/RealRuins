# Windows backend for driver.sh (sourced; runs under Git Bash). Steam install, RimWorldWin64.exe,
# Win32 input/screenshots via win-input.cs. Defines: GAME_DIR LOG and plat_* functions used by driver.sh.

# Steam library holding RimWorld (appid 294100): RR_GAME_DIR, else found via Steam's libraryfolders.vdf.
find_game_dir() {
  local steam vdf lib
  steam=$(reg query 'HKCU\Software\Valve\Steam' //v SteamPath 2>/dev/null | awk '/SteamPath/ {sub(/.*REG_SZ +/, ""); print}' | tr -d '\r')
  vdf="$(cygpath -u "${steam:-C:/Program Files (x86)/Steam}")/steamapps/libraryfolders.vdf"
  if [[ -f "$vdf" ]]; then
    # Each library block: "path" "X:\\Lib" ... "apps" { "294100" ... }. Keep the last path before 294100.
    lib=$(awk '/"path"/ {gsub(/.*"path"[ \t]*"|"$/, ""); p=$0} /"294100"/ {print p; exit}' "$vdf" | sed 's/\\\\/\\/g')
    [[ -n "$lib" ]] && { echo "$(cygpath -u "$lib")/steamapps/common/RimWorld"; return; }
  fi
  echo "$(cygpath -u "${steam:-C:/Program Files (x86)/Steam}")/steamapps/common/RimWorld"
}

GAME_DIR="${RR_GAME_DIR:-$(find_game_dir)}"
# 1.7+ (new engine, .NET) ships RimWorld.exe; 1.6 and older shipped RimWorldWin64.exe (Unity).
GAME_EXE="$GAME_DIR/RimWorld.exe"; [[ -f "$GAME_EXE" ]] || GAME_EXE="$GAME_DIR/RimWorldWin64.exe"
GAME_IMAGE="$(basename "$GAME_EXE")"
# 1.7+ logs to Logs/Player.log (like macOS); 1.6 and older wrote Player.log straight into the data dir.
# Config/Prefs.xml (devMode) and Config/ModsConfig.xml live in the data dir.
DATA_DIR="$(cygpath -u "$USERPROFILE")/AppData/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios"
LOG="$DATA_DIR/Logs/Player.log"; [[ "$GAME_IMAGE" == RimWorld.exe ]] || LOG="$DATA_DIR/Player.log"
INPUT_BIN="$OUT/input.exe"
CSC="$(cygpath -u "${WINDIR:-C:/Windows}")/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
# Screenshots wider than this are downscaled (keeps them readable as images); clicks are scaled back.
SS_MAXW="${RR_SS_MAXW:-1600}"
ZOOM_X=$((SS_MAXW / 2)) ZOOM_Y=450   # default zoomout cursor point (window coords)

plat_input() {
  if [[ ! -x "$INPUT_BIN" || "$PLAT_DIR/win-input.cs" -nt "$INPUT_BIN" ]]; then
    "$CSC" -nologo -optimize -platform:x64 -r:System.Drawing.dll \
      -out:"$(cygpath -w "$INPUT_BIN")" "$(cygpath -w "$PLAT_DIR/win-input.cs")" >&2
  fi
  "$INPUT_BIN" "$@"
}

plat_build() {
  # VS Build Tools 2022 (or any VS with MSBuild + .NET Framework 4.7.2 targeting pack).
  local vswhere="/c/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe" msb
  [[ -x "$vswhere" ]] && msb=$("$vswhere" -latest -products '*' -requires Microsoft.Component.MSBuild \
    -find 'MSBuild\**\Bin\MSBuild.exe' | head -1 | tr -d '\r')
  [[ -n "$msb" ]] || die "MSBuild not found - install VS 2022 Build Tools (see SKILL.md)"
  "$(cygpath -u "$msb")" Source/RealRuins.csproj -v:m -nologo
}

plat_pid() {
  tasklist //FI "IMAGENAME eq $GAME_IMAGE" //FO CSV //NH 2>/dev/null \
    | awk -F'","' -v img="$GAME_IMAGE" 'index($0, img) {print $2; exit}' || true
}
# Detach stdio: otherwise the game inherits our stdout and callers piping driver output never see EOF.
plat_launch() { cmd.exe //c start "" "$(cygpath -w "$GAME_EXE")" </dev/null >/dev/null 2>&1; }
plat_kill()   { taskkill //PID "$1" ${2:+//F} >/dev/null 2>&1 || true; }   # without //F = WM_CLOSE
plat_mtime()  { stat -c %Y "$1"; }
plat_focus()  { plat_input focus "$1"; }

# Captures the game window's client area only (multi-monitor safe); coords are window-relative.
plat_ss()     { plat_input ss "$(plat_pid)" "$SS_MAXW" "$(cygpath -w "$1")" >/dev/null; }
plat_mouse()  { plat_input "$1" "$(plat_pid)" "$SS_MAXW" "$2" "$3"; }
plat_scroll() { plat_input scroll "$1"; }
plat_type()   { plat_input type "$1"; }

# Windows virtual-key codes: names map to codes, numbers pass through.
plat_key() {
  local k="$1"; shift
  case "$k" in
    esc) k=27 ;; enter|return) k=13 ;; space) k=32 ;; tab) k=9 ;; backspace) k=8 ;;
    left) k=37 ;; up) k=38 ;; right) k=39 ;; down) k=40 ;;
  esac
  plat_input key "$k" "$@"
}
