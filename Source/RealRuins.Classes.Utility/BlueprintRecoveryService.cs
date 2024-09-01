using System.IO;
using System.Xml;

namespace RealRuins.Classes.Utility;

public class BlueprintRecoveryService
{
	private string path;

	public BlueprintRecoveryService(string path)
	{
		this.path = path;
	}

	public bool TryRecoverInPlace()
	{
		string text = File.ReadAllText(path);
		int num = text.LastIndexOf("</cell>");
		if (num < text.Length - 7)
		{
			text = text.Substring(0, num);
			text += "</cell>";
		}
		text += "</snapshot>";
		File.WriteAllText(path, text);
		XmlDocument xmlDocument = new XmlDocument();
		try
		{
			xmlDocument.Load(path);
		}
		catch
		{
			return false;
		}
		return true;
	}
}
