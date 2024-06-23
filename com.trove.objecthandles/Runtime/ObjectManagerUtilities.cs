using System.Runtime.CompilerServices;
using Unity.Assertions;
using Unity.Collections;
using Unity.Entities;
using static Trove.ObjectHandles.VirtualObjectManager;

namespace Trove.ObjectHandles
{
    public struct IndexRangeElement
    {
        public int StartInclusive;
        public int EndExclusive;
    }

    public static class ObjectManagerUtilities
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RangesOverlap(int aStartInclusive, int aEndExclusive, int bStartInclusive, int bEndExclusive)
        {
            return aStartInclusive < bEndExclusive && bStartInclusive < aEndExclusive;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ConsumeFreeRange(IndexRangeElement freeIndexRange, int objectIndexesSize,
            out bool isFullyConsumed, out int consumedStartIndex)
        {
            // Consume memory out of the found range
            consumedStartIndex = freeIndexRange.StartInclusive;
            freeIndexRange.StartInclusive += objectIndexesSize;

            Assert.IsTrue(freeIndexRange.StartInclusive <= freeIndexRange.EndExclusive);

            if (freeIndexRange.StartInclusive == freeIndexRange.EndExclusive)
            {
                isFullyConsumed = true;
            }
            isFullyConsumed = false;
        }
    }
}