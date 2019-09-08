using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;
using RimWorld;
using RimWorld.BaseGen;

namespace RealRuins {
    abstract class AbstractDefenderForcesGenerator {
        public abstract void GenerateForces(Map map, ResolveParams rp); //called during map generation

        //There was some problem with pawns not created properly during basegen stack steps, but all working fine in genstep handler
        //Probably should investigate this later and combine all generation in one method
        public abstract void GenerateStartingParty(Map map, ResolveParams rp); 
    }
}
