using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.IO;

using Harmony;
using Verse;
using RimWorld;
using UnityEngine;

namespace RealRuins
{
    [StaticConstructorOnStartup]
    static class RealRuins {


        static RealRuins() {
            var harmony = HarmonyInstance.Create("com.woolstrand.realruins");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        static class SnapshotSaver {
            static Dictionary<string, DateTime> snapshotTimestamps = new Dictionary<string, DateTime>();

            public static void SaveSnapshot() {
                if (!RealRuins_ModSettings.allowUploads && !RealRuins_ModSettings.offlineMode) return;

                string worldId = (Math.Abs(Find.World.info.persistentRandomValue)).ToString();
                string mapId = Find.CurrentMap.uniqueID.ToString();
                string snapshotId = worldId + mapId;

                if (snapshotTimestamps.ContainsKey(snapshotId) && (DateTime.Now - snapshotTimestamps[snapshotId]).TotalMinutes < 180) {
                    return; //skip upload if we're trying to do it more frequent than once per three hours.
                }

                //we actually don't care if something goes wrong. big data, y'know
                snapshotTimestamps[snapshotId] = DateTime.Now;

                SnapshotGenerator generator = new SnapshotGenerator(Find.CurrentMap);
                if (!generator.CanGenerate()) return; //skip if generation is not allowed on some reason (too small area, empty area, whatever)
                string tmpFilename = generator.Generate();

                if (tmpFilename != null) {
                    if (RealRuins_ModSettings.offlineMode) {
                        SnapshotStoreManager.Instance.StoreData(File.ReadAllText(tmpFilename), "local-" + snapshotId + ".xml");
                    } else {
                        string amazonFilename = DateTime.UtcNow.ToString("yyyyMMdd") + "-" + worldId + Find.CurrentMap.uniqueID + "-jeluder.xml";

                        AmazonS3Service uploader = new AmazonS3Service();
                        uploader.AmazonS3Upload(tmpFilename, "", amazonFilename);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(UIRoot_Entry), "Init", new Type[0])]
        static class UIRoot_Entry_Init_Patch {
            static void Postfix() {
                if (RealRuins_ModSettings.allowDownloads) {
                    SnapshotManager.Instance.LoadSomeSnapshots();
                }
                SnapshotStoreManager.Instance.CheckCacheSizeLimits();
            }
        }

        [HarmonyPatch(typeof(GameDataSaveLoader), "SaveGame")]
        class SaveGame_Patch {
            static void Postfix() {
                SnapshotSaver.SaveSnapshot();
            }
        }

        [HarmonyPatch(typeof(GenGameEnd), "EndGameDialogMessage", typeof(string), typeof(bool), typeof(Color))]
        class GameOver_Patch {
            static void Postfix() {
                SnapshotSaver.SaveSnapshot();
            }
        }
    }
}