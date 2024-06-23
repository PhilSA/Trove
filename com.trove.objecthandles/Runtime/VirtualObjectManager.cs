using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Logging;
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
            public int MetadataFreeRangesSize;
            public int DataFreeRangesSize;

            public VirtualListHandle<IndexRangeElement> MetadataFreeRangesHandle;
            public VirtualListHandle<IndexRangeElement> DataFreeRangesHandle;
        }

        private const int FreeRangesInitialCapacity = 16; 
        private const float ObjectsCapacityGrowFactor = 2f;

        private static readonly int ByteIndex_ObjectMetadataCapacity = 0; // int 
        private static readonly int ByteIndex_ObjectDataStartIndex = ByteIndex_ObjectMetadataCapacity + UnsafeUtility.SizeOf<int>(); // int
        private static readonly int ByteIndex_MetadataFreeRangesHandle = ByteIndex_ObjectDataStartIndex + UnsafeUtility.SizeOf<int>(); // VirtualListHandle
        private static readonly int ByteIndex_ObjectDataFreeRangesHandle = ByteIndex_MetadataFreeRangesHandle + UnsafeUtility.SizeOf<VirtualListHandle<IndexRangeElement>>(); // VirtualListHandle
        private static readonly int ByteIndex_MetadatasStart = ByteIndex_ObjectDataFreeRangesHandle + UnsafeUtility.SizeOf<VirtualListHandle<IndexRangeElement>>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetObjectMetadatasCapacityValue(byte* byteArrayPtr, out int value)
        {
            ByteArrayUtilities.ReadValue<int>(byteArrayPtr, ByteIndex_ObjectMetadataCapacity, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void GetObjectDatasStartIndexValue(byte* byteArrayPtr, out int value)
        {
            ByteArrayUtilities.ReadValue<int>(byteArrayPtr, ByteIndex_ObjectDataStartIndex, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CalculateObjectDatasStartIndex(int metadatasCapacity, out int objectDatasStartIndex)
        {
            objectDatasStartIndex = ByteIndex_MetadatasStart + (UnsafeUtility.SizeOf<VirtualObjectMetadata>() * metadatasCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetObjectDatasStartIndexValue(byte* byteArrayPtr, int value)
        {
            ByteArrayUtilities.WriteValue<int>(byteArrayPtr, ByteIndex_ObjectDataStartIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetObjectMetadatasCapacityValue(byte* byteArrayPtr, int value)
        {
            ByteArrayUtilities.WriteValue<int>(byteArrayPtr, ByteIndex_ObjectMetadataCapacity, value);
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
            byte* bytesBufferPtr,
            int indexShift,
            int metadatasCount)
        {
            byte* metadatasBytePtr = (byte*)bytesBufferPtr + (long)ByteIndex_MetadatasStart;
            VirtualObjectMetadata* metadatasPtr = (VirtualObjectMetadata*)metadatasBytePtr;

            for (int i = 0; i < metadatasCount; i++)
            {
                VirtualObjectMetadata metadata = metadatasPtr[i];
                if (metadata.ByteIndex > 0)
                {
                    int old = metadata.ByteIndex;
                    metadata.ByteIndex += indexShift;
                    metadatasPtr[i] = metadata;

                    Log.Debug($"Shaft metadata byteindex from {old} to {metadata.ByteIndex}");
                }
            }
        }
    }
}
