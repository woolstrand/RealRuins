using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace RealRuins {
    class RuinedBaseSite : Site {

        //list of all triggers on the map. don't know if it does really influence performance, but it definitely does not make it worse.
        List<RaidTrigger> triggersCache;

        //convenience bool used to check if we can start running final countdown timer (and it also tells we should not check triggers anymore)
        bool allTriggersFired = false;

        public override void PostMapGenerate() {
            base.PostMapGenerate();
            BuildRaidTriggersCache();
            Debug.Message("Built cache, cache has size of {0}", triggersCache.Count);
        }

        private void BuildRaidTriggersCache() {
            triggersCache = (List<RaidTrigger>)Map.spawnedThings.Where((thing) => thing is RaidTrigger);
        }

        private void CheckTriggers() {
            foreach (RaidTrigger raidTrigger in triggersCache) {
                if (raidTrigger.IsTriggered() == false) return;
            }
            allTriggersFired = true;

        }

        public override void ExposeData() {
            base.ExposeData();

            BuildRaidTriggersCache();
        }

        public override void Tick() {
            base.Tick();

            if (!RealRuins_ModSettings.allowInstantCaravanReform) {
                if (!allTriggersFired) {
                    CheckTriggers();
                } else {
                    if (!GenHostility.AnyHostileActiveThreatToPlayer(base.Map)) {
                        RuinedBaseComp comp = GetComponent<RuinedBaseComp>();
                        if (comp.unlockTargetTime == -1) {
                            comp.unlockTargetTime = Find.TickManager.TicksGame + Rand.Range(100, 1000);
                            Debug.Message("no more hostiles. time: {0}, unlocktime: {1}", Find.TickManager.TicksGame, comp.unlockTargetTime);
                        } else if (Find.TickManager.TicksGame > comp.unlockTargetTime) {
                            string text = "RealRuins.NoMoreEnemies".Translate();
                            Messages.Message(text, this, MessageTypeDefOf.PositiveEvent);
                            comp.mapExitLocked = false;
                        }
                    }
                }
            }
        }
    }
}
