using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;
using Verse;

namespace RealRuins.Classes.Snapshotting
{
    class SnapshotStoreManager
    {
        public static SnapshotStoreManager instance = new SnapshotStoreManager();

        private string rootFolder = "../Snapshots";

        public SnapshotStoreManager() {
            if (!Directory.Exists(rootFolder)) {
                Directory.CreateDirectory(rootFolder);
            }
        }

        public void StoreData(byte[] buffer, string filename) {
            File.WriteAllBytes(rootFolder, buffer);
        }

        private string DoGetRandomFilenameFromRootFolder() {
            var files = Directory.GetFiles(rootFolder);
            int index = Rand.Range(0, files.Length);
            return files[index];
        }

        private string RandomSnapshotFilename() {
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

    }
}
