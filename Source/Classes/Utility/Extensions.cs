using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RealRuins {
    static class ArrayExtension {
        public static void Blur(this float[,] map, int stepsCount) {
            int width = map.GetLength(0);
            int height = map.GetLength(1);
            float[,] delta = new float[width, height]; //delta integrity for making blur
            // - then blur the map to create gradient deterioration around the intact area.

            for (int steps = 0; steps < stepsCount; steps++) { //terrain map
                for (int x = 1; x < width - 1; x++) {
                    for (int z = 1; z < height - 1; z++) {
                        delta[x, z] = (map[x - 1, z - 1] + map[x, z - 1] + map[x + 1, z - 1] +
                            map[x - 1, z] + map[x, z] + map[x + 1, z] +
                            map[x - 1, z + 1] + map[x, z + 1] + map[x + 1, z + 1]) / 9.0f;
                    }
                }
                for (int x = 1; x < width - 1; x++) {
                    for (int z = 1; z < height - 1; z++) {
                        if (map[x, z] < 1) {
                            map[x, z] = delta[x, z] * (0* 0.1f);
                        }
                    }
                }
            }
        }
    }
}
