
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Trove.PolymorphicStructs
{
    public static unsafe class PolymorphicUtilities
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte* GetByteElementPtr(byte* byteArrayPtr, int byteIndex)
        {
            return byteArrayPtr + (long)byteIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanReadValue<T>(int byteArrayLength, int byteIndex)
            where T : unmanaged
        {
            return byteIndex >= 0 && byteArrayLength >= byteIndex + UnsafeUtility.SizeOf<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanReadValue(int byteArrayLength, int byteIndex, int valueSize)
        {
            return byteIndex >= 0 && byteArrayLength >= byteIndex + valueSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAddValueWithinCapacity<T>(int byteArrayLength, int byteArrayCapacity, out int requiredLength)
            where T : unmanaged
        {
            requiredLength = byteArrayLength + UnsafeUtility.SizeOf<T>();
            return byteArrayCapacity >= requiredLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAddValueWithinCapacity(int byteArrayLength, int byteArrayCapacity, int valueSize, out int requiredLength)
        {
            requiredLength = byteArrayLength + valueSize;
            return byteArrayCapacity >= requiredLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteValue<T>(byte* byteArrayPtr, int byteIndex, T value)
            where T : unmanaged
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            UnsafeUtility.AsRef<T>(startPtr) = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteValue<T>(byte* byteArrayPtr, ref int byteIndex, T value)
            where T : unmanaged
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            UnsafeUtility.AsRef<T>(startPtr) = value;
            byteIndex += UnsafeUtility.SizeOf<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteValue(byte* byteArrayPtr, int byteIndex, byte* value, int valueSize)
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            UnsafeUtility.MemCpy(startPtr, value, valueSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteValue(byte* byteArrayPtr, ref int byteIndex, byte* value, int valueSize)
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            UnsafeUtility.MemCpy(startPtr, value, valueSize);
            byteIndex += valueSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReadValue<T>(byte* byteArrayPtr, int byteIndex, out T value)
            where T : unmanaged
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            UnsafeUtility.CopyPtrToStructure(startPtr, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReadValue<T>(byte* byteArrayPtr, ref int byteIndex, out T value)
            where T : unmanaged
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            UnsafeUtility.CopyPtrToStructure(startPtr, out value);
            byteIndex += UnsafeUtility.SizeOf<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReadValue(byte* byteArrayPtr, int byteIndex, out byte* value)
        {
            value = byteArrayPtr + (long)byteIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReadValue(byte* byteArrayPtr, ref int byteIndex, out byte* value, int valueSize)
        {
            value = byteArrayPtr + (long)byteIndex;
            byteIndex += valueSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InsertValue<T>(byte* byteArrayPtr, int byteIndex, int endByteIndex, T value)
            where T : unmanaged
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            byte* destPtr = startPtr + (long)UnsafeUtility.SizeOf<T>();
            int copySize = endByteIndex - byteIndex;
            UnsafeUtility.MemCpy(destPtr, startPtr, copySize);
            WriteValue(byteArrayPtr, byteIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InsertValue(byte* byteArrayPtr, int byteIndex, int endByteIndex, byte* value, int valueSize)
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            byte* destPtr = startPtr + (long)valueSize;
            int copySize = endByteIndex - byteIndex;
            UnsafeUtility.MemCpy(destPtr, startPtr, copySize);
            WriteValue(byteArrayPtr, byteIndex, value, valueSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveValue<T>(byte* byteArrayPtr, int byteIndex, int byteArrayLength, out int newByteArrayLength)
            where T : unmanaged
        {
            int sizeOfValue = UnsafeUtility.SizeOf<T>();
            byte* destPtr = byteArrayPtr + (long)byteIndex;
            byte* startPtr = destPtr + (long)sizeOfValue;
            int copySize = byteArrayLength - (byteIndex + sizeOfValue);
            UnsafeUtility.MemCpy(destPtr, startPtr, copySize);
            newByteArrayLength = byteArrayLength - sizeOfValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveValue(byte* byteArrayPtr, int byteIndex, int byteArrayLength, int valueSize, out int newByteArrayLength)
        {
            byte* destPtr = byteArrayPtr + (long)byteIndex;
            byte* startPtr = destPtr + (long)valueSize;
            int copySize = byteArrayLength - (byteIndex + valueSize);
            UnsafeUtility.MemCpy(destPtr, startPtr, copySize);
            newByteArrayLength = byteArrayLength - valueSize;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddValue<T>(ref NativeList<byte> list, T value, float growFactor = 2f)
            where T : unmanaged
        {
            int initialLength = list.Length;
            if (!IsAddValueWithinCapacity<T>(list.Length, list.Capacity, out int requiredLength))
            {
                list.SetCapacity((int)math.ceil(requiredLength * growFactor));
            }
            list.ResizeUninitialized(requiredLength);
            WriteValue(list.GetUnsafePtr(), initialLength, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddValue<T>(ref UnsafeList<byte> list, T value, float growFactor = 2f)
            where T : unmanaged
        {
            int initialLength = list.Length;
            if (!IsAddValueWithinCapacity<T>(list.Length, list.Capacity, out int requiredLength))
            {
                list.SetCapacity((int)math.ceil(requiredLength * math.min(1f, growFactor)));
            }
            list.Resize(requiredLength);
            WriteValue(list.Ptr, initialLength, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddValue<T>(ref DynamicBuffer<byte> buffer, T value, float growFactor = 2f)
            where T : unmanaged
        {
            int initialLength = buffer.Length;
            if (!IsAddValueWithinCapacity<T>(buffer.Length, buffer.Capacity, out int requiredLength))
            {
                buffer.EnsureCapacity((int)math.ceil(requiredLength * math.min(1f, growFactor)));
            }
            buffer.ResizeUninitialized(requiredLength);
            WriteValue((byte*)buffer.GetUnsafePtr(), initialLength, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryReadValue<T>(ref NativeList<byte> list, ref int byteIndex, out T value)
            where T : unmanaged
        {
            if (CanReadValue<T>(list.Length, byteIndex))
            {
                ReadValue<T>(list.GetUnsafePtr(), ref byteIndex, out value);
                return true;
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryReadValue<T>(ref UnsafeList<byte> list, ref int byteIndex, out T value)
            where T : unmanaged
        {
            if (CanReadValue<T>(list.Length, byteIndex))
            {
                ReadValue<T>(list.Ptr, ref byteIndex, out value);
                return true;
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryReadValue<T>(ref DynamicBuffer<byte> buffer, ref int byteIndex, out T value)
            where T : unmanaged
        {
            if (CanReadValue<T>(buffer.Length, byteIndex))
            {
                ReadValue<T>((byte*)buffer.GetUnsafePtr(), ref byteIndex, out value);
                return true;
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryInsertValue<T>(ref NativeList<byte> list, int byteIndex, T value, float growFactor = 2f)
            where T : unmanaged
        {
            if (byteIndex >= 0 && byteIndex < list.Length)
            {
                int lengthBeforeResize = list.Length;
                if (!IsAddValueWithinCapacity<T>(list.Length, list.Capacity, out int requiredLength))
                {
                    list.SetCapacity((int)math.ceil(requiredLength * math.min(1f, growFactor)));
                }
                list.ResizeUninitialized(requiredLength);
                InsertValue<T>(list.GetUnsafePtr(), byteIndex, lengthBeforeResize, value);
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryInsertValue<T>(ref UnsafeList<byte> list, int byteIndex, T value, float growFactor = 2f)
            where T : unmanaged
        {
            if (byteIndex >= 0 && byteIndex < list.Length)
            {
                int lengthBeforeResize = list.Length;
                if (!IsAddValueWithinCapacity<T>(list.Length, list.Capacity, out int requiredLength))
                {
                    list.SetCapacity((int)math.ceil(requiredLength * math.min(1f, growFactor)));
                }
                list.Resize(requiredLength);
                InsertValue<T>(list.Ptr, byteIndex, lengthBeforeResize, value);
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryInsertValue<T>(ref DynamicBuffer<byte> buffer, int byteIndex, T value, float growFactor = 2f)
            where T : unmanaged
        {
            if (byteIndex >= 0 && byteIndex < buffer.Length)
            {
                int lengthBeforeResize = buffer.Length;
                if (!IsAddValueWithinCapacity<T>(buffer.Length, buffer.Capacity, out int requiredLength))
                {
                    buffer.EnsureCapacity((int)math.ceil(requiredLength * math.min(1f, growFactor)));
                }
                buffer.ResizeUninitialized(requiredLength);
                InsertValue<T>((byte*)buffer.GetUnsafePtr(), byteIndex, lengthBeforeResize, value);
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRemoveValue<T>(ref NativeList<byte> list, int byteIndex)
            where T : unmanaged
        {
            if (CanReadValue<T>(list.Length, byteIndex))
            {
                RemoveValue<T>(list.GetUnsafePtr(), byteIndex, list.Length, out int newLength);
                list.ResizeUninitialized(newLength);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRemoveValue<T>(ref UnsafeList<byte> list, int byteIndex)
            where T : unmanaged
        {
            if (CanReadValue<T>(list.Length, byteIndex))
            {
                RemoveValue<T>(list.Ptr, byteIndex, list.Length, out int newLength);
                list.Resize(newLength);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRemoveValue<T>(ref DynamicBuffer<byte> buffer, int byteIndex)
            where T : unmanaged
        {
            if (CanReadValue<T>(buffer.Length, byteIndex))
            {
                RemoveValue<T>((byte*)buffer.GetUnsafePtr(), byteIndex, buffer.Length, out int newLength);
                buffer.ResizeUninitialized(newLength);
            }
            return false;
        }
    }
}