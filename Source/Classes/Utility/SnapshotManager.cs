using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

using Verse;
using UnityEngine;

namespace RealRuins {


    // Main thread only: public methods are called from the UI / game thread, and all API callbacks arrive
    // on the main thread too (CoroutineManager). That's what keeps the unlocked state below consistent.
    class SnapshotManager {

        private static void ExecuteAfter(Action action, TimeSpan delay) {
            CoroutineManager.Instance.RunAfter(action, (float)delay.TotalSeconds);
        }


        private static SnapshotManager instance = null;
        public static SnapshotManager Instance {
            get {
                if (instance == null) {
                    instance = new SnapshotManager();
                }
                return instance;
            }
        }

        public Action<int, int> Progress { set => progress = value; }
        public Action<bool> Completion { set => completion = value; }

        private readonly SnapshotStoreManager storeManager = SnapshotStoreManager.Instance;
        //private concurrentDownloads = 0;

        static Dictionary<string, DateTime> snapshotTimestamps = new Dictionary<string, DateTime>();

        private List<string> snapshotsToLoad = new List<string>();

        private int snapshotsToLoadCount = 0;
        private int loadedSnapshotsCount = 0;
        private int failedSnapshotsCount = 0;
        private bool completionReported = false;
        private bool loadIfExists = true;

        private Action<int, int> progress;
        private Action<bool> completion;

        private bool forceStop = false;


        public void Reset() {
            snapshotsToLoadCount = 0;
            loadedSnapshotsCount = 0;
            failedSnapshotsCount = 0;
            completionReported = false;
            snapshotsToLoad = new List<string>();
        }

        public void Stop() {
            forceStop = true;
        }

        //try to load snapshots until your guts are out
        public void AggressiveLoadSnapshots() {
            APIService service = new APIService();

            Debug.Log(Debug.Store, "Snapshot pool is almost empty, doing some aggressive loading...", true);
            
            service.LoadRandomMapsList(delegate (bool success, List<string> files) {
                if (!success) {
                    Debug.Log(Debug.Store, "Failed loading list of random maps. Rescheduling after 10 seconds");
                    ExecuteAfter(delegate () {
                        AggressiveLoadSnapshots();
                    }, new TimeSpan(0, 0, 20));
                    return;
                }

                Debug.Log(Debug.Store, "Loaded list of {0} elements...", files.Count);
                files = storeManager.FilterOutExistingItems(files);

                foreach (string filename in files) {
                    snapshotsToLoad.Add(filename);
                }

                Debug.Log(Debug.Store, "Loading {0} files...", snapshotsToLoad.Count);
                AggressiveLoadSnaphotsFromList(snapshotsToLoad, null);
            });
        }

        public void AggressiveLoadSnaphotsFromList(List<string> snapshotsToLoad, string gamePath = null, bool loadIfExists = true) {
            this.loadIfExists = loadIfExists;

            if (loadIfExists == false) {
                this.snapshotsToLoad = storeManager.FilterOutExistingItems(snapshotsToLoad, gamePath);
                Debug.Log(Debug.Store, "Filtered snapshots {0} -> {1}", snapshotsToLoad.Count, this.snapshotsToLoad.Count);
            } else {
                this.snapshotsToLoad = snapshotsToLoad; //in case it was call from the outside
            }

            snapshotsToLoadCount = this.snapshotsToLoad.Count;
            loadedSnapshotsCount = 0;
            failedSnapshotsCount = 0;
            completionReported = false;
            var concurrentLoaders = Math.Min(10, snapshotsToLoadCount);

            if (snapshotsToLoadCount > 0) {
                for (int i = 0; i < concurrentLoaders; i++) {
                    LoadNextSnapshot(gamePath);
                }
            } else {
                completion?.Invoke(true);
            }
        }

