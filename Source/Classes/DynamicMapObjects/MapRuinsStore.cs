using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld.Planet;
using Verse;

namespace RealRuins {
    public class MapRuinsStore : WorldComponent {

        public List<string> remoteMapIds = new List<string>();
        public string blueprintsFolder = Guid.NewGuid().ToString();

        public MapRuinsStore(World world)
            : base(world) {
        }

        public override void ExposeData() {
            Scribe_Collections.Look(ref remoteMapIds, "remoteMapIds", LookMode.Undefined);
            Scribe_Values.Look(ref blueprintsFolder, "blueprintsFolder", Guid.NewGuid().ToString(), true);
        }

    }
}
