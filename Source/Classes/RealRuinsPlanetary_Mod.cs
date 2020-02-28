using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;
using RimWorld;
using UnityEngine;


namespace RealRuins {
    public class RealRuinsPlanetary_Mod : Mod {

        Page_RealRuins embeddedPage;

        public RealRuinsPlanetary_Mod(ModContentPack content)
        : base(content) {
        }

        public override string SettingsCategory() {
            return "RealRuins.PlanetarySettignsCaption".Translate();
        }

        public override void DoSettingsWindowContents(Rect rect) {
            if (embeddedPage == null) {
                embeddedPage = new Page_RealRuins();
            }

            embeddedPage.DoWindowContents(rect, standalone: false);
        }
    }
}
