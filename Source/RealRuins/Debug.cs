using System;
using System.Collections.Generic;
using Verse;

namespace RealRuins;

internal class Debug
{
	public const string Generic = "Generic";

	public const string Loader = "Loader";

	public const string Store = "Store";

	public const string BlueprintGen = "BlueprintGen";

	public const string BlueprintTransfer = "BlueprintTransfer";

	public const string Analyzer = "Analyzer";

	public const string POI = "POI";

	public const string Scatter = "Scatter";

	public const string PawnGen = "PawnGen";

	public const string ThingGen = "ThingGen";

	public const string Event = "Event";

	public const string ForceGen = "ForceGen";

	public const string QuestNode_Find = "QuestNode_Find";

	public static List<string> extras = new List<string> { "PawnGen", "ThingGen", "ForceGen" };

	public static bool active = true;

	public static int logLevel => RealRuins_ModSettings.logLevel;

	public static void Log(string format, params object[] args)
	{
		Log("Generic", format, args);
	}

	public static void Warning(string format, params object[] args)
	{
		Warning("Generic", format, args);
	}

	public static void Error(string format, params object[] args)
	{
		Error("Generic", format, args);
	}

	public static void SysLog(string format, params object[] args)
	{
		Message("System", format, args);
	}

	public static void Extra(string part, string format, params object[] args)
	{
		if (logLevel == 0 && extras.Contains(part))
		{
			Message(part, format, args);
		}
	}

	public static void Log(string part, string format, params object[] args)
	{
		if (logLevel == 0)
		{
			Message(part, format, args);
		}
	}

	public static void Warning(string part, string format, params object[] args)
	{
		if (logLevel < 2)
		{
			Message(part, format, args);
		}
	}

	public static void Error(string part, string format, params object[] args)
	{
		if (logLevel < 3)
		{
			Message(part, format, args);
		}
	}

	private static void Message(string part, string format, params object[] args)
	{
		if (!active)
		{
			return;
		}
		object[] array = new object[args.Length];
		for (int i = 0; i < args.Length; i++)
		{
			if (args[i] != null)
			{
				array[i] = args[i];
			}
			else
			{
				array[i] = "[NULL]";
			}
		}
		string text = "[RealRuins][" + part + "]: " + string.Format(format, array);
		Verse.Log.Message(text);
	}

	public static void PrintIntMap(int[,] map, string charMap = "#._23456789ABCDEFGHIJKLMNOPQRSTU", int delta = 0)
	{
		if (!active)
		{
			return;
		}
		string text = "============= INT MAP ============ \r\n";
		for (int i = 0; i < map.GetLength(0); i++)
		{
			for (int j = 0; j < map.GetLength(1); j++)
			{
				int num = map[i, j] + delta;
				text = ((num >= 0 && num < charMap.Length) ? (text + charMap[num]) : (text + "!"));
			}
			text += "\r\n";
		}
		Verse.Log.Message(text);
	}

	public static void PrintBoolMap(bool[,] map)
	{
		if (!active)
		{
			return;
		}
		string text = "============== BOOL MAP ============= \r\n";
		for (int i = 0; i < map.GetLength(0); i++)
		{
			for (int j = 0; j < map.GetLength(1); j++)
			{
				text = ((!map[i, j]) ? (text + " ") : (text + "#"));
			}
			text += "\r\n";
		}
		Verse.Log.Message(text);
	}

	public static void PrintNormalizedFloatMap(float[,] map, string charMap = " .,oO8")
	{
		if (!active)
		{
			return;
		}
		int length = charMap.Length;
		string text = "============== FLOAT MAP ============= \r\n";
		for (int i = 0; i < map.GetLength(0); i++)
		{
			for (int j = 0; j < map.GetLength(1); j++)
			{
				float num = map[i, j];
				int num2 = (int)Math.Round(num * (float)length);
				if (num2 >= charMap.Length)
				{
					num2 = charMap.Length - 1;
				}
				text += charMap[num2];
			}
			text += "\r\n";
		}
		Verse.Log.Message(text);
	}

	public static void PrintArray(object[] list)
	{
		string text = "";
		foreach (object obj in list)
		{
			text += obj.ToString();
			text += "\r\n";
		}
		Verse.Log.Message(text);
	}
}
