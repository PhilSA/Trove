using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Trove.ObjectHandles
{
    public unsafe static partial class VirtualObjectManager
    {
        public struct MemoryInfo
        {
            public int MetadatasStartIndex;
            public int MetadatasCapacity;
            public int DatasStartIndex;
            public int MetadataFreeRangesStartIndex;
            public int DataFreeRangesStartIndex;

            public VirtualListHandle<IndexRangeElement> MetadataFreeRangesHandle;
            public VirtualListHandle<IndexRangeElement> DataFreeRangesHandle;
        }

        private const int FreeRangesInitialCapacity = 64;
        private const float ObjectsCapacityGrowFactor = 2f;

        private static int ByteIndex_ObjectMetadataCapacity = 0;
        private static int ByteIndex_ObjectMetadataCount = ByteIndex_ObjectMetadataCapacity + UnsafeUtility.SizeOf<int>();
        private static int ByteIndex_ObjectDataStartIndex = ByteIndex_ObjectMetadataCount + UnsafeUtility.SizeOf<int>();
        private static int ByteIndex_MetadataFreeRangesHandle = ByteIndex_ObjectDataStartIndex + UnsafeUtility.SizeOf<int>();
        private static int ByteIndex_ObjectDataFreeRangesHandle = ByteIndex_MetadataFreeRangesHandle + UnsafeUtility.SizeOf<VirtualListHandle<IndexRangeElement>>();
        private static int ByteIndex_MetadatasStartIndex = ByteIndex_ObjectDataFreeRangesHandle + UnsafeUtility.SizeOf<VirtualListHandle<IndexRangeElement>>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetObjectMetadatasCapacity(byte* byteArrayPtr, out int value)
        {
            ByteArrayUtilities.ReadValue<int>(byteArrayPtr, ByteIndex_ObjectMetadataCapacity, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetObjectMetadatasCount(byte* byteArrayPtr, out int value)
        {
            ByteArrayUtilities.ReadValue<int>(byteArrayPtr, ByteIndex_ObjectMetadataCount, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetObjectDatasStartIndex(byte* byteArrayPtr, out int value)
        {
            ByteArrayUtilities.ReadValue<int>(byteArrayPtr, ByteIndex_ObjectDataStartIndex, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CalculateAndSetObjectDatasStartIndex(byte* byteArrayPtr)
        {
            GetObjectMetadatasCapacity(byteArrayPtr, out int objectMetadatasCapacity);
            int startIndex = ByteIndex_MetadatasStartIndex + (UnsafeUtility.SizeOf<VirtualObjectMetadata>() * objectMetadatasCapacity);
            ByteArrayUtilities.WriteValue<int>(byteArrayPtr, ByteIndex_ObjectDataStartIndex, startIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetObjectMetadatasCapacity(byte* byteArrayPtr, int value)
        {
            ByteArrayUtilities.WriteValue<int>(byteArrayPtr, ByteIndex_ObjectMetadataCapacity, value);
            CalculateAndSetObjectDatasStartIndex(byteArrayPtr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetObjectMetadatasCount(byte* byteArrayPtr, int value)
        {
            ByteArrayUtilities.WriteValue<int>(byteArrayPtr, ByteIndex_ObjectMetadataCount, value);
        }

        private static void GetDataFreeRangesListHandle(byte* byteArrayPtr, out VirtualListHandle<IndexRangeElement> value)
        {
            ByteArrayUtilities.ReadValue(byteArrayPtr, ByteIndex_ObjectDataFreeRangesHandle, out value);
        }

        private static void GetMetadataFreeRangesListHandle(byte* byteArrayPtr, out VirtualListHandle<IndexRangeElement> value)
        {
            ByteArrayUtilities.ReadValue(byteArrayPtr, ByteIndex_MetadataFreeRangesHandle, out value);
        }

        private static void SetDataFreeRangesListHandle(byte* byteArrayPtr, VirtualListHandle<IndexRangeElement> value)
        {
            ByteArrayUtilities.WriteValue(byteArrayPtr, ByteIndex_ObjectDataFreeRangesHandle, value);
        }

        private static void SetMetadataFreeRangesListHandle(byte* byteArrayPtr, VirtualListHandle<IndexRangeElement> value)
        {
            ByteArrayUtilities.WriteValue(byteArrayPtr, ByteIndex_MetadataFreeRangesHandle, value);
        }

        private static void ShiftMetadataByteIndexes(
            byte* elementDataBufferPtr,
            int indexShift,
            int metadatasEndIndexExclusive)
        {
            for (int i = ByteIndex_MetadatasStartIndex; i < metadatasEndIndexExclusive; i += UnsafeUtility.SizeOf<VirtualObjectMetadata>())
            {
                ByteArrayUtilities.ReadValue(elementDataBufferPtr, i, out VirtualObjectMetadata metadata);
                metadata.ByteIndex += indexShift;
                ByteArrayUtilities.WriteValue(elementDataBufferPtr, i, metadata);
            }
        }
    }
}
