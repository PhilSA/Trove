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
        public static void EvaluateRangeFreeing<T>(ref T freeIndexRangesBuffer, int objectStartIndex, int objectIndexesSize,
            out RangeFreeingType rangeFreeingType, out int indexMatch)
            where T : unmanaged, INativeList<IndexRangeElement>, IIndexable<IndexRangeElement>
        {
            rangeFreeingType = RangeFreeingType.Add;
            indexMatch = -1;

            for (int i = 0; i < freeIndexRangesBuffer.Length; i++)
            {
                IndexRangeElement tmpRange = freeIndexRangesBuffer[i];

                // Assert no ranges overlap
                Assert.IsFalse(RangesOverlap(objectStartIndex, (objectStartIndex + objectIndexesSize), tmpRange.StartInclusive, tmpRange.EndExclusive));

                if (tmpRange.StartInclusive == objectStartIndex + objectIndexesSize)
                {
                    rangeFreeingType = RangeFreeingType.MergeFirst;
                    indexMatch = i;
                    break;
                }
                else if (tmpRange.EndExclusive == objectStartIndex)
                {
                    rangeFreeingType = RangeFreeingType.MergeLast;
                    indexMatch = i;
                    break;
                }
                else if (tmpRange.StartInclusive > objectStartIndex)
                {
                    rangeFreeingType = RangeFreeingType.Insert;
                    indexMatch = i;
                    break;
                }
            }
        }

        public static bool FindLastUsedIndex<T>(ref T freeIndexRangesBuffer, int dataStartIndexInclusive, int dataEndIndexExclusive, out int lastUsedIndex)
            where T : unmanaged, INativeList<IndexRangeElement>, IIndexable<IndexRangeElement>
        {
            int evaluatedIndex = dataEndIndexExclusive - 1;
            for (int i = freeIndexRangesBuffer.Length - 1; i >= 0; i--)
            {
                IndexRangeElement tmpRange = freeIndexRangesBuffer[i];

                if (evaluatedIndex < dataStartIndexInclusive)
                {
                    // If we're past the start index, we haven't found any used index
                    lastUsedIndex = -1;
                    return false;
                }
                else if (RangesOverlap(evaluatedIndex, evaluatedIndex + 1, tmpRange.StartInclusive, tmpRange.EndExclusive))
                {
                    // If the ranges overlap, that means this evaluated index is free.
                    // Continue checking from the start of that free range.
                    evaluatedIndex = tmpRange.StartInclusive - 1;
                }
                else
                {
                    // If the ranges don't overlap, that means the last used index is the iterated one
                    lastUsedIndex = evaluatedIndex;
                    return true;
                }
            }

            lastUsedIndex = -1;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RangesOverlap(int aStartInclusive, int aEndExclusive, int bStartInclusive, int bEndExclusive)
        {
            return aStartInclusive < bEndExclusive && bStartInclusive < aEndExclusive;
        }
    }
}