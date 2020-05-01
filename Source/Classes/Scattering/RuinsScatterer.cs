using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using RimWorld.BaseGen;
using Verse;

namespace RealRuins {
    class RuinsScatterer {

        //ResolveParams's GetCustom/SetCustom is broken now, and it is a sealed (WHY??!) struct (WHY??!?!?) so I neither can fix it, nor make a dirty hack.
        //Seems like I have to abandon this approach and make direct calls.
        static public void Scatter(ResolveParams rp, ScatterOptions options, CoverageMap coverage, List<AbstractDefenderForcesGenerator> generators) {
            Debug.Log(Debug.Scatter, "Running stand-alone scatterer");
            if (options == null) {
                Debug.Warning(Debug.Scatter, "Scatter options are null, aborting");
                return;
            }

            DateTime start = DateTime.UtcNow;
            Debug.Log(Debug.Scatter, "[0 s]: Loading blueprint of size {0} - {1} to deploy at {2}, {3}", options.minRadius, options.maxRadius, rp.rect.minX, rp.rect.minZ);

            Blueprint bp = null;
            Map map = BaseGen.globalSettings.map;

            //probably should split scattering options into several distinct objects
            string filename = options.blueprintFileName;
            if (string.IsNullOrEmpty(filename) || !BlueprintLoader.CanLoadBlueprintAtPath(filename)) {
                bp = BlueprintFinder.FindRandomBlueprintWithParameters(out filename, options.minimumAreaRequired, options.minimumDensityRequired, 15, removeNonQualified: options.deleteLowQuality);
                if (string.IsNullOrEmpty(filename)) {
                    //still null = no suitable blueprints, fail.
                    Debug.Warning(Debug.Scatter, "Blueprint name was null and could not find another suitable blueprint, skipping");
                    return;
                }
            }

            if (!options.shouldLoadPartOnly) { //should not cut => load whole
                if (bp == null) { //if not loaded yet
                    bp = BlueprintLoader.LoadWholeBlueprintAtPath(filename);
                }
            } else {
                int radius = Rand.Range(options.minRadius, options.maxRadius);
                if (bp == null) {
                    bp = BlueprintLoader.LoadWholeBlueprintAtPath(filename).RandomPartCenteredAtRoom(new IntVec3(radius, 0, radius));
                } else {
                    bp = bp.RandomPartCenteredAtRoom(new IntVec3(radius, 0, radius));
                }
            }

            if (bp == null) {
                Debug.Warning(Debug.Scatter, "Blueprint is still null after attempting to load any qualifying, returning");
                return;
            }


            Debug.Extra(Debug.Scatter, "Blueprint loaded, cutting and searching for rooms");
            bp.CutIfExceedsBounds(map.Size);

            // Here we have our blueprint loaded and ready to action. Doing stuff:
            BlueprintPreprocessor.ProcessBlueprint(bp, options); //Preprocess: remove missing and unnecessary items according to options
            bp.FindRooms(); //Traverse blueprint and construct rooms map
            options.roomMap = bp.wallMap;

            //Debug.PrintIntMap(bp.wallMap, delta: 1);
            Debug.Extra(Debug.Scatter, "Rooms traversed, initializing transfer utility");

            BlueprintTransferUtility btu = new BlueprintTransferUtility(bp, map, rp, options); //prepare blueprint transferrer
            Debug.Extra(Debug.Scatter, "Initialized, removing incompatible items...");

            btu?.RemoveIncompatibleItems(); //remove incompatible items 
            Debug.Extra(Debug.Scatter, "Recalculating stats...");

            bp.UpdateBlueprintStats(true); //Update total cost, items count, etc

            Debug.Extra(Debug.Scatter, "Deteriorating...");
            DeteriorationProcessor.Process(bp, options); //create deterioration maps and do deterioration

            Debug.Extra(Debug.Scatter, "Scavenging...");
            ScavengingProcessor sp = new ScavengingProcessor();
            sp.RaidAndScavenge(bp, options); //scavenge remaining items according to scavenge options

            Debug.Extra(Debug.Scatter, "[{0} s] Prepared, about to start transferring.", DateTime.UtcNow.Subtract(start).TotalSeconds);
            try {
                btu.Transfer(coverage); //transfer blueprint
                Debug.Extra(Debug.Scatter, "[{0} s] Transferred.", DateTime.UtcNow.Subtract(start).TotalSeconds);
            } catch (Exception e) {
                Debug.Error(Debug.BlueprintTransfer, "Failed to transfer blueprint due to {0}", e);
            }

            if (generators != null) {
                foreach (AbstractDefenderForcesGenerator generator in generators) {
                    try {
                        generator.GenerateForces(map, rp, options);
                    } catch (Exception e) {
                        Debug.Error(Debug.BlueprintTransfer, "Failed to generate forces: {0}", e);
                    }
                }
            }
            Debug.Log(Debug.Scatter, "[{0} s] Generated forces.", DateTime.UtcNow.Subtract(start).TotalSeconds);

            if (options.shouldAddFilth) {
                try {
                    btu.AddFilthAndRubble(); //add filth and rubble
                } catch (Exception e) {
                    Debug.Warning(Debug.BlueprintTransfer, "Failed to add filth and rubble: {0}", e);
                }
            }
            Debug.Extra(Debug.Scatter, "[{0} s] Spiced up with rubble. Completed.", DateTime.UtcNow.Subtract(start).TotalSeconds);

            Debug.Log(Debug.Scatter, "Chunk scattering finished, moving");
        }
    }
}
