using RimWorld;
using Verse;

namespace RealRuins;

public class BakedTaleReference : TaleReference
{
	public TaggedString bakedTale;

	public BakedTaleReference()
	{
		bakedTale = "DeterioratedArtDescription".Translate();
	}

	public BakedTaleReference(string taleDescription)
	{
		bakedTale = new TaggedString(taleDescription);
	}

	public BakedTaleReference(TaggedString taleDescription)
	{
		bakedTale = taleDescription;
	}

	public new void ExposeData()
	{
		Scribe_Values.Look(ref bakedTale, "bakedTale", "DeterioratedArtDescription".Translate());
	}
}
