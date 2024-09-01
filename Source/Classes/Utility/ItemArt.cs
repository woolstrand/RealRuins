using System;
using System.Text.RegularExpressions;

namespace RealRuins;

internal class ItemArt
{
	public string author = "Unknown";

	public string title = "Unknown";

	public string text = "";

	public string TextWithDatesShiftedBy(int shift)
	{
		string text = this.text;
		try
		{
			Match match = Regex.Match(text, "[5-9]\\d\\d\\d");
			string value = match.Value;
			if (value == null)
			{
				return text;
			}
			return text.Replace(value, (int.Parse(value) + shift).ToString());
		}
		catch (Exception)
		{
			return text;
		}
	}
}
