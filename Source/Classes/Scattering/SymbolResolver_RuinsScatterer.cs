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

            Debug.Message("Loading blueprint of size {0} - {1} to deploy at {2}, {3}", options.minRadius, options.maxRadius, rp.rect.minX, rp.rect.minZ);

            Blueprint bp = null;
            Map map = BaseGen.globalSettings.map;

            //probably should split scattering options into several distinct objects
            string filename = options.blueprintFileName;
            if (filename == null) {
                BlueprintFinder.FindRandomBlueprintWithParameters(out filename, options.minimumAreaRequired, options.minimumDensityRequired, 15);
                if (filename == null) {
                    //still null = no suitable blueprints, fail.
                    return;
                }
            }

            if (!options.shouldLoadPartOnly) { //should not cut => load whole
                bp = BlueprintLoader.LoadWholeBlueprintAtPath(filename);
            } else {
                int radius = Rand.Range(options.minRadius, options.maxRadius);
                bp = BlueprintLoader.LoadRandomBlueprintPartAtPath(filename, new IntVec3(radius * 2, 0, radius * 2));
            }

            if (bp == null) return;
            bp.CutIfExceedsBounds(map.Size);

            // Here we have our blueprint loaded and ready to action. Doing stuff:
            BlueprintPreprocessor.ProcessBlueprint(bp, options); //Preprocess: remove missing and unnecessary items according to options
            bp.FindRooms(); //Traverse blueprint and construct rooms map
            options.roomMap = bp.wallMap;

            //Debug.PrintIntMap(bp.wallMap, delta: 1);

            BlueprintTransferUtility btu = new BlueprintTransferUtility(bp, map, rp); //prepare blueprint transferrer
            btu.RemoveIncompatibleItems(); //remove incompatible items 
            bp.UpdateBlueprintStats(true); //Update total cost, items count, etc

            DeteriorationProcessor.Process(bp, options); //create deterioration maps and do deterioration

            ScavengingProcessor.RaidAndScavenge(bp, options); //scavenge remaining items according to scavenge options

            btu.Transfer(); //transfer blueprint
            if (!options.shouldAddRaidTriggers) {
                btu.ScatterMobs();
            }

            if (options.shouldAddRaidTriggers) {
                btu.ScatterRaidTriggers();
            }

            btu.AddFilthAndRubble(); //add filth and rubble
            
        }

        /*
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
                        result = BlueprintLoader.LoadRandomBlueprintPartAtPath(snapshotName, size);
                    } else {
                        result = BlueprintLoader.LoadWholeBlueprintAtPath(snapshotName);
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
        }*/
    }
}
