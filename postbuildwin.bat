@echo off
setlocal enabledelayedexpansion

REM Define the RimWorld mods folder - UPDATE THIS PATH TO YOUR RIMWORLD INSTALLATION
set "MOD_PATH=F:\SteamLibrary\steamapps\common\RimWorld\Mods\RealRuins"

REM Create mod directory if it doesn't exist
if not exist "!MOD_PATH!" mkdir "!MOD_PATH!"

REM --- Common files ---
xcopy /T /E /I /Y "About" "!MOD_PATH!\About"
copy /Y "About\About.xml" "!MOD_PATH!\About\About.xml"
copy /Y "About\PublishedFileId.txt" "!MOD_PATH!\About\PublishedFileId.txt"
if exist "About\Preview.png" copy /Y "About\Preview.png" "!MOD_PATH!\About\Preview.png"

copy /Y "LoadFolders.xml" "!MOD_PATH!\LoadFolders.xml"

xcopy /E /I /Y "Assemblies" "!MOD_PATH!\Assemblies"
xcopy /E /I /Y "Defs" "!MOD_PATH!\Defs"
xcopy /E /I /Y "Languages" "!MOD_PATH!\Languages"
xcopy /E /I /Y "Patches" "!MOD_PATH!\Patches"
xcopy /E /I /Y "Textures" "!MOD_PATH!\Textures"

REM --- Version 1.1 ---
xcopy /E /I /Y "1.1" "!MOD_PATH!\1.1"

REM --- Version 1.3 ---
if not exist "!MOD_PATH!\1.3\Assemblies" mkdir "!MOD_PATH!\1.3\Assemblies"
if exist "!MOD_PATH!\1.3\Assemblies\*.dll" del /Q "!MOD_PATH!\1.3\Assemblies\*.dll"
if exist "1.3\Assemblies\RealRuins.dll" copy /Y "1.3\Assemblies\RealRuins.dll" "!MOD_PATH!\1.3\Assemblies\"
xcopy /E /I /Y "1.3\Defs" "!MOD_PATH!\1.3\Defs"
xcopy /E /I /Y "1.3\Patches" "!MOD_PATH!\1.3\Patches"

REM --- Version 1.4 ---
if not exist "!MOD_PATH!\1.4\Assemblies" mkdir "!MOD_PATH!\1.4\Assemblies"
if exist "!MOD_PATH!\1.4\Assemblies\*.dll" del /Q "!MOD_PATH!\1.4\Assemblies\*.dll"
if exist "1.4\Assemblies\RealRuins.dll" copy /Y "1.4\Assemblies\RealRuins.dll" "!MOD_PATH!\1.4\Assemblies\"
xcopy /E /I /Y "1.4\Defs" "!MOD_PATH!\1.4\Defs"
xcopy /E /I /Y "1.4\Patches" "!MOD_PATH!\1.4\Patches"

REM --- Version 1.5 ---
if not exist "!MOD_PATH!\1.5\Assemblies" mkdir "!MOD_PATH!\1.5\Assemblies"
if exist "!MOD_PATH!\1.5\Assemblies\*.dll" del /Q "!MOD_PATH!\1.5\Assemblies\*.dll"
if exist "1.5\Assemblies\RealRuins.dll" copy /Y "1.5\Assemblies\RealRuins.dll" "!MOD_PATH!\1.5\Assemblies\"
xcopy /E /I /Y "1.5\Defs" "!MOD_PATH!\1.5\Defs"
xcopy /E /I /Y "1.5\Patches" "!MOD_PATH!\1.5\Patches"

REM --- Version 1.6 ---
if not exist "!MOD_PATH!\1.6\Assemblies" mkdir "!MOD_PATH!\1.6\Assemblies"
if exist "!MOD_PATH!\1.6\Assemblies\*.dll" del /Q "!MOD_PATH!\1.6\Assemblies\*.dll"
if exist "1.6\Assemblies\RealRuins.dll" copy /Y "1.6\Assemblies\RealRuins.dll" "!MOD_PATH!\1.6\Assemblies\"
xcopy /E /I /Y "1.6\Defs" "!MOD_PATH!\1.6\Defs"
xcopy /E /I /Y "1.6\Patches" "!MOD_PATH!\1.6\Patches"

echo.
echo Post-build copy completed successfully!
echo Files copied to: !MOD_PATH!

endlocal