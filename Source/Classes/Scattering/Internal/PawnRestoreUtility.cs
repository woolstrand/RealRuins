using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

using RimWorld;
using Verse;

/**
 * This class handles restoration of pawns from saved XML data.
 * It reconstructs pawn properties including name, age, skills, health, and apparel.
 * It gracefully handles missing definitions and data corruption.
 */

namespace RealRuins
{
    class PawnRestoreUtility
    {
        private const long ticksInYear = 3600000;

        private long dateShift;
        private Faction faction;

        public PawnRestoreUtility(long dateShift, Faction faction)
        {
            this.dateShift = dateShift;
            this.faction = faction;
        }

        /// <summary>
        /// Creates a pawn from raw XML data extracted from a saved pawn.
        /// Handles missing definitions gracefully, restoring what's possible.
        /// </summary>
        public Pawn MakePawnWithRawXml(string xml)
        {
            try
            {
                XmlDocument document = new XmlDocument();
                document.LoadXml(xml);
                XmlNode root = document.FirstChild;

                string pawnKind = root.SelectSingleNode("kind").InnerText;
                PawnKindDef kindDef = PawnKindDef.Named(pawnKind);
                if (kindDef == null)
                {
                    kindDef = PawnKindDefOf.AncientSoldier;
                }

                Pawn p = PawnGenerator.GeneratePawn(kindDef, faction);

                RestorePawnName(root, p);
                RestorePawnGender(root, p);
                RestorePawnAge(root, p);
                RestorePawnHealth(root, p);
                RestorePawnApparel(root, p);
                RestorePawnStory(root, p);
                // Skills are temporarily disabled in original code

                return p;
            }
            catch (Exception e)
            {
                Debug.Log(Debug.BlueprintPawnDecoder, "Pawn decoding failed: {0}", e);
                return null;
            }
        }

        private void RestorePawnName(XmlNode root, Pawn p)
        {
            try
            {
                Name name = null;
                var nameNode = root.SelectSingleNode("name");
                if (nameNode != null)
                {
                    var attrFirst = nameNode.Attributes.GetNamedItem("first");
                    var attrLast = nameNode.Attributes.GetNamedItem("last");
                    var attrNick = nameNode.Attributes.GetNamedItem("nick");
                    if (attrFirst != null && attrLast != null)
                    {
                        name = new NameTriple(attrFirst.Value, attrNick?.Value ?? "", attrLast.Value);
                    }
                    else
                    {
                        name = new NameSingle(p.kindDef.LabelCap);
                    }
                    p.Name = name;
                }
            }
            catch (Exception ex)
            {
                Debug.Extra(Debug.BlueprintPawnDecoder, "Failed to restore pawn name: {0}", ex.Message);
            }
        }

        private void RestorePawnGender(XmlNode root, Pawn p)
        {
            try
            {
                string gender = root.SelectSingleNode("gender")?.InnerText;
                if (gender == "Male")
                {
                    p.gender = Gender.Male;
                }
                else if (gender == "Female")
                {
                    p.gender = Gender.Female;
                }
            }
            catch (Exception ex)
            {
                Debug.Extra(Debug.BlueprintPawnDecoder, "Failed to restore pawn gender: {0}", ex.Message);
            }
        }

        private void RestorePawnAge(XmlNode root, Pawn p)
        {
            try
            {
                string bioAgeString = root.SelectSingleNode("biologicalAge")?.InnerText;
                string chronoAgeString = root.SelectSingleNode("chronologicalAge")?.InnerText;
                if (bioAgeString != null && chronoAgeString != null)
                {
                    long result = 0;
                    Int64.TryParse(bioAgeString, out result);
                    p.ageTracker.AgeBiologicalTicks = result;
                    Int64.TryParse(chronoAgeString, out result);
                    p.ageTracker.AgeChronologicalTicks = result + ticksInYear * (-dateShift);
                }
            }
            catch (Exception ex)
            {
                Debug.Extra(Debug.BlueprintPawnDecoder, "Failed to restore pawn age: {0}", ex.Message);
            }
        }

