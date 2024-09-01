using System;
using System.Collections.Generic;
using RimWorld.BaseGen;
using Verse;

namespace RealRuins;

internal class RuinsScatterer
{
	public static void Scatter(ResolveParams rp, ScatterOptions options, CoverageMap coverage, List<AbstractDefenderForcesGenerator> generators)
	{
		Debug.Log("Scatter", "Running stand-alone scatterer");
		if (options == null)
		{
			Debug.Warning("Scatter", "Scatter options are null, aborting");
			return;
		}
		DateTime utcNow = DateTime.UtcNow;
		Debug.Log("Scatter", "[0 s]: Loading blueprint of size {0} - {1} to deploy at {2}, {3}", options.minRadius, options.maxRadius, rp.rect.minX, rp.rect.minZ);
		Blueprint blueprint = null;
		Map map = BaseGen.globalSettings.map;
		string filename = options.blueprintFileName;
		if (string.IsNullOrEmpty(filename) || !BlueprintLoader.CanLoadBlueprintAtPath(filename))
		{
			blueprint = BlueprintFinder.FindRandomBlueprintWithParameters(out filename, options.minimumAreaRequired, options.minimumDensityRequired, 15, 5, options.deleteLowQuality);
			if (string.IsNullOrEmpty(filename))
			{
				Debug.Warning("Scatter", "Blueprint name was null and could not find another suitable blueprint, skipping");
				return;
			}
		}
		if (!options.shouldLoadPartOnly)
		{
			if (blueprint == null)
			{
				blueprint = BlueprintLoader.LoadWholeBlueprintAtPath(filename);
			}
		}
		else
		{
			int num = Rand.Range(options.minRadius, options.maxRadius);
			blueprint = ((blueprint != null) ? blueprint.RandomPartCenteredAtRoom(new IntVec3(num, 0, num)) : BlueprintLoader.LoadWholeBlueprintAtPath(filename).RandomPartCenteredAtRoom(new IntVec3(num, 0, num)));
		}
		if (blueprint == null)
		{
			Debug.Warning("Scatter", "Blueprint is still null after attempting to load any qualifying, returning");
			return;
		}
		Debug.Extra("Scatter", "Blueprint loaded, cutting and searching for rooms");
		blueprint.CutIfExceedsBounds(map.Size);
		BlueprintPreprocessor.ProcessBlueprint(blueprint, options);
		blueprint.FindRooms();
		options.roomMap = blueprint.wallMap;
		Debug.Extra("Scatter", "Rooms traversed, initializing transfer utility");
		BlueprintTransferUtility blueprintTransferUtility = new BlueprintTransferUtility(blueprint, map, rp, options);
		Debug.Extra("Scatter", "Initialized, removing incompatible items...");
		blueprintTransferUtility?.RemoveIncompatibleItems();
		Debug.Extra("Scatter", "Recalculating stats...");
		blueprint.UpdateBlueprintStats(includeCost: true);
		Debug.Extra("Scatter", "Deteriorating...");
		DeteriorationProcessor.Process(blueprint, options);
		Debug.Extra("Scatter", "Scavenging...");
		ScavengingProcessor scavengingProcessor = new ScavengingProcessor();
		scavengingProcessor.RaidAndScavenge(blueprint, options);
		Debug.Extra("Scatter", "[{0} s] Prepared, about to start transferring.", DateTime.UtcNow.Subtract(utcNow).TotalSeconds);
		try
		{
			blueprintTransferUtility.Transfer(coverage);
			Debug.Extra("Scatter", "[{0} s] Transferred.", DateTime.UtcNow.Subtract(utcNow).TotalSeconds);
		}
		catch (Exception ex)
		{
			Debug.Error("BlueprintTransfer", "Failed to transfer blueprint due to {0}", ex);
		}
		if (generators != null)
		{
			foreach (AbstractDefenderForcesGenerator generator in generators)
			{
				try
				{
					generator.GenerateForces(map, rp, options);
					Debug.Log("Scatter", "[{0} s] Generated forces for a generator.", DateTime.UtcNow.Subtract(utcNow).TotalSeconds);
				}
				catch (Exception ex2)
				{
					Debug.Error("BlueprintTransfer", "Failed to generate forces: {0}", ex2);
				}
			}
		}
		if (options.shouldAddFilth)
		{
			try
			{
				blueprintTransferUtility.AddFilthAndRubble();
			}
			catch (Exception ex3)
			{
				Debug.Warning("BlueprintTransfer", "Failed to add filth and rubble: {0}", ex3);
			}
		}
		Debug.Extra("Scatter", "[{0} s] Spiced up with rubble. Completed.", DateTime.UtcNow.Subtract(utcNow).TotalSeconds);
		Debug.Log("Scatter", "Chunk scattering finished, moving");
	}
}
