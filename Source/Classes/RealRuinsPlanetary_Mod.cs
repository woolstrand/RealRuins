using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;
using RimWorld;
using UnityEngine;


namespace RealRuins {
    public enum PlanetaryRuinsState {
        disabled,
        configuring,
        configured
    }
    public class PlanetaryRuinsInitData {
        public static PlanetaryRuinsInitData shared = new PlanetaryRuinsInitData();

        public int selectedMapSize;
        public string selectedSeed;

        public PlanetaryRuinsState state;
    }

    public class RealRuinsPlanetary_Mod : Mod {

        Page_PlanetaryRuinsLoader embeddedPage;

        public RealRuinsPlanetary_Mod(ModContentPack content)
        : base(content) {
        }

        public override string SettingsCategory() {
            return "RealRuins.PlanetarySettignsCaption".Translate();
        }

        public override void DoSettingsWindowContents(Rect rect) {
            if (embeddedPage == null) {
                embeddedPage = new Page_PlanetaryRuinsLoader();
            }

            embeddedPage.DoWindowContents(rect, standalone: false);
        }
    }
}