        private void RestorePawnHealth(XmlNode root, Pawn p)
        {
            try
            {
                var healthNode = root.SelectSingleNode("saveable[@Class='Pawn_HealthTracker']");
                if (healthNode == null)
                    return;

                XmlNode healthState = healthNode.SelectSingleNode("healthState");
                if (healthState?.InnerText == "Dead")
                {
                    p.health.SetDead();
                }

                XmlNodeList hediffsList = healthNode.SelectNodes("hediffSet/hediffs/li");
                if (hediffsList != null)
                {
                    Scribe.mode = LoadSaveMode.LoadingVars;
                    p.health?.hediffSet?.hediffs?.RemoveAll(_ => true);

                    foreach (XmlNode hediffNode in hediffsList)
                    {
                        try
                        {
                            if (!ValidateAndFixHediffNode(hediffNode, p))
                            {
                                Debug.Extra(Debug.BlueprintPawnDecoder, "Skipping hediff due to validation failure: {0}", hediffNode.SelectSingleNode("def")?.InnerText ?? "unknown");
                                continue;
                            }

                            // Extract combat log message if present before deserialization
                            string combatLogMessage = hediffNode.SelectSingleNode("combatLogText")?.InnerText;

                            Hediff hediff = ScribeExtractor.SaveableFromNode<Hediff>(hediffNode, null);
                            if (hediff != null && hediff.Part != null && !p.health.hediffSet.PartIsMissing(hediff.Part))
                            {
                                p.health.AddHediff(hediff);
                            }

                            // Add the combat log message to the pawn's combat log if present. even for missing parts.
                            if (!string.IsNullOrEmpty(combatLogMessage))
                            {
                                Debug.Extra(Debug.BlueprintPawnDecoder, "Trying to add combat log message: {0}", combatLogMessage);
                                AddCombatLogMessage(p, combatLogMessage, hediff);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.Extra(Debug.BlueprintPawnDecoder, "Failed to restore hediff {0}: {1}", hediffNode.SelectSingleNode("def")?.InnerText ?? "unknown", ex.Message);
                        }
                    }
                    Scribe.mode = LoadSaveMode.Inactive;
                }
            }
            catch (Exception ex)
            {
                Debug.Extra(Debug.BlueprintPawnDecoder, "Failed to restore pawn health: {0}", ex.Message);
            }
        }

        private void RestorePawnApparel(XmlNode root, Pawn p)
        {
            try
            {
                var apparelNode = root.SelectSingleNode("apparel");
                if (apparelNode == null)
                    return;

                XmlNodeList apparelList = apparelNode.SelectNodes("item");
                foreach (XmlNode item in apparelList)
                {
                    string defName = item.Attributes?.GetNamedItem("def")?.Value;
                    string stuffDefName = item.Attributes?.GetNamedItem("stuffDef")?.Value;

                    ThingDef stuffDef = null;
                    ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                    if (stuffDefName != null)
                    {
                        stuffDef = DefDatabase<ThingDef>.GetNamedSilentFail(stuffDefName);
                    }

                    if (thingDef != null)
                    {
                        Apparel apparel = (Apparel)ThingMaker.MakeThing(thingDef, stuffDef);
                        apparel.HitPoints = Rand.Range(1, (int)(apparel.MaxHitPoints * 0.6));
                        if (apparel is Apparel)
                        {
                            p.apparel.Wear(apparel, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Extra(Debug.BlueprintPawnDecoder, "Failed to restore pawn apparel: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Restores pawn story and appearance from saved XML.
        /// Currently mostly disabled (commented) pending future implementation.
        /// </summary>
        private void RestorePawnStory(XmlNode root, Pawn p)
        {
            try
            {
                var storyNode = root.SelectSingleNode("saveable[@Class='Pawn_StoryTracker']");
                if (storyNode == null)
                    return;

                /*
                Backstory bs = null;
                string childhoodDef = storyNode.SelectSingleNode("childhood")?.InnerText;
                if (BackstoryDatabase.TryGetWithIdentifier(childhoodDef, out bs)) {
                    p.story.childhood = bs;
                }
                string adulthoodDef = storyNode.SelectSingleNode("adulthood")?.InnerText;
                if (BackstoryDatabase.TryGetWithIdentifier(adulthoodDef, out bs)) {
                    p.story.adulthood = bs;
                }

                string bodyTypeDefName = storyNode.SelectSingleNode("bodyType")?.InnerText;
                if (bodyTypeDefName != null) {
                    BodyTypeDef def = DefDatabase<BodyTypeDef>.GetNamedSilentFail(bodyTypeDefName);
                    if (def != null) { p.story.bodyType = def; }

                    try {
                        string crownTypeName = storyNode.SelectSingleNode("crownType")?.InnerText;
                        p.story.crownType = (CrownType)Enum.Parse(typeof(CrownType), crownTypeName);
                    } catch (Exception) { }

                    string hairDefName = storyNode.SelectSingleNode("hairDef")?.InnerText;
                    HairDef hairDef = DefDatabase<HairDef>.GetNamedSilentFail(hairDefName);
                    if (hairDef != null) { p.story.hairDef = hairDef; }

                    float melanin = 0;
                    if (float.TryParse(storyNode.SelectSingleNode("melanin")?.InnerText, out melanin)) {
                        p.story.melanin = melanin;
                    }

                    string hairColorString = storyNode.SelectSingleNode("hairColor")?.InnerText;
                    Color hairColor = (Color)ParseHelper.FromString(hairColorString, typeof(Color));
                    if (hairColor != null) {
                        p.story.hairColor = hairColor;
                    }
                }
                XmlNodeList traitsList = storyNode.SelectNodes("traits/allTraits/li");
                if (traitsList != null) {
                    p.story.traits.allTraits.RemoveAll(_ => true);
                    foreach (XmlNode traitNode in traitsList) {
                        string traitDefName = traitNode.SelectSingleNode("def")?.InnerText;
                        int traitDegree = 0;
                        int.TryParse(traitNode.SelectSingleNode("degree")?.InnerText, out traitDegree);

                        TraitDef traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(traitDefName);
                        if (traitDef == null) continue;

                        Trait t = new Trait(traitDef, traitDegree);
                        if (t == null) continue;

                        p.story.traits.allTraits.Add(t);
                    }
                }
                */
            }
            catch (Exception ex)
            {
                Debug.Extra(Debug.BlueprintPawnDecoder, "Failed to restore pawn story: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Validates and fixes hediff XML node to ensure all references are valid.
        /// Removes invalid references, replaces missing sources with defaults.
        /// Returns false if hediff should be skipped entirely (e.g., prosthetic for missing body part).
        /// </summary>
        private bool ValidateAndFixHediffNode(XmlNode hediffNode, Pawn pawn)
        {
            try
            {
                string hediffDefName = hediffNode.SelectSingleNode("def")?.InnerText;
                if (string.IsNullOrEmpty(hediffDefName))
                {
                    Debug.Extra(Debug.BlueprintPawnDecoder, "Hediff has no def, skipping");
                    return false;
                }

                HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(hediffDefName);
                if (hediffDef == null)
                {
                    Debug.Extra(Debug.BlueprintPawnDecoder, "Hediff def not found: {0}, skipping", hediffDefName);
                    return false;
                }

                // Validate hediff class exists
                var classAttr = hediffNode.Attributes?.GetNamedItem("Class");
                if (classAttr != null && !string.IsNullOrEmpty(classAttr.Value))
                {
                    Type hediffType = GenTypes.GetTypeInAnyAssembly(classAttr.Value);
                    if (hediffType == null)
                    {
                        Debug.Extra(Debug.BlueprintPawnDecoder, "Hediff {0} has missing class {1}, skipping", hediffDefName, classAttr.Value);
                        return false;
                    }
                }

                // Validate and fix body part reference
                var partNode = hediffNode.SelectSingleNode("part");
                if (partNode != null)
                {
                    string partDefName = partNode.SelectSingleNode("def")?.InnerText;
                    if (!string.IsNullOrEmpty(partDefName))
                    {
                        BodyPartDef partDef = DefDatabase<BodyPartDef>.GetNamedSilentFail(partDefName);
                        if (partDef == null)
                        {
                            Debug.Extra(Debug.BlueprintPawnDecoder, "Hediff {0} references missing body part {1}, skipping", hediffDefName, partDefName);
                            return false;
                        }

                        // Check if pawn actually has this body part in its current health state
                        // Use GetNotMissingParts() to find existing parts, not the race definition
                        BodyPartRecord actualPart = FindBodyPartRecord(pawn, partDef);
                        if (actualPart == null)
                        {
                            string availableParts = string.Join(", ", pawn.health.hediffSet.GetNotMissingParts().Select(p => p.def.defName));
                            Debug.Extra(Debug.BlueprintPawnDecoder, "Hediff {0} references body part {1} pawn doesn't have. Available: {2}", 
                                hediffDefName, partDefName, availableParts);
                            return false;
                        }
                    }
                }

                // Validate and fix source reference (weapon that caused injury, etc.)
                var sourceNode = hediffNode.SelectSingleNode("source");
                if (sourceNode != null && !string.IsNullOrEmpty(sourceNode.InnerText))
                {
                    ThingDef sourceDef = DefDatabase<ThingDef>.GetNamedSilentFail(sourceNode.InnerText);
                    if (sourceDef == null)
                    {
                        Debug.Extra(Debug.BlueprintPawnDecoder, "Hediff {0} has missing source {1}, removing source reference", hediffDefName, sourceNode.InnerText);
                        hediffNode.RemoveChild(sourceNode);
                    }
                }

                // Validate nested hediff effects/stages
                ValidateAndFixNestedHediffs(hediffNode, pawn);

                return true;
            }
            catch (Exception ex)
            {
                Debug.Extra(Debug.BlueprintPawnDecoder, "Exception during hediff validation: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Finds a body part in the pawn's current body.
        /// Searches through all existing parts including nested children.
        /// </summary>
        private BodyPartRecord FindBodyPartRecord(Pawn pawn, BodyPartDef partDef)
        {
            if (pawn?.health?.hediffSet == null) return null;

            foreach (var part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.def == partDef)
                {
                    return part;
                }
            }

            return null;
        }

        /// <summary>
        /// Validates references within implant/addon nodes (e.g., prosthetics).
        /// Removes any that reference missing definitions.
        /// </summary>
        private void ValidateAndFixImplantReferences(XmlNode hediffNode, HediffDef hediffDef, Pawn pawn)
        {
            try
            {
                // Check for implant type references
                var implantTypeNode = hediffNode.SelectSingleNode("implantType");
                if (implantTypeNode != null && !string.IsNullOrEmpty(implantTypeNode.InnerText))
                {
                    ThingDef implantDef = DefDatabase<ThingDef>.GetNamedSilentFail(implantTypeNode.InnerText);
                    if (implantDef == null)
                    {
                        Debug.Extra(Debug.BlueprintPawnDecoder, "Hediff {0} references missing implant {1}, removing", hediffDef.defName, implantTypeNode.InnerText);
                        hediffNode.RemoveChild(implantTypeNode);
                    }
                }

                // Check for addon references
                var addonsNode = hediffNode.SelectSingleNode("addons");
                if (addonsNode != null)
                {
                    XmlNodeList addonsList = addonsNode.SelectNodes("li");
                    List<XmlNode> invalidsToRemove = new List<XmlNode>();
                    foreach (XmlNode addonNode in addonsList)
                    {
                        string addonDefName = addonNode.SelectSingleNode("def")?.InnerText;
                        if (!string.IsNullOrEmpty(addonDefName))
                        {
                            ThingDef addonDef = DefDatabase<ThingDef>.GetNamedSilentFail(addonDefName);
                            if (addonDef == null)
                            {
                                Debug.Extra(Debug.BlueprintPawnDecoder, "Addon {0} not found, removing", addonDefName);
                                invalidsToRemove.Add(addonNode);
                            }
                        }
                    }
                    foreach (var invalidNode in invalidsToRemove)
                    {
                        addonsNode.RemoveChild(invalidNode);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Extra(Debug.BlueprintPawnDecoder, "Exception during implant validation: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Validates nested hediffs (effects, stages with embedded hediff data).
        /// Removes references to missing definitions.
        /// </summary>
        private void ValidateAndFixNestedHediffs(XmlNode hediffNode, Pawn pawn)
        {
            try
            {
                // Check severityStage nodes that may contain nested hediffs or thing references
                var severityStagesNode = hediffNode.SelectSingleNode("stages");
                if (severityStagesNode != null)
                {
                    XmlNodeList stagesList = severityStagesNode.SelectNodes("li");
                    foreach (XmlNode stageNode in stagesList)
                    {
                        // Remove any references to things that don't exist
                        var hediffEffectsNode = stageNode.SelectSingleNode("hediffGivers");
                        if (hediffEffectsNode != null)
                        {
                            XmlNodeList effectsList = hediffEffectsNode.SelectNodes("li");
                            List<XmlNode> invalidsToRemove = new List<XmlNode>();
                            foreach (XmlNode effectNode in effectsList)
                            {
                                string effectDefName = effectNode?.InnerText;
                                if (!string.IsNullOrEmpty(effectDefName))
                                {
                                    HediffDef effectDef = DefDatabase<HediffDef>.GetNamedSilentFail(effectDefName);
                                    if (effectDef == null)
                                    {
                                        Debug.Extra(Debug.BlueprintPawnDecoder, "Stage effect {0} not found, removing", effectDefName);
                                        invalidsToRemove.Add(effectNode);
                                    }
                                }
                            }
                            foreach (var invalidNode in invalidsToRemove)
                            {
                                hediffEffectsNode.RemoveChild(invalidNode);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Extra(Debug.BlueprintPawnDecoder, "Exception during nested hediff validation: {0}", ex.Message);
            }
        }

        private void AddCombatLogMessage(Pawn pawn, string combatLogText, Hediff hediff)
        {
            try
            {
                var logEntry = new BakedLogEntry(combatLogText, pawn, dateShift);
                Find.BattleLog.Add(logEntry);
                hediff.combatLogEntry = new Verse.WeakReference<LogEntry>(logEntry);
            }
            catch (Exception ex)
            {
                Debug.Extra(Debug.BlueprintPawnDecoder, "Exception adding combat log message: {0}", ex.Message);
            }
        }
    }
}
