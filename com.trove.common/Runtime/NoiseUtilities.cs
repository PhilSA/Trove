using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace Trove
{
    public static class NoiseUtilities
    {
        public static float Perlin1D<T>(float x, T randomSlopes, int indexOffset = 0) where T : unmanaged, IIndexable<float>, INativeList<float>
        {
            int floor = (int)math.floor(x);
            float distToFloor = x - floor;
            floor = (floor + indexOffset) % randomSlopes.Length;
            floor = math.select(floor, floor + randomSlopes.Length, floor < 0);
            int ceil = floor + 1;
            ceil = math.select(ceil, ceil - randomSlopes.Length, ceil >= randomSlopes.Length);

            float floorSlope = randomSlopes[floor];
            float ceilSlope = randomSlopes[ceil];
            float floorPos = floorSlope * distToFloor;
            float ceilPos = -ceilSlope * (1f - distToFloor);

            float u = distToFloor * distToFloor * (3f - (2f * distToFloor));
            return (floorPos * (1f - u)) + (ceilPos * u);
        }

        public static void InitRandomSlopes(ref Random random, ref FixedList32Bytes<float> randomSlopes)
        {
            randomSlopes.Clear();
            for (int i = 0; i < randomSlopes.Capacity; i++)
            {
                randomSlopes.Add(random.NextFloat(-1f, 1f));
            }
        }

        public static void InitRandomSlopes(ref Random random, ref FixedList64Bytes<float> randomSlopes)
        {
            randomSlopes.Clear();
            for (int i = 0; i < randomSlopes.Capacity; i++)
            {
                randomSlopes.Add(random.NextFloat(-1f, 1f));
            }
        }

        public static void InitRandomSlopes(ref Random random, ref FixedList128Bytes<float> randomSlopes)
        {
            randomSlopes.Clear();
            for (int i = 0; i < randomSlopes.Capacity; i++)
            {
                randomSlopes.Add(random.NextFloat(-1f, 1f));
            }
        }

        public static void InitRandomSlopes(ref Random random, ref FixedList512Bytes<float> randomSlopes)
        {
            randomSlopes.Clear();
            for (int i = 0; i < randomSlopes.Capacity; i++)
            {
                randomSlopes.Add(random.NextFloat(-1f, 1f));
            }
        }

        public static void GetRandomSlopes(ref Random random, ref FixedList32Bytes<float> randomSlopes)
        {
            for (int i = 0; i < randomSlopes.Length; i++)
            {
                randomSlopes[i] = random.NextFloat(-1f, 1f);
            }
        }

        public static void GetRandomSlopes(ref Random random, ref FixedList64Bytes<float> randomSlopes)
        {
            for (int i = 0; i < randomSlopes.Length; i++)
            {
                randomSlopes[i] = random.NextFloat(-1f, 1f);
            }
        }

        public static void GetRandomSlopes(ref Random random, ref FixedList128Bytes<float> randomSlopes)
        {
            for (int i = 0; i < randomSlopes.Length; i++)
            {
                randomSlopes[i] = random.NextFloat(-1f, 1f);
            }
        }

        public static void GetRandomSlopes(ref Random random, ref FixedList512Bytes<float> randomSlopes)
        {
            for (int i = 0; i < randomSlopes.Length; i++)
            {
                randomSlopes[i] = random.NextFloat(-1f, 1f);
            }
        }
    }
}