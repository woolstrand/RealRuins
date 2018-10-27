using System;
using System.Linq;
using System.Reflection;

using Harmony;
using Verse;
using RimWorld;
using UnityEngine;
//https://pastebin.com/LrAb814Z

namespace RealRuins
{
    [StaticConstructorOnStartup]
    static class RealRuins {
        static RealRuins() {
            var harmony = HarmonyInstance.Create("com.woolstrand.realruins");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        static class SnapshotSaver {
            public static void SaveSnapshot() {
                SnapshotGenerator generator = new SnapshotGenerator(Find.CurrentMap);
                string tmpFilename = generator.Generate();

                if (tmpFilename != null) {
                    string amazonFilename = DateTime.UtcNow.ToString("yyyyMMdd") + "-" + (Math.Abs(Find.World.info.persistentRandomValue)).ToString() + Find.CurrentMap.uniqueID + "-jeluder.xml";

                    AmazonS3Service uploader = new AmazonS3Service();
                    uploader.AmazonS3Upload(tmpFilename, "", amazonFilename);
                }
            }
        }

        [HarmonyPatch(typeof(UIRoot_Entry), "Init", new Type[0])]
        static class UIRoot_Entry_Init_Patch {
            static void Postfix() {
                Debug.Message("real ruins postinit");
                SnapshotManager.Instance.LoadSomeSnapshots();
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