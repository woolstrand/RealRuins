using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using RimWorld.BaseGen;
using Verse;

namespace RealRuins {
    public class SymbolResolver_RuinsScatterer : SymbolResolver {

        public override void Resolve(ResolveParams rp) {
            ScatterOptions options = rp.GetCustom<ScatterOptions>(Constants.ScatterOptions);
            if (options == null) return;

            Blueprint bp = null;
            Map map = BaseGen.globalSettings.map;

            //probably should split scattering options into several distinct objects

            if (options.blueprintFileName != null) {
                if (options.shouldCutBlueprint) {
                    bp = BlueprintLoader.LoadWholeBlueprintAtPath(options.blueprintFileName, options);
                } else {
                    int radius = Rand.Range(options.minRadius, options.maxRadius);
                    bp = BlueprintLoader.LoadRandomBlueprintPartAtPath(options.blueprintFileName, new IntVec3(radius * 2, 0, radius * 2), options);
                }
            } else if (options.shouldCutBlueprint) {
                bp = FindAndLoadRandomBlueprint(new IntVec3(0, 0, 0), options);
            } else {
                int radius = Rand.Range(options.minRadius, options.maxRadius);
                bp = FindAndLoadRandomBlueprint(new IntVec3(radius * 2, 0, radius * 2), options);
            }

            if (bp == null) return;

            if (options.shouldCutBlueprint) {
                IntVec3 random = CellFinder.RandomNotEdgeCell(4 + Math.Max(bp.width / 2, bp.height / 2), map);
                rp.rect = new CellRect(random.x - bp.width / 2, random.z - bp.height / 2, bp.width, bp.height);
            } else {
                rp.rect = new CellRect(map.Size.x / 2 - bp.width / 2, map.Size.z / 2 - bp.height / 2, bp.width, bp.height);
            }

            BlueprintTransferUtility btu = new BlueprintTransferUtility(bp, map, rp);
            btu.RemoveIncompatibleItems();

            DeteriorationProcessor.Process(bp, options);

            ScavengingProcessor.RaidAndScavenge(bp, options);

            btu.Transfer();

            btu.AddFilthAndRubble();
            
        }

        private Blueprint FindAndLoadRandomBlueprint(IntVec3 size, ScatterOptions options) {
            Blueprint result = null;
            int attemptNumber = 0;
            bool forceDelete = false;
            while (attemptNumber < 5 && result == null) {

                attemptNumber++;

                string snapshotName = SnapshotStoreManager.Instance.RandomSnapshotFilename();
                if (snapshotName == null) {
                    return null;
                }

                try {
                    if (size.x * size.z != 0) {
                        result = BlueprintLoader.LoadRandomBlueprintPartAtPath(snapshotName, size, options);
                    } else {
                        result = BlueprintLoader.LoadWholeBlueprintAtPath(snapshotName, options);
                    }
                    forceDelete = false;
                } catch (Exception e) {
                    Debug.Message("Corrupted file, removing. Error: {0}", e.ToString());
                    forceDelete = true;
                }

                if (result == null && (options.deleteLowQuality || forceDelete)) { //remove bad snapshots
                    Debug.Message("DELETING low quality file");
                    //File.Delete(snapshotName);
                    string deflatedName = snapshotName + ".xml";
                    if (!File.Exists(deflatedName)) {
                        File.Delete(deflatedName);
                    }
                }
            }
            return result;
        }
    }
}
