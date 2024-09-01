namespace RealRuins;

internal static class ArrayExtension
{
	public static void Blur(this float[,] map, int stepsCount)
	{
		int length = map.GetLength(0);
		int length2 = map.GetLength(1);
		float[,] array = new float[length, length2];
		for (int i = 0; i < stepsCount; i++)
		{
			for (int j = 1; j < length - 1; j++)
			{
				for (int k = 1; k < length2 - 1; k++)
				{
					array[j, k] = (map[j - 1, k - 1] + map[j, k - 1] + map[j + 1, k - 1] + map[j - 1, k] + map[j, k] + map[j + 1, k] + map[j - 1, k + 1] + map[j, k + 1] + map[j + 1, k + 1]) / 9f - map[j, k];
				}
			}
			for (int l = 1; l < length - 1; l++)
			{
				for (int m = 1; m < length2 - 1; m++)
				{
					if (map[l, m] < 1f)
					{
						map[l, m] += array[l, m];
					}
				}
			}
		}
	}
}