        public void LoadSomeSnapshots(int concurrent = 1, int retries = 10) {
            if (snapshotsToLoad.Count > 0) return; //don't start loader if there is something still to load

            loadIfExists = true;


            APIService service = new APIService();

            Debug.Log(Debug.Store, "Loading some snapshots...", true);

            service.LoadRandomMapsList(delegate (bool success, List<string> files) {
                if (!success) {
                    if (retries <= 0) {
                        Debug.Log(Debug.Store, "Failed loading list of random maps, giving up");
                        return;
                    }
                    Debug.Log(Debug.Store, "Failed loading list of random maps");
                    ExecuteAfter(delegate () {
                        LoadSomeSnapshots(concurrent, retries - 1);
                    }, new TimeSpan(0, 0, 20));
                    return;
                }

                Debug.Log(Debug.Store, "Loaded list of {0} elements...", files.Count);
                files = storeManager.FilterOutExistingItems(files);
            
                foreach (string filename in files) {
                    snapshotsToLoad.Add(filename);
                }

                Debug.Log(Debug.Store, "Loading {0} files...", snapshotsToLoad.Count);

                if (snapshotsToLoad.Count > 0) {
                    for (int i = 0; i < concurrent; i++) {
                        LoadNextSnapshot();
                    }
                }
            });
        }

        private void LoadNextSnapshot(string gamePath = null) {
            if (forceStop) {
                return;
            }

            if (snapshotsToLoad.Count == 0) return;
            string next = snapshotsToLoad.Pop();

            Debug.Log(Debug.Store, "Loading snapshot {0}", next);

            APIService service = new APIService();
            service.LoadMap(next, delegate (bool success, byte[] data) {
                if (success) {
                    // A snapshot counts as loaded only once it's on disk: completion consumers read the store right away.
                    storeManager.StoreBinaryData(data, next, gamePath, delegate (bool stored) {
                        if (stored) {
                            loadedSnapshotsCount++;
                        } else {
                            failedSnapshotsCount++;
                        }
                        SnapshotFinished();
                    });
                } else {
                    failedSnapshotsCount++;
                    Debug.Warning(Debug.Store, "Failed loading snapshot {0}", next);
                    SnapshotFinished();
                }

                if (snapshotsToLoad.Count > 0) {
                    LoadNextSnapshot(gamePath);
                }
            });
        }

        private void SnapshotFinished() {
            progress?.Invoke(loadedSnapshotsCount, snapshotsToLoadCount);

            if (!completionReported && loadedSnapshotsCount + failedSnapshotsCount == snapshotsToLoadCount) {
                completionReported = true;
                completion?.Invoke(true);
            }
        }

        //Uploads wait here until the server confirms them, so an upload cut short (game closed right after game over,
        //server down) is retried on the next start instead of being lost.
        private const string PendingUploadsFolderName = "RealRuinsPendingUploads";
        private const int MaxPendingUploads = 10;
        private const int PendingUploadMaxAgeDays = 14;

