

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace Trove.EventSystems
{
    public static unsafe class PolymorphicUtilities
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte* GetByteElementPtr(byte* byteArrayPtr, int byteIndex)
        {
            return byteArrayPtr + (long)byteIndex;
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
        public static bool CanRead<T>(int byteArrayLength, int byteIndex)
            where T : unmanaged
        {
            return byteArrayLength >= byteIndex + UnsafeUtility.SizeOf<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CanRead(int byteArrayLength, int byteIndex, int dataSize)
        {
            return byteArrayLength >= byteIndex + dataSize;
        }
    }
}