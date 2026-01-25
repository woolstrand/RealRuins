@echo off
setlocal enabledelayedexpansion

REM Change to the directory where this script is located
cd /d "%~dp0"

REM Define the RimWorld mods folder - uses %USERPROFILE% for cross-user compatibility
REM Update the path after RimWorld if your Steam library is in a different location
REM set "MOD_BASE_FOLDER=%USERPROFILE%\Documents\Steam\steamapps\common\RimWorld\Mods\RealRuins"

REM Alternative: If Steam is installed in a custom location, uncomment and modify:
set "MOD_BASE_FOLDER=F:\SteamLibrary\steamapps\common\RimWorld\Mods\RealRuins"

REM Create mod directory if it doesn't exist
if not exist "!MOD_BASE_FOLDER!" mkdir "!MOD_BASE_FOLDER!"

REM --- Common files ---
xcopy /E /I /Y "About" "!MOD_BASE_FOLDER!\About"
copy /Y "LoadFolders.xml" "!MOD_BASE_FOLDER!\LoadFolders.xml"
xcopy /E /I /Y "Assemblies" "!MOD_BASE_FOLDER!\Assemblies"
xcopy /E /I /Y "Defs" "!MOD_BASE_FOLDER!\Defs"
xcopy /E /I /Y "Languages" "!MOD_BASE_FOLDER!\Languages"
xcopy /E /I /Y "Patches" "!MOD_BASE_FOLDER!\Patches"
xcopy /E /I /Y "Textures" "!MOD_BASE_FOLDER!\Textures"

REM --- Versioned folders ---
xcopy /E /I /Y "1.1" "!MOD_BASE_FOLDER!\1.1"
xcopy /E /I /Y "1.3" "!MOD_BASE_FOLDER!\1.3"
xcopy /E /I /Y "1.4" "!MOD_BASE_FOLDER!\1.4"
xcopy /E /I /Y "1.5" "!MOD_BASE_FOLDER!\1.5"
xcopy /E /I /Y "1.6" "!MOD_BASE_FOLDER!\1.6"

echo.
echo Post-build copy completed successfully!
echo Files copied to: !MOD_BASE_FOLDER!

endlocal
