using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RealRuins {
    // Everything a single scattering run needs, passed down the pipeline instead of reading static state.
    // Create one per RuinsScatterer.Scatter call and keep it if you need to read the results afterwards.
    class ScatterContext {
        // What to generate: mod options merged with per-scatterer overrides (see ScatterOptionsFactory).
        // This is a private copy, so the pipeline can never change the source it was built from.
        public readonly ScatterOptions options;

        // Global mod options which apply the same way to every scatterer (blacklists, force multiplier, etc).
        public readonly PipelineSettings settings;

        // Data produced during the run. Some of it is read back by the caller (e.g. uncovered cost for starting parties).
        public readonly ScatterState state = new ScatterState();

        public ScatterContext(ScatterOptions options, PipelineSettings settings) {
            this.options = options.Copy();
            this.settings = settings;
        }
    }
}
