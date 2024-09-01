using System.Reflection;

namespace RealRuins;

internal class BlueprintAnalyzerResult
{
	public int totalArea;

	public int itemsCount;

	public int occupiedTilesCount;

	public int haulableItemsCount;

	public int haulableStacksCount;

	public int itemsInside;

	public int militaryItemsCount;

	public float totalItemsCost;

	public float haulableItemsCost;

	public int wallLength;

	public int roomsCount;

	public int internalArea;

	public int defensiveItemsCount;

	public int mannableCount;

	public int productionItemsCount;

	public int bedsCount;

	public override string ToString()
	{
		string text = "Analyzer result:\r\n";
		FieldInfo[] fields = GetType().GetFields();
		FieldInfo[] array = fields;
		foreach (FieldInfo fieldInfo in array)
		{
			text = text + fieldInfo.Name + ": " + fieldInfo.GetValue(this)?.ToString() + "\r\n";
		}
		return text;
	}
}