        private static string PendingUploadsFolderPath() {
            string path = Path.Combine(GenFilePaths.SaveDataFolderPath, PendingUploadsFolderName);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        //force: skip the once per three hours limit (used for the final snapshot on game over)
        public void UploadCurrentMapSnapshot(bool force = false) {
            UploadMapSnapshot(Find.CurrentMap, force);
        }

        public void UploadMapSnapshot(Map map, bool force = false) {
            if (map == null) {
                return;
            }

            //a cleared planetary POI becomes player-owned (and so a player home) while it is only being raided.
            //Its map is someone else's blueprint, so it must not be uploaded. Settling replaces the POI with a Settlement, after which uploading is fine.
            if (Find.CurrentMap.Parent is RealRuinsPOIWorldObject) {
                Debug.Log(Debug.Store, "Skipping snapshot of a planetary POI map");
                return;
            }

            string worldId = (Math.Abs(Find.World.info.persistentRandomValue)).ToString();
            string mapId = map.uniqueID.ToString();
            string snapshotId = worldId + mapId;

            if (!force && snapshotTimestamps.ContainsKey(snapshotId) && (DateTime.Now - snapshotTimestamps[snapshotId]).TotalMinutes < 180) {
                return; //skip upload if we're trying to do it more frequent than once per three hours.
            }

            //we actually don't care if something goes wrong. big data, y'know
            snapshotTimestamps[snapshotId] = DateTime.Now;

            SnapshotGenerator generator = new SnapshotGenerator(map);
            if (!generator.CanGenerate()) return; //skip if generation is not allowed on some reason (too small area, empty area, whatever)

            string tmpFilename = generator.Generate();

            if (tmpFilename != null) {
                Compressor.ZipFile(tmpFilename);

                if (RealRuins.SingleFile) {
                    SnapshotStoreManager.Instance.StoreBinaryData(File.ReadAllBytes(tmpFilename), "jeluder");
                } else if (RealRuins_ModSettings.offlineMode) {
                    SnapshotStoreManager.Instance.StoreBinaryData(File.ReadAllBytes(tmpFilename), "local-" + snapshotId);
                } else {
                    try {
                        string pendingFilename = Path.Combine(PendingUploadsFolderPath(), snapshotId + "-" + DateTime.UtcNow.Ticks + ".bp");
                        File.Move(tmpFilename, pendingFilename);
                        UploadSnapshotFile(pendingFilename, true, completion);
                    } catch (Exception e) {
                        Debug.Warning(Debug.Store, "Could not queue snapshot {0} for upload, uploading it directly: {1}", tmpFilename, e);
                        UploadSnapshotFile(tmpFilename, false, completion);
                    }
                }
            }
        }

        //Retries uploads left over from previous sessions. Called once on startup.
        public void UploadPendingSnapshots() {
            string folder;
            List<FileInfo> files;
            try {
                folder = Path.Combine(GenFilePaths.SaveDataFolderPath, PendingUploadsFolderName);
                if (!Directory.Exists(folder)) return;
                files = new DirectoryInfo(folder).GetFiles().OrderByDescending(f => f.LastWriteTimeUtc).ToList();
            } catch (Exception e) {
                Debug.Warning(Debug.Store, "Could not read pending uploads: {0}", e);
                return;
            }

            bool uploadsAllowed = RealRuins_ModSettings.allowUploads && !RealRuins_ModSettings.offlineMode && !RealRuins.SingleFile;
            for (int i = 0; i < files.Count; i++) {
                FileInfo file = files[i];
                bool expired = i >= MaxPendingUploads || (DateTime.UtcNow - file.LastWriteTimeUtc).TotalDays > PendingUploadMaxAgeDays;
                if (!uploadsAllowed || expired) {
                    try {
                        file.Delete();
                    } catch { }
                    continue;
                }
                Debug.Log(Debug.Store, "Retrying pending upload {0}", file.FullName);
                UploadSnapshotFile(file.FullName, true, null);
            }
        }

        private void UploadSnapshotFile(string filename, bool queued, Action<bool> onFinished) {
            Debug.Log(Debug.Store, "Uploading file {0}", filename);
            APIService service = new APIService();
            try {
                service.UploadMap(filename, OnUploaded);
            } catch (Exception e) {
                Debug.Warning(Debug.Store, "Could not upload snapshot {0}: {1}", filename, e);
            }

            void OnUploaded(bool success) {
                try {
                    if (success) {
                        if (!RealRuins_ModSettings.debugKeepSnapshotsAfterUpload) {
                            File.Delete(filename);
                        } else {
                            //move out of the pending folder, otherwise it would be uploaded again on the next start
                            string keptFilename = Path.Combine(Path.GetTempPath(), Path.GetFileName(filename));
                            if (keptFilename != filename) {
                                if (File.Exists(keptFilename)) File.Delete(keptFilename);
                                File.Move(filename, keptFilename);
                            }
                            Debug.Log(Debug.Store, "[DEBUG] Keeping snapshot file: {0}", keptFilename);
                        }
                    } else if (queued) {
                        Debug.Log(Debug.Store, "Upload failed, keeping {0} for a retry on the next start", filename);
                    } else {
                        File.Delete(filename); //could not be queued, nothing to retry from
                    }
                } catch (Exception e) {
                    Debug.Warning(Debug.Store, "Could not clean up uploaded snapshot {0}: {1}", filename, e);
                }
                onFinished?.Invoke(success);
            }
        }
    }
}
