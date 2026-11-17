using System;
using System.Collections.Generic;
using Verse;
using Verse.Grammar;

namespace RealRuins
{
    /// <summary>
    /// A LogEntry subclass that stores a pre-baked string rather than generating it from grammar.
    /// Used for inserting custom string-based log events into the battle log.
    /// </summary>
    public class BakedLogEntry : LogEntry
    {
        private string bakedString;
        private Pawn pawn;

        public BakedLogEntry()
        {
            // Default constructor for loading
        }

        public BakedLogEntry(string logString, Pawn pawn, long dateShift = 0) : base()
        {
            this.bakedString = logString;
            this.pawn = pawn;
            // Set timestamp: current in-game time minus the date shift to represent when the event occurred
            ticksAbs = Find.TickManager.TicksAbs - (int)dateShift;
            // base() is the loading constructor, which leaves the id at 0; the load id is made of the timestamp and this id
            logID = Find.UniqueIDsManager.GetNextLogID();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref bakedString, "bakedString", "");
            Scribe_References.Look(ref pawn, "pawn");

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                logID = DuplicateLoadIds.LogEntryId(ticksAbs, logID);
            }
        }

        protected override string ToGameStringFromPOV_Worker(Thing pov, bool forceLog)
        {
            // Return the baked string directly without grammar resolution
            return bakedString ?? "";
        }

        public override bool Concerns(Thing t)
        {
            return pawn == t;
        }

        public override IEnumerable<Thing> GetConcerns()
        {
            return new List<Thing> { pawn };
        }
    }
}
