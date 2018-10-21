using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        private List<string> snapshotsToLoad = new List<string>();

        public void LoadSomeSnapshots() {
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


                int maxNumberToLoad = 10;
                if (storeManager.StoredSnapshotsCount() < 30) {
                    maxNumberToLoad = 30;
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
            elementLoader.AmazonS3DownloadSnapshot(next, delegate (string data) {
                //TODO: check for file is a valid xml
                if (data != null) {
                    storeManager.StoreData(data, next);
                    if (snapshotsToLoad.Count > 0) {
                        LoadNextSnapshot();
                    } else {
                        Debug.Message("Finished loading snapshots");
                    }
                }
            });
        }
    }
}
