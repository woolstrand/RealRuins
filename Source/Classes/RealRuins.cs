using System;
using System.Linq;
using System.Reflection;

using Harmony;
using Verse;


namespace RealRuins
{
    [StaticConstructorOnStartup]
    static class RealRuins {
        static RealRuins() {
            var harmony = HarmonyInstance.Create("com.woolstrand.realruins");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
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
                SnapshotGenerator generator = new SnapshotGenerator(Find.CurrentMap);
                string tmpFilename = generator.Generate();

                if (tmpFilename != null) {
                    string amazonFilename = DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Find.CurrentMap.ConstantRandSeed + "-jeluder.xml";

                    AmazonS3Service uploader = new AmazonS3Service();
                    uploader.AmazonS3Upload(tmpFilename, "", amazonFilename);
                }
            }
        }
    }
}