using System.Reflection;
using RimWorld;
using Verse;

namespace RealRuins;

public static class Art_Extensions
{
	public static void InitializeArt(this CompArt art, string author, string title, string bakedTaleData)
	{
		FieldInfo field = art.GetType().GetField("taleRef", BindingFlags.Instance | BindingFlags.NonPublic);
		field.SetValue(art, new BakedTaleReference(bakedTaleData));
		FieldInfo field2 = art.GetType().GetField("titleInt", BindingFlags.Instance | BindingFlags.NonPublic);
		field2.SetValue(art, new TaggedString(title));
		FieldInfo field3 = art.GetType().GetField("authorNameInt", BindingFlags.Instance | BindingFlags.NonPublic);
		field3.SetValue(art, new TaggedString(author));
	}
}
