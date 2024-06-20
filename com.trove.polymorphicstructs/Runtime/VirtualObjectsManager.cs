using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using System.Runtime.InteropServices;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;

namespace Trove.PolymorphicStructs
{
    public struct VirtualObjectHandle
    {
        public int MetaDataIndex;

        public bool IsValid()
        {
            return MetaDataIndex != 0;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct VirtualObjectMetaData
    {
        [FieldOffset(0)]
        public int ByteIndex;
        [FieldOffset(4)]
        public int Size;

        public bool IsValid()
        {
            return ByteIndex != 0;
        }
    }

    public unsafe static class VirtualObjectsManager
    {
        public const int ByteIndex_ObjectMetaDatasCapacity = 0;
        public const int ByteIndex_UsedObjectMetaDatasStartIndex = 4;
        public const int ByteIndex_UsedObjectMetaDatasCount = 8;
        public const int ByteIndex_FreeObjectMetaDatasStartIndex = 12;
        public const int ByteIndex_FreeObjectMetaDatasCount = 16;
        public const int ByteIndex_ObjectDataStartIndex = 20;
        public const int ByteIndex_ObjectDataEndIndex = 24;
        public const int ByteIndex_ObjectMetaDatasPoolStart = 28;
        // - ...ObjectMetaDatas pool
        // - ...Used ObjectMetaDatas list
        // - ...Free ObjectMetaDatas list
        // - ...Object datas

        private const int SizeOfInt = 4;
        private const float ObjectsCapacityGrowFactor = 2f;
        private const float TotalCapacityGrowFactor = 2f;

        public static void InitializeVirtualObjects(byte* byteArrayPtr, int objectsCapacity)
        {
            // Static data
            SetObjectMetaDatasCapacity(byteArrayPtr, objectsCapacity);
            SetUsedObjectMetaDatasStartIndex(byteArrayPtr, CalculateUsedObjectMetaDatasStartIndex(byteArrayPtr));
            SetUsedObjectMetaDatasCount(byteArrayPtr, 0);
            SetFreeObjectMetaDatasStartIndex(byteArrayPtr, CalculateFreeObjectMetaDatasStartIndex(byteArrayPtr));
            SetFreeObjectMetaDatasCount(byteArrayPtr, objectsCapacity);
            SetObjectDataStartIndex(byteArrayPtr, CalculateObjectDataStartIndex(byteArrayPtr));
            SetObjectDataEndIndex(byteArrayPtr, GetObjectDataStartIndex(byteArrayPtr));

            // Note: ObjectMetaDatas pool all initialized at 0
            // Note: UsedObjectMetaDatas have zero count, so they don't need initialization

            // Initialize free object handles
            int objectMetaDataByteIndex = ByteIndex_ObjectMetaDatasPoolStart;
            int byteIndexOfFreeObjectMetaDatas = GetFreeObjectMetaDatasStartIndex(byteArrayPtr);
            for (int i = 0; i < objectsCapacity; i++)
            {
                PolymorphicUtilities.WriteValue<int>(byteArrayPtr, ref byteIndexOfFreeObjectMetaDatas, objectMetaDataByteIndex);
                objectMetaDataByteIndex += UnsafeUtility.SizeOf<VirtualObjectMetaData> ();
            }
        }

        public static void InitializeVirtualObjects(ref NativeList<byte> list, int objectsCapacity, int objectDataBytesCapacity)
        {
            int requiredLength = GetInitializedBufferLength(objectsCapacity);
            list.SetCapacity(requiredLength + objectDataBytesCapacity);
            list.ResizeUninitialized(requiredLength);
            InitializeVirtualObjects(list.GetUnsafePtr(), objectsCapacity);
        }

        public static void InitializeVirtualObjects(ref UnsafeList<byte> list, int objectsCapacity, int objectDataBytesCapacity)
        {
            int requiredLength = GetInitializedBufferLength(objectsCapacity);
            list.SetCapacity(requiredLength + objectDataBytesCapacity);
            list.Resize(requiredLength);
            InitializeVirtualObjects(list.Ptr, objectsCapacity);
        }

        public static void InitializeVirtualObjects(ref DynamicBuffer<byte> buffer, int objectsCapacity, int objectDataBytesCapacity)
        {
            int requiredLength = GetInitializedBufferLength(objectsCapacity);
            buffer.EnsureCapacity(requiredLength + objectDataBytesCapacity);
            buffer.ResizeUninitialized(requiredLength);
            InitializeVirtualObjects((byte*)buffer.GetUnsafePtr(), objectsCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static VirtualObjectHandle AddObject<T>(byte* byteArrayPtr, int byteIndex, T value)
            where T : unmanaged
        {
            // TODO: there's something finnicky about the "SetObjectDataEndIndex"
            PolymorphicUtilities.WriteValue(byteArrayPtr, byteIndex, value);
            int valueSize = UnsafeUtility.SizeOf<T>();
            SetObjectDataEndIndex(byteArrayPtr, GetObjectDataEndIndex(byteArrayPtr) + valueSize);
            return CreateNewObject(byteArrayPtr, byteIndex, valueSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static VirtualObjectHandle AddObject<T1, T2>(byte* byteArrayPtr, int byteIndex, T1 value1, T2 value2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            PolymorphicUtilities.WriteValues(byteArrayPtr, byteIndex, value1, value2);
            int valueSize = UnsafeUtility.SizeOf<T1>() + UnsafeUtility.SizeOf<T2>();
            SetObjectDataEndIndex(byteArrayPtr, GetObjectDataEndIndex(byteArrayPtr) + valueSize);
            return CreateNewObject(byteArrayPtr, byteIndex, valueSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddObject<T>(ref NativeList<byte> list, T value)
            where T : unmanaged
        {
            int objectSize = UnsafeUtility.SizeOf<T>();
            int lengthBeforeResize = list.Length;
            int requiredLength = lengthBeforeResize + objectSize;
            Resize(ref list, requiredLength);
            AddObject(list.GetUnsafePtr(), lengthBeforeResize, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddObject<T1, T2>(ref NativeList<byte> list, T1 value1, T2 value2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            int objectSize = UnsafeUtility.SizeOf<T1>() + UnsafeUtility.SizeOf<T2>();
            int lengthBeforeResize = list.Length;
            int requiredLength = lengthBeforeResize + objectSize;
            Resize(ref list, requiredLength);
            AddObject(list.GetUnsafePtr(), lengthBeforeResize, value1, value2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddObject<T>(ref UnsafeList<byte> list, T value)
            where T : unmanaged
        {
            int objectSize = UnsafeUtility.SizeOf<T>();
            int lengthBeforeResize = list.Length;
            int requiredLength = lengthBeforeResize + objectSize;
            Resize(ref list, requiredLength);
            AddObject(list.Ptr, lengthBeforeResize, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddObject<T1, T2>(ref UnsafeList<byte> list, T1 value1, T2 value2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            int objectSize = UnsafeUtility.SizeOf<T1>() + UnsafeUtility.SizeOf<T2>();
            int lengthBeforeResize = list.Length;
            int requiredLength = lengthBeforeResize + objectSize;
            Resize(ref list, requiredLength);
            AddObject(list.Ptr, lengthBeforeResize, value1, value2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddObject<T>(ref DynamicBuffer<byte> buffer, T value)
            where T : unmanaged
        {
            int objectSize = UnsafeUtility.SizeOf<T>();
            int lengthBeforeResize = buffer.Length;
            int requiredLength = lengthBeforeResize + objectSize;
            Resize(ref buffer, requiredLength);
            AddObject((byte*)buffer.GetUnsafePtr(), lengthBeforeResize, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddObject<T1, T2>(ref DynamicBuffer<byte> buffer, T1 value1, T2 value2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            int objectSize = UnsafeUtility.SizeOf<T1>() + UnsafeUtility.SizeOf<T2>();
            int lengthBeforeResize = buffer.Length;
            int requiredLength = lengthBeforeResize + objectSize;
            Resize(ref buffer, requiredLength);
            AddObject((byte*)buffer.GetUnsafePtr(), lengthBeforeResize, value1, value2);
        }

        // TODO: Insert object

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RemoveObject(byte* byteArrayPtr, VirtualObjectMetaData objectMetaData)
        {
            PolymorphicUtilities.WriteValue(byteArrayPtr, byteIndex, value);
            return new VirtualObjectMetaData
            {
                ByteIndex = byteIndex,
                Size = UnsafeUtility.SizeOf<T>(),
            };
        }

        #region Getters
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetObjectMetaDatasCapacity(byte* byteArrayPtr)
        {
            PolymorphicUtilities.ReadValue(byteArrayPtr, ByteIndex_ObjectMetaDatasCapacity, out int val);
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetUsedObjectMetaDatasStartIndex(byte* byteArrayPtr)
        {
            PolymorphicUtilities.ReadValue(byteArrayPtr, ByteIndex_UsedObjectMetaDatasStartIndex, out int val);
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetUsedObjectMetaDatasCount(byte* byteArrayPtr)
        {
            PolymorphicUtilities.ReadValue(byteArrayPtr, ByteIndex_UsedObjectMetaDatasCount, out int val);
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetFreeObjectMetaDatasStartIndex(byte* byteArrayPtr)
        {
            PolymorphicUtilities.ReadValue(byteArrayPtr, ByteIndex_FreeObjectMetaDatasStartIndex, out int val);
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetFreeObjectMetaDatasCount(byte* byteArrayPtr)
        {
            PolymorphicUtilities.ReadValue(byteArrayPtr, ByteIndex_FreeObjectMetaDatasCount, out int val);
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetObjectDataStartIndex(byte* byteArrayPtr)
        {
            PolymorphicUtilities.ReadValue(byteArrayPtr, ByteIndex_ObjectDataStartIndex, out int val);
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetObjectDataEndIndex(byte* byteArrayPtr)
        {
            PolymorphicUtilities.ReadValue(byteArrayPtr, ByteIndex_ObjectDataEndIndex, out int val);
            return val;
        }
        #endregion

        #region Setters
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SetObjectMetaDatasCapacity(byte* byteArrayPtr, int val)
        {
            PolymorphicUtilities.WriteValue<int>(byteArrayPtr, ByteIndex_ObjectMetaDatasCapacity, val);
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SetUsedObjectMetaDatasStartIndex(byte* byteArrayPtr, int val)
        {
            PolymorphicUtilities.WriteValue<int>(byteArrayPtr, ByteIndex_UsedObjectMetaDatasStartIndex, val);
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SetUsedObjectMetaDatasCount(byte* byteArrayPtr, int val)
        {
            PolymorphicUtilities.WriteValue<int>(byteArrayPtr, ByteIndex_UsedObjectMetaDatasCount, val);
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SetFreeObjectMetaDatasStartIndex(byte* byteArrayPtr, int val)
        {
            PolymorphicUtilities.WriteValue<int>(byteArrayPtr, ByteIndex_FreeObjectMetaDatasStartIndex, val);
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SetFreeObjectMetaDatasCount(byte* byteArrayPtr, int val)
        {
            PolymorphicUtilities.WriteValue<int>(byteArrayPtr, ByteIndex_FreeObjectMetaDatasCount, val);
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SetObjectDataStartIndex(byte* byteArrayPtr, int val)
        {
            PolymorphicUtilities.WriteValue<int>(byteArrayPtr, ByteIndex_ObjectDataStartIndex, val);
            return val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int SetObjectDataEndIndex(byte* byteArrayPtr, int val)
        {
            PolymorphicUtilities.WriteValue<int>(byteArrayPtr, ByteIndex_ObjectDataEndIndex, val);
            return val;
        }
        #endregion

        #region Misc
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetInitializedBufferLength(int objectMetaDatasCapacity)
        {
            return
                ByteIndex_ObjectMetaDatasPoolStart + // Where the flexible data starts
                (objectMetaDatasCapacity * UnsafeUtility.SizeOf<VirtualObjectMetaData>()) + // ObjectMetaDatas pool
                (objectMetaDatasCapacity * SizeOfInt); // Used/Free object handles
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int CalculateUsedObjectMetaDatasStartIndex(byte* byteArrayPtr)
        {
            return ByteIndex_ObjectMetaDatasPoolStart + (GetObjectMetaDatasCapacity(byteArrayPtr) * UnsafeUtility.SizeOf<VirtualObjectMetaData>());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int CalculateFreeObjectMetaDatasStartIndex(byte* byteArrayPtr)
        {
            return GetUsedObjectMetaDatasStartIndex(byteArrayPtr) + (GetUsedObjectMetaDatasCount(byteArrayPtr) * SizeOfInt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int CalculateObjectDataStartIndex(byte* byteArrayPtr)
        {
            return GetFreeObjectMetaDatasStartIndex(byteArrayPtr) + (GetFreeObjectMetaDatasCount(byteArrayPtr) * SizeOfInt);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Resize(ref NativeList<byte> list, int requiredLength)
        {
            if (list.Capacity <= requiredLength)
            {
                list.SetCapacity((int)math.ceil(requiredLength * TotalCapacityGrowFactor));
            }
            list.ResizeUninitialized(requiredLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Resize(ref UnsafeList<byte> list, int requiredLength)
        {
            if (list.Capacity <= requiredLength)
            {
                list.SetCapacity((int)math.ceil(requiredLength * TotalCapacityGrowFactor));
            }
            list.Resize(requiredLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Resize(ref DynamicBuffer<byte> buffer, int requiredLength)
        {
            if (buffer.Capacity <= requiredLength)
            {
                buffer.EnsureCapacity((int)math.ceil(requiredLength * TotalCapacityGrowFactor));
            }
            buffer.ResizeUninitialized(requiredLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static VirtualObjectHandle CreateNewObject(byte* byteArrayPtr, int byteIndexOfObject, int sizeOfObject)
        {
            // Find a free object metaData slot
            int usedObjectsCount = GetUsedObjectMetaDatasCount(byteArrayPtr);
            int freeObjectsCount = GetFreeObjectMetaDatasCount(byteArrayPtr);
            int newObjectMetaDataByteIndex;
            if (freeObjectsCount > 0)
            {
                MoveFreeObjectToUsedObject(byteArrayPtr, ref usedObjectsCount, ref freeObjectsCount, out newObjectMetaDataByteIndex);
            }
            else
            {
                // TODO: Grow objects capacity
                {
                    // Set static data
                    int oldObjectsCapacity = GetObjectMetaDatasCapacity(byteArrayPtr);
                    int newObjectsCapacity = (int)math.ceil(oldObjectsCapacity * 2f);
                    int capacityDiff = (newObjectsCapacity - oldObjectsCapacity);
                    SetObjectMetaDatasCapacity(byteArrayPtr, newObjectsCapacity);
                    SetUsedObjectMetaDatasStartIndex(byteArrayPtr, CalculateUsedObjectMetaDatasStartIndex(byteArrayPtr));
                    SetFreeObjectMetaDatasStartIndex(byteArrayPtr, CalculateFreeObjectMetaDatasStartIndex(byteArrayPtr));
                    int freeObjectMetaDatasCount = GetFreeObjectMetaDatasCount(byteArrayPtr) + capacityDiff;
                    SetFreeObjectMetaDatasCount(byteArrayPtr, freeObjectMetaDatasCount);
                    SetObjectDataStartIndex(byteArrayPtr, CalculateObjectDataStartIndex(byteArrayPtr));
                    SetObjectDataEndIndex(byteArrayPtr, GetObjectDataEndIndex(byteArrayPtr) + capacityDiff);

                    // Resize the list/buffer

                    // Move all data starting from used objects

                    // Update existing metaDatas after the data shift

                    // Clear new object metaDatas
                    UnsafeUtility.MemClear();
                }

                MoveFreeObjectToUsedObject(byteArrayPtr, ref usedObjectsCount, ref freeObjectsCount, out newObjectMetaDataByteIndex);
            }

            // Write object meta data
            VirtualObjectMetaData objectMetaData = new VirtualObjectMetaData
            {
                ByteIndex = byteIndexOfObject,
                Size = sizeOfObject,
            };
            PolymorphicUtilities.WriteValue(byteArrayPtr, newObjectMetaDataByteIndex, objectMetaData);

            return new VirtualObjectHandle
            {
                MetaDataIndex = newObjectMetaDataByteIndex,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void MoveFreeObjectToUsedObject(byte* byteArrayPtr, ref int usedObjectsCount, ref int freeObjectsCount, out int byteIndexOfFreeObjectMetaData)
        {
            int usedObjectsStartIndex = GetUsedObjectMetaDatasStartIndex(byteArrayPtr);
            int freeObjectsStartIndex = GetFreeObjectMetaDatasStartIndex(byteArrayPtr);

            // Get the first free object in the list, guaranteed to be the lowest one due to sorting
            PolymorphicUtilities.ReadValue<int>(byteArrayPtr, freeObjectsStartIndex, out byteIndexOfFreeObjectMetaData);

            // Add the free object index to used objects, sorted in ascending order
            int newUsedObjectInsertIndex = -1;
            // Note: If there were no used objects, the first free object simply becomes the first used object by adjusting counts.
            // So there is nothing to do
            if (usedObjectsCount > 0)
            {
                PolymorphicUtilities.ReadValue<int>(byteArrayPtr, usedObjectsStartIndex, out int byteIndexOfFirstUsedObjectMetaData);
                PolymorphicUtilities.ReadValue<int>(byteArrayPtr, freeObjectsStartIndex - SizeOfInt, out int byteIndexOfLastUsedObjectMetaData);

                // We only need to insert the new used object if it wouldn't be at the end of the list (greater than the current last used object)
                if (byteIndexOfFreeObjectMetaData < byteIndexOfLastUsedObjectMetaData)
                {
                    // Iterate used objects until we find one that's greater than our new one. That's our insert index
                    for (int i = 0; i < usedObjectsCount; i++)
                    {
                        int tmpUsedObjectByteIndex = usedObjectsStartIndex + (i * SizeOfInt);
                        PolymorphicUtilities.ReadValue<int>(byteArrayPtr, tmpUsedObjectByteIndex, out int tmpByteIndexOfUsedObjectMetaData);
                        if (byteIndexOfFreeObjectMetaData < tmpByteIndexOfUsedObjectMetaData)
                        {
                            newUsedObjectInsertIndex = tmpUsedObjectByteIndex;
                            break;
                        }
                    }
                }
            }

            // Handle inserting new used object, and displace the old used objects over the first free object to overwrite it
            if (newUsedObjectInsertIndex >= 0)
            {
                byte* startPtr = byteArrayPtr + (long)(newUsedObjectInsertIndex);
                byte* destPtr = byteArrayPtr + (long)(newUsedObjectInsertIndex + SizeOfInt);
                int copySize = usedObjectsCount * SizeOfInt;
                UnsafeUtility.MemCpy(destPtr, startPtr, copySize);
                PolymorphicUtilities.WriteValue(byteArrayPtr, newUsedObjectInsertIndex, byteIndexOfFreeObjectMetaData);
            }

            // Update counts
            usedObjectsCount++;
            freeObjectsCount--;
            SetUsedObjectMetaDatasCount(byteArrayPtr, usedObjectsCount);
            SetFreeObjectMetaDatasCount(byteArrayPtr, freeObjectsCount);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void FreeObject(byte* byteArrayPtr, VirtualObjectHandle objectHandle)
        {
            // TODO
        }
        #endregion
    }
}