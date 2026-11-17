using System;

namespace RealRuins {
    // The wealth all regular ruin chunks of one map may share (RealRuins_ModSettings.ruinsWealthPerMap).
    // Chunks are scattered one by one; each gets what is left minus a small reserve for every chunk still to come,
    // so a single rich blueprint can't leave the rest of the map empty, and most maps never reach the limit at all.
    class RuinsWealthBudget {
        // Reserve per chunk still to come: a typical chunk is worth a few hundred, see the wealth sampler (median chunk ~1000 before the limit).
        private const float maxReservePerChunk = 500;

        public readonly float total;
        private readonly float reservePerChunk;
        private int chunksLeft;
        public float spent { get; private set; }

        public RuinsWealthBudget(float total, int chunksCount) {
            this.total = total;
            chunksLeft = chunksCount;
            reservePerChunk = Math.Min(maxReservePerChunk, total / Math.Max(1, chunksCount) / 2);
        }

        // Allowance for the next chunk
        public float NextAllowance() {
            return Math.Max(0, total - spent - reservePerChunk * Math.Max(0, chunksLeft - 1));
        }

        // Records what a chunk actually placed on the map
        public void Spend(float cost) {
            spent += cost;
            chunksLeft--;
        }
    }
}
