using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RealRuins {

    class ItemArt {
        public string author = "Unknown";
        public string title = "Unknown";
        public string text = "";

        public string TextWithDatesShiftedBy(int shift) {
            string initialText = text;
            try {
                Match match = Regex.Match(initialText, "[5-9]\\d\\d\\d");
                string yearString = match.Value;
                if (yearString == null) return initialText;
                int resultingYear = int.Parse(yearString) + shift;
                return initialText.Replace(yearString, resultingYear.ToString());
            } catch (Exception) {
                return initialText;
            }
        }
    }
}
