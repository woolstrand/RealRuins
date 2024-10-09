using System.IO;
using System.Text.RegularExpressions;

namespace RealRuins;

internal static class StringExtension
{
	public static string SanitizeForFileSystem(this string filename)
	{
		string str = new string(Path.GetInvalidFileNameChars());
		string arg = Regex.Escape(str);
		string pattern = string.Format("([{0}]*\\.+$)|([{0}]+)", arg);
		return Regex.Replace(filename, pattern, "_");
	}
}
