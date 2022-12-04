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
        configured,
        spawned
    }

    public enum SettleMode {
        normal, // arrive at ruins
        takeover, // start wil undamaged base
        attack // attack existing settlement
    }

    // Global context used to create new planetary-ruins enabled game
    class PlanetaryRuinsInitData {
        public static PlanetaryRuinsInitData shared = new PlanetaryRuinsInitData();

        public int selectedMapSize;
        public string selectedSeed;

        public PlanetaryRuinsState state;

        // If the user starts at POI we should keep it as a reference. It won't be a map parent, but will be used later
        public RealRuinsPOIWorldObject startingPOI;
        public SettleMode settleMode;

        public void Cleanup() {
            startingPOI = null;
            selectedMapSize = 0;
            selectedSeed = null;
            state = PlanetaryRuinsState.spawned;
        }
    }

    /*
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
    }*/
}
