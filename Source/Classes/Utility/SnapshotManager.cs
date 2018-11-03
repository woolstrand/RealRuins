using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

using Verse;

namespace RealRuins {
    class SnapshotManager {
        private static SnapshotManager instance = null;
        public static SnapshotManager Instance {
            get {
                if (instance == null) {
                    instance = new SnapshotManager();
                }
                return instance;
            }
        }

        private readonly SnapshotStoreManager storeManager = SnapshotStoreManager.Instance;

        static Dictionary<string, DateTime> snapshotTimestamps = new Dictionary<string, DateTime>();

        private List<string> snapshotsToLoad = new List<string>();

        public void LoadSomeSnapshots() {
            if (snapshotsToLoad.Count > 0) return; //don't start loader if there is something still to load

            AmazonS3Service listLoader = new AmazonS3Service();

            Debug.Message("Loading some snapshots...", true);
            listLoader.AmazonS3ListFiles(delegate (List<string> files) {
                Debug.Message("Loaded list of {0} elements...", files.Count);
                files = storeManager.FilterOutExistingItems(files);

                Debug.Message("Filtered down to list of {0} elements...", files.Count);

                Random rng = new Random();
                int n = files.Count;
                while (n > 1) {
                    n--;
                    int k = rng.Next(n + 1);
                    var value = files[k];
                    files[k] = files[n];
                    files[n] = value;
                }

                Debug.Message("Shuffled...");


                int maxNumberToLoad = 25;
                if (storeManager.StoredSnapshotsCount() < 50) {
                    maxNumberToLoad = 50;
                }

                foreach (string filename in files) {
                    snapshotsToLoad.Add(filename);
                    if (snapshotsToLoad.Count >= maxNumberToLoad) break;
                }

                Debug.Message("Loading {0} files...", snapshotsToLoad.Count);


                if (snapshotsToLoad.Count > 0) {
                    LoadNextSnapshot();
                }
            });
        }

        private void LoadNextSnapshot() {
            string next = snapshotsToLoad.Pop();

            Debug.Message("Loading snapshot {0}", next);

            AmazonS3Service elementLoader = new AmazonS3Service();
            elementLoader.AmazonS3DownloadSnapshot(next, delegate (bool success, byte[] data) {
                if (success) {
                    storeManager.StoreBinaryData(data, next);
                }
                if (snapshotsToLoad.Count > 0) {
                    LoadNextSnapshot();
                }
            });
        }

        public void UploadCurrentMapSnapshot() {
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
                Compressor.ZipFile(tmpFilename);

                if (RealRuins_ModSettings.offlineMode) {
                    SnapshotStoreManager.Instance.StoreBinaryData(File.ReadAllBytes(tmpFilename), "local-" + snapshotId + ".bp");
                } else {
                    int deltaDays = (int)(DateTime.UtcNow - DateTime.FromBinary(-8586606884938459217)).TotalDays;
                    int now = int.Parse(DateTime.UtcNow.ToString("yyyyMMdd"));
                    string amazonFilename = (20181030 - deltaDays).ToString() + "-" + worldId + Find.CurrentMap.uniqueID + "-jeluder.bp";

                    Debug.Message("Uploading file {0}", amazonFilename);

                    AmazonS3Service uploader = new AmazonS3Service();
                    uploader.AmazonS3Upload(tmpFilename, "", amazonFilename);
                }
            }
        }
    }
}
