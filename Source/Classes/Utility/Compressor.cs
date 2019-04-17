using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;

namespace RealRuins {
    class Compressor {
        //Dangerous zipping: if somethig goes wrong the information is lost, but we don't care too.
        public static void ZipFile(string filename) {
            var bytes = File.ReadAllBytes(filename);
            File.Delete(filename);

            using (var msi = new MemoryStream(bytes))
            using (var mso = File.Open(filename, FileMode.Create)) {
                mso.Seek(0, SeekOrigin.Begin);
                using (var gs = new GZipStream(mso, CompressionMode.Compress)) {
                    CopyTo(msi, gs);
                }
            }
        }

        public static string UnzipFile(string filename) {
            using (var msi = File.OpenRead(filename))
            using (var mso = new MemoryStream()) {
                using (var gs = new GZipStream(msi, CompressionMode.Decompress)) {
                    CopyTo(gs, mso);
                }

                return Encoding.UTF8.GetString(mso.ToArray());
            }
        }

        public static void CopyTo(Stream src, Stream dest) {
            byte[] bytes = new byte[4096];

            int cnt;

            while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0) {
                dest.Write(bytes, 0, cnt);
            }
        }
    }
}
