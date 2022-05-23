using System;
using System.Xml;
using System.IO;

namespace RealRuins.Classes.Utility
{
    public class BlueprintRecoveryService
    {
        private string path;

        public BlueprintRecoveryService(string path)
        {
            this.path = path;
        }

        public bool TryRecoverInPlace()
        {
            string data = File.ReadAllText(this.path);

            // cutting anything beyond last tag bracket
            int cutLocation = data.LastIndexOf("</cell>");
            if (cutLocation < data.Length - 7)
            {
                data = data.Substring(0, cutLocation);
                data += "</cell>";
            }

            data += "</snapshot>";

            File.WriteAllText(this.path, data);
            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(this.path);
            }
            catch
            {
                return false;
            }

            return true;
        }

    }
}
