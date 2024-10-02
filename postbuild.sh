
# --- common ---

cp -R ../About /Users/woolstrand/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins

cp -R ../LoadFolders.xml /Users/woolstrand/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins/LoadFolders.xml

cp -R ../Assemblies /Users/woolstrand/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins

cp -R ../Defs /Users/woolstrand/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins

cp -R ../Languages /Users/woolstrand/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins

cp -R ../Patches /Users/woolstrand/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins

cp -R ../Textures /Users/woolstrand/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins

# --- 1.1 ---

cp -R ../1.1 /Users/woolstrand/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins

# --- 1.4 ---

mkdir -p /Users/woolstrand/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins/1.4/Assemblies

rm -f /Users/woolstrand/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins/1.4/Assemblies/*.dll

cp -R ../1.4/Assemblies/RealRuins.dll /Users/woolstrand/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins/1.4/Assemblies/RealRuins.dll

cp -R ../1.4/Defs /Users/woolstrand/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins/1.4

cp -R ../1.4/Patches /Users/woolstrand/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins/1.4

# --- 1.5 ---

mkdir -p /Users/woolstrand/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins/1.5/Assemblies

rm -f /Users/woolstrand/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins/1.5/Assemblies/*.dll

cp -R ../1.5/Assemblies/RealRuins.dll /Users/woolstrand/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins/1.5/Assemblies/RealRuins.dll

cp -R ../1.5/Defs /Users/woolstrand/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins/1.5

cp -R ../1.5/Patches /Users/woolstrand/Library/Application\ Support/Steam/steamapps/common/RimWorld/RimWorldMac.app/Mods/RealRuins/1.5
