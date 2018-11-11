using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;

using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace RealRuins
{
    class SnapshotStoreManager
    {
        private static SnapshotStoreManager instance = null;
        public static SnapshotStoreManager Instance {
            get {
                if (instance == null) {
                    instance = new SnapshotStoreManager();
                }
                return instance;
            }
        }

        private string rootFolder = "../Snapshots";
        private long totalFilesSize = 0;
        private int totalFileCount = 0;

        private void CheckFolderExistence() {
            if (!Directory.Exists(rootFolder)) {
                Directory.CreateDirectory(rootFolder);
            }
        }

        public SnapshotStoreManager() {
            CheckFolderExistence();
            RecalculateFilesSize();
        }

        public void StoreData(string buffer, string filename) {
            StoreBinaryData(Encoding.UTF8.GetBytes(buffer), filename);
        }

        public void StoreBinaryData(byte[] buffer, string filename) {
            //when storing a file you need to remove older version of snapshots of the same game
            CheckFolderExistence();
            string[] parts = filename.Split('=');
            if (parts.Count() > 1) {
                int date = 0;
                if (int.TryParse(parts[0], out date)) {
                    parts[0] = "*";
                    string mask = string.Join("=", parts);
                    string[] files = Directory.GetFiles(rootFolder, mask);
                    foreach (string existingFile in files) {
                        int existingFileDate = 0;
                        string[] existingFileParts = existingFile.Split('-');
                        if (int.TryParse(existingFileParts[0], out existingFileDate)) {
                            if (existingFileDate > date) {
                                //there is more fresh file. no need to save this one.
                                return;
                            } else {
                                //remove older files
                                File.Delete(existingFile);
                            }
                        }
                    }
                }
            }

            //writing file in all cases except "newer version available"
            File.WriteAllBytes(rootFolder + "/" + filename, buffer);
            RecalculateFilesSize();
        }

        private string DoGetRandomFilenameFromRootFolder() {
            var files = Directory.GetFiles(rootFolder);
            if (files.Length == 0) return null;

            int index = Rand.Range(0, files.Length);
            Debug.Message("files length: {0} count {1}, selected: {2}", files.Length, files.Count(), index);
            return files[index];
        }

        public string RandomSnapshotFilename() {
            CheckFolderExistence();
            string filename = null;
            do {
                filename = DoGetRandomFilenameFromRootFolder();
                if (filename == null) return null; //no more valid files. sorry, no party.
            } while (filename == null);
            return filename;
        }

        public int StoredSnapshotsCount() {
            return totalFileCount;
        }

        public List<string> FilterOutExistingItems(List<string> source) {
            CheckFolderExistence();

            List<string> result = new List<string>();

            foreach (string item in source) {
                if (!File.Exists(rootFolder + "/" + item)) {
                    result.Add(item);
                }
            }

            return result;
        }

        public void CheckCacheContents() {
            CheckFolderExistence();

            if (RealRuins_ModSettings.offlineMode) {
                var files = Directory.GetFiles(rootFolder);
                foreach (string fileName in files) {
                    if (!fileName.Contains("local")) {
                        File.Delete(fileName); //delete all remote files in offline mode.
                    }
                }
            }
            RecalculateFilesSize();
        }

        public void ClearCache() {
            CheckFolderExistence();

            Directory.Delete(rootFolder, true);
            Directory.CreateDirectory(rootFolder);
            totalFilesSize = 0;
        }

        public long TotalSize() {
            return totalFilesSize;
        }

        private void RecalculateFilesSize() {
            CheckFolderExistence();

            var filesList = Directory.GetFiles(rootFolder);
            long totalSize = 0;
            foreach (string file in filesList) {
                totalSize += new FileInfo(file).Length;
            }

            totalFilesSize = totalSize;
            totalFileCount = filesList.Length;
        }

        public void CheckCacheSizeLimits() {
            CheckFolderExistence();

            var files = Directory.GetFiles(rootFolder);
            List<string> filesList = files.ToList();
            filesList.Sort();
            filesList.Reverse();
            

            long totalSize = 0;
            foreach (string file in filesList) {
                totalSize += new FileInfo(file).Length;
                if (totalSize > RealRuins_ModSettings.diskCacheLimit * 1024 * 1024) {
                    File.Delete(file);
                }
            }
            RecalculateFilesSize();
        }

    }
}
