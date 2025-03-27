
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Trove
{
    public static unsafe class ByteArrayUtilities
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
        public static bool CanReadValues<T1, T2>(int byteArrayLength, int byteIndex)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            return byteIndex >= 0 && byteArrayLength >= byteIndex + UnsafeUtility.SizeOf<T1>() + UnsafeUtility.SizeOf<T2>();
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
        public static bool IsAddValuesWithinCapacity<T1, T2>(int byteArrayLength, int byteArrayCapacity, out int requiredLength)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            requiredLength = byteArrayLength + UnsafeUtility.SizeOf<T1>() + UnsafeUtility.SizeOf<T2>();
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
        public static void WriteValues<T1, T2>(byte* byteArrayPtr, int byteIndex, T1 value1, T2 value2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            UnsafeUtility.AsRef<T1>(startPtr) = value1;
            byteIndex += UnsafeUtility.SizeOf<T1>();
            startPtr = byteArrayPtr + (long)byteIndex;
            UnsafeUtility.AsRef<T2>(startPtr) = value2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteValue<T>(byte* byteArrayPtr, ref int byteIndex, T value)
            where T : unmanaged
        {
            WriteValue(byteArrayPtr, byteIndex, value);
            byteIndex += UnsafeUtility.SizeOf<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteValues<T1, T2>(byte* byteArrayPtr, ref int byteIndex, T1 value1, T2 value2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            WriteValues(byteArrayPtr, byteIndex, value1, value2);
            byteIndex += UnsafeUtility.SizeOf<T1>() + UnsafeUtility.SizeOf<T2>();
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
            value = *(T*)startPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T ReadValueAsRef<T>(byte* byteArrayPtr, int byteIndex)
            where T : unmanaged
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            return ref *(T*)startPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReadValues<T1, T2>(byte* byteArrayPtr, int byteIndex, out T1 value1, out T2 value2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            value1 = *(T1*)startPtr;
            byteIndex += UnsafeUtility.SizeOf<T1>();
            startPtr = byteArrayPtr + (long)byteIndex;
            value2 = *(T2*)startPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReadValue<T>(byte* byteArrayPtr, ref int byteIndex, out T value)
            where T : unmanaged
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            value = *(T*)startPtr;
            byteIndex += UnsafeUtility.SizeOf<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReadValues<T1, T2>(byte* byteArrayPtr, ref int byteIndex, out T1 value1, out T2 value2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            ReadValues(byteArrayPtr, byteIndex, out value1, out value2);
            byteIndex += UnsafeUtility.SizeOf<T1>() + UnsafeUtility.SizeOf<T2>();
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
        public static void InsertValues<T1, T2>(byte* byteArrayPtr, int byteIndex, int endByteIndex, T1 value1, T2 value2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            byte* startPtr = byteArrayPtr + (long)byteIndex;
            byte* destPtr = startPtr + (long)UnsafeUtility.SizeOf<T1>() + (long)UnsafeUtility.SizeOf<T2>();
            int copySize = endByteIndex - byteIndex;
            UnsafeUtility.MemCpy(destPtr, startPtr, copySize);
            WriteValues(byteArrayPtr, byteIndex, value1, value2);
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
            RemoveValue(byteArrayPtr, byteIndex, byteArrayLength, sizeOfValue, out newByteArrayLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RemoveValues<T1, T2>(byte* byteArrayPtr, int byteIndex, int byteArrayLength, out int newByteArrayLength)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            int sizeOfValue = UnsafeUtility.SizeOf<T1>() + UnsafeUtility.SizeOf<T2>();
            RemoveValue(byteArrayPtr, byteIndex, byteArrayLength, sizeOfValue, out newByteArrayLength);
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

        // TODO: Can't remember what that big comments thing meant?
        // TODO: Make sure I use MemMove instead of MemCpy when ranges could overlap
        ///////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////  ///////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////////////////////////////////////////////////////////////////

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddValue<T>(ref NativeStream.Writer stream, T value)
            where T : unmanaged
        {
            stream.Write(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddValues<T1, T2>(ref NativeStream.Writer stream, T1 value1, T2 value2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            byte* startPtr = stream.Allocate(UnsafeUtility.SizeOf<T1>() + UnsafeUtility.SizeOf<T2>());
            UnsafeUtility.AsRef<T1>(startPtr) = value1;
            startPtr = startPtr + (long)UnsafeUtility.SizeOf<T1>();
            UnsafeUtility.AsRef<T2>(startPtr) = value2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddValue<T>(ref UnsafeStream.Writer stream, T value)
            where T : unmanaged
        {
            stream.Write(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddValues<T1, T2>(ref UnsafeStream.Writer stream, T1 value1, T2 value2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            byte* startPtr = stream.Allocate(UnsafeUtility.SizeOf<T1>() + UnsafeUtility.SizeOf<T2>());
            UnsafeUtility.AsRef<T1>(startPtr) = value1;
            startPtr = startPtr + (long)UnsafeUtility.SizeOf<T1>();
            UnsafeUtility.AsRef<T2>(startPtr) = value2;
        }

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
        public static void AddValues<T1, T2>(ref NativeList<byte> list, T1 value1, T2 value2, float growFactor = 2f)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            int initialLength = list.Length;
            if (!IsAddValuesWithinCapacity<T1, T2>(list.Length, list.Capacity, out int requiredLength))
            {
                list.SetCapacity((int)math.ceil(requiredLength * growFactor));
            }
            list.ResizeUninitialized(requiredLength);
            WriteValues(list.GetUnsafePtr(), initialLength, value1, value2);
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
        public static void AddValues<T1, T2>(ref UnsafeList<byte> list, T1 value1, T2 value2, float growFactor = 2f)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            int initialLength = list.Length;
            if (!IsAddValuesWithinCapacity<T1, T2>(list.Length, list.Capacity, out int requiredLength))
            {
                list.SetCapacity((int)math.ceil(requiredLength * math.min(1f, growFactor)));
            }
            list.Resize(requiredLength);
            WriteValues(list.Ptr, initialLength, value1, value2);
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
        public static void AddValues<T1, T2>(ref DynamicBuffer<byte> buffer, T1 value1, T2 value2, float growFactor = 2f)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            int initialLength = buffer.Length;
            if (!IsAddValuesWithinCapacity<T1, T2>(buffer.Length, buffer.Capacity, out int requiredLength))
            {
                buffer.EnsureCapacity((int)math.ceil(requiredLength * math.min(1f, growFactor)));
            }
            buffer.ResizeUninitialized(requiredLength);
            WriteValues((byte*)buffer.GetUnsafePtr(), initialLength, value1, value2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryReadValue<T>(ref NativeStream.Reader stream, out T value)
            where T : unmanaged
        {
            if (stream.RemainingItemCount > 0)
            {
                value = stream.Read<T>();
                return true;
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryReadValues<T1, T2>(ref NativeStream.Reader stream, out T1 value1, out T2 value2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            if (stream.RemainingItemCount > 0)
            {
                byte* startPtr = stream.ReadUnsafePtr(UnsafeUtility.SizeOf<T1>() + UnsafeUtility.SizeOf<T2>());
                value1 = *(T1*)startPtr;
                startPtr = startPtr + (long)UnsafeUtility.SizeOf<T1>();
                value2 = *(T2*)startPtr;
                return true;
            }
            value1 = default;
            value2 = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryReadValue<T>(ref UnsafeStream.Reader stream, out T value)
            where T : unmanaged
        {
            if (stream.RemainingItemCount > 0)
            {
                value = stream.Read<T>();
                return true;
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryReadValues<T1, T2>(ref UnsafeStream.Reader stream, out T1 value1, out T2 value2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            if (stream.RemainingItemCount > 0)
            {
                byte* startPtr = stream.ReadUnsafePtr(UnsafeUtility.SizeOf<T1>() + UnsafeUtility.SizeOf<T2>());
                value1 = *(T1*)startPtr;
                startPtr = startPtr + (long)UnsafeUtility.SizeOf<T1>();
                value2 = *(T2*)startPtr;
                return true;
            }
            value1 = default;
            value2 = default;
            return false;
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
        public static bool TryReadValues<T1, T2>(ref NativeList<byte> list, ref int byteIndex, out T1 value1, out T2 value2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            if (CanReadValues<T1, T2>(list.Length, byteIndex))
            {
                ReadValues<T1, T2>(list.GetUnsafePtr(), ref byteIndex, out value1, out value2);
                return true;
            }
            value1 = default;
            value2 = default;
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
        public static bool TryReadValues<T1, T2>(ref UnsafeList<byte> list, ref int byteIndex, out T1 value1, out T2 value2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            if (CanReadValues<T1, T2>(list.Length, byteIndex))
            {
                ReadValues<T1, T2>(list.Ptr, ref byteIndex, out value1, out value2);
                return true;
            }
            value1 = default;
            value2 = default;
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
        public static bool TryReadValues<T1, T2>(ref DynamicBuffer<byte> buffer, ref int byteIndex, out T1 value1, out T2 value2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            if (CanReadValues<T1, T2>(buffer.Length, byteIndex))
            {
                ReadValues<T1, T2>((byte*)buffer.GetUnsafePtr(), ref byteIndex, out value1, out value2);
                return true;
            }
            value1 = default;
            value2 = default;
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
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryInsertValues<T1, T2>(ref NativeList<byte> list, int byteIndex, T1 value1, T2 value2, float growFactor = 2f)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            if (byteIndex >= 0 && byteIndex < list.Length)
            {
                int lengthBeforeResize = list.Length;
                if (!IsAddValuesWithinCapacity<T1, T2>(list.Length, list.Capacity, out int requiredLength))
                {
                    list.SetCapacity((int)math.ceil(requiredLength * math.min(1f, growFactor)));
                }
                list.ResizeUninitialized(requiredLength);
                InsertValues<T1, T2>(list.GetUnsafePtr(), byteIndex, lengthBeforeResize, value1, value2);
                return true;
            }
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
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryInsertValues<T1, T2>(ref UnsafeList<byte> list, int byteIndex, T1 value1, T2 value2, float growFactor = 2f)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            if (byteIndex >= 0 && byteIndex < list.Length)
            {
                int lengthBeforeResize = list.Length;
                if (!IsAddValuesWithinCapacity<T1, T2>(list.Length, list.Capacity, out int requiredLength))
                {
                    list.SetCapacity((int)math.ceil(requiredLength * math.min(1f, growFactor)));
                }
                list.Resize(requiredLength);
                InsertValues<T1, T2>(list.Ptr, byteIndex, lengthBeforeResize, value1, value2);
                return true;
            }
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
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryInsertValues<T1, T2>(ref DynamicBuffer<byte> buffer, int byteIndex, T1 value1, T2 value2, float growFactor = 2f)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            if (byteIndex >= 0 && byteIndex < buffer.Length)
            {
                int lengthBeforeResize = buffer.Length;
                if (!IsAddValuesWithinCapacity<T1, T2>(buffer.Length, buffer.Capacity, out int requiredLength))
                {
                    buffer.EnsureCapacity((int)math.ceil(requiredLength * math.min(1f, growFactor)));
                }
                buffer.ResizeUninitialized(requiredLength);
                InsertValues<T1, T2>((byte*)buffer.GetUnsafePtr(), byteIndex, lengthBeforeResize, value1, value2);
                return true;
            }
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
        public static bool TryRemoveValues<T1, T2>(ref NativeList<byte> list, int byteIndex)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            if (CanReadValues<T1, T2>(list.Length, byteIndex))
            {
                RemoveValues<T1, T2>(list.GetUnsafePtr(), byteIndex, list.Length, out int newLength);
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
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRemoveValues<T1, T2>(ref UnsafeList<byte> list, int byteIndex)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            if (CanReadValues<T1, T2>(list.Length, byteIndex))
            {
                RemoveValues<T1, T2>(list.Ptr, byteIndex, list.Length, out int newLength);
                list.Resize(newLength);
                return true;
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
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryRemoveValues<T1, T2>(ref DynamicBuffer<byte> buffer, int byteIndex)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            if (CanReadValues<T1, T2>(buffer.Length, byteIndex))
            {
                RemoveValues<T1, T2>((byte*)buffer.GetUnsafePtr(), byteIndex, buffer.Length, out int newLength);
                buffer.ResizeUninitialized(newLength);
                return true;
            }
            return false;
        }
    }
}