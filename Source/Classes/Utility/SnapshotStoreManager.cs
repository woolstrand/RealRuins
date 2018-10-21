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

        public SnapshotStoreManager() {
            if (!Directory.Exists(rootFolder)) {
                Directory.CreateDirectory(rootFolder);
            }
        }

        public void StoreData(string buffer, string filename) {
            File.WriteAllText(rootFolder + "/" + filename, buffer);
        }

        private string DoGetRandomFilenameFromRootFolder() {
            var files = Directory.GetFiles(rootFolder);
            int index = Rand.Range(0, files.Length);
            return files[index];
        }

        public string RandomSnapshotFilename() {
            string filename = null;
            do {
                filename = DoGetRandomFilenameFromRootFolder();
                Log.Message(string.Format("found random file {0}", filename));
                if (!filename.EndsWith("xml")) {
                    File.Delete(filename);
                    filename = null;
                }
            } while (filename == null);
            return filename;
        }

        public int StoredSnapshotsCount() {
            return Directory.GetFiles(rootFolder).Count();
        }

        public List<string> FilterOutExistingItems(List<string> source) {
            List<string> result = new List<string>();

            foreach (string item in source) {
                if (!File.Exists(rootFolder + "/" + item)) {
                    result.Add(item);
                }
            }

            return result;
        }

    }
}
