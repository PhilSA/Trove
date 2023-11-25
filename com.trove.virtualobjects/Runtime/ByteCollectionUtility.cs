

using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Logging;

namespace Trove.PolymorphicElements
{
    public unsafe interface IStreamWriter
    {
        public void Write<T>(T t) where T : unmanaged;
        public byte* Allocate(int size);
        public void BeginForEachIndex(int index);
        public void EndForEachIndex();
    }

    public unsafe interface IStreamReader
    {
        public int RemainingItemCount { get; }
        public T Read<T>() where T : unmanaged;
        public byte* ReadUnsafePtr(int size);
        public void BeginForEachIndex(int index);
        public void EndForEachIndex();
    }

    public unsafe interface IByteList
    {
        int Length { get; }
        byte* Ptr { get; }
        void Resize(int newLength, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory);
    }

    public static unsafe class ByteCollectionUtility
    {
        #region Read
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Read<T>(ref NativeStream.Reader stream, out T t)
            where T : unmanaged
        {
            if (stream.RemainingItemCount > 0)
            {
                t = stream.Read<T>();
                return true;
            }

            t = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Read<T>(ref UnsafeStream.Reader stream, out T t)
            where T : unmanaged
        {
            if (stream.RemainingItemCount > 0)
            {
                t = stream.Read<T>();
                return true;
            }

            t = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Read<T, S>(ref S stream, out T t)
            where T : unmanaged
            where S : unmanaged, IStreamReader
        {
            if (stream.RemainingItemCount > 0)
            {
                t = stream.Read<T>();
                return true;
            }

            t = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Read<T>(ref DynamicBuffer<byte> buffer, int startByteIndex, out int readSize, out T t)
            where T : unmanaged
        {
            readSize = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + readSize <= buffer.Length)
            {
                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)startByteIndex;
                t = *(T*)startPtr;
                return true;
            }

            t = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Read<T>(ref NativeList<byte> list, int startByteIndex, out int readSize, out T t)
            where T : unmanaged
        {
            readSize = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + readSize <= list.Length)
            {
                byte* startPtr = list.GetUnsafePtr() + (long)startByteIndex;
                t = *(T*)startPtr;
                return true;
            }

            t = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Read<T>(ref UnsafeList<byte> list, int startByteIndex, out int readSize, out T t)
            where T : unmanaged
        {
            readSize = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + readSize <= list.Length)
            {
                byte* startPtr = list.Ptr + (long)startByteIndex;
                t = *(T*)startPtr;
                return true;
            }

            t = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Read<T, L>(ref L list, int startByteIndex, out int readSize, out T t)
            where T : unmanaged
            where L : unmanaged, IByteList
        {
            readSize = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + readSize <= list.Length)
            {
                byte* startPtr = list.Ptr + (long)startByteIndex;
                t = *(T*)startPtr;
                return true;
            }

            t = default;
            return false;
        }
        #endregion

        #region ReadAsRef
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T ReadAsRef<T>(ref DynamicBuffer<byte> buffer, int startByteIndex, out int readSize, out bool success)
            where T : unmanaged
        {
            readSize = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + readSize <= buffer.Length)
            {
                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)startByteIndex;
                success = true;
                return ref *(T*)startPtr;
            }

            success = false;
            return ref *(T*)buffer.GetUnsafePtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T ReadAsRef<T>(ref NativeList<byte> list, int startByteIndex, out int readSize, out bool success)
            where T : unmanaged
        {
            readSize = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + readSize <= list.Length)
            {
                byte* startPtr = list.GetUnsafePtr() + (long)startByteIndex;
                success = true;
                return ref *(T*)startPtr;
            }

            success = false;
            return ref *(T*)list.GetUnsafePtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T ReadAsRef<T>(ref UnsafeList<byte> list, int startByteIndex, out int readSize, out bool success)
            where T : unmanaged
        {
            readSize = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + readSize <= list.Length)
            {
                byte* startPtr = list.Ptr + (long)startByteIndex;
                success = true;
                return ref *(T*)startPtr;
            }

            success = false;
            return ref *(T*)list.Ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T ReadAsRef<T, L>(ref L list, int startByteIndex, out int readSize, out bool success)
            where T : unmanaged
            where L : unmanaged, IByteList
        {
            readSize = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + readSize <= list.Length)
            {
                byte* startPtr = list.Ptr + (long)startByteIndex;
                success = true;
                return ref *(T*)startPtr;
            }

            success = false;
            return ref *(T*)list.Ptr;
        }
        #endregion

        #region Write
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteNoResize<T>(ref DynamicBuffer<byte> buffer, int startByteIndex, T t)
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + sizeOfT <= buffer.Length)
            {
                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)startByteIndex;
                *(T*)(startPtr) = t;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteNoResize<T>(ref NativeList<byte> list, int startByteIndex, T t)
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + sizeOfT <= list.Length)
            {
                byte* startPtr = list.GetUnsafePtr() + (long)startByteIndex;
                *(T*)(startPtr) = t;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteNoResize<T>(ref UnsafeList<byte> list, int startByteIndex, T t)
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + sizeOfT <= list.Length)
            {
                byte* startPtr = list.Ptr + (long)startByteIndex;
                *(T*)(startPtr) = t;
                return true;
            }
            return false;
        } 

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteNoResize<T, L>(ref L list, int startByteIndex, T t)
            where T : unmanaged
            where L : unmanaged, IByteList
        {
            int sizeOfT = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + sizeOfT <= list.Length)
            {
                byte* startPtr = list.Ptr + (long)startByteIndex;
                *(T*)(startPtr) = t;
                return true;
            }
            return false;
        }
        #endregion

        #region Add
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add<T>(ref DynamicBuffer<byte> buffer, T value)
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            int prevLength = buffer.Length;
            buffer.ResizeUninitialized(buffer.Length + sizeOfT);
            byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)prevLength;
            *(T*)(startPtr) = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add<T>(ref NativeList<byte> list, T value)
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            int prevLength = list.Length;
            list.ResizeUninitialized(list.Length + sizeOfT);
            byte* startPtr = list.GetUnsafePtr() + (long)prevLength;
            *(T*)(startPtr) = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add<T>(ref UnsafeList<byte> list, T value)
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            int prevLength = list.Length;
            int newLength = list.Length + sizeOfT;
            if (newLength > list.Capacity)
            {
                list.SetCapacity(newLength * 2); // UnsafeList resizes don't automatically add extra capacity beyond the desired length
            }
            list.Resize(newLength);
            byte* startPtr = list.Ptr + (long)prevLength;
            *(T*)(startPtr) = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add<T, L>(ref L list, T value)
            where T : unmanaged
            where L : unmanaged, IByteList
        {
            int sizeOfT = sizeof(T);
            int prevLength = list.Length;
            list.Resize(list.Length + sizeOfT);
            byte* startPtr = list.Ptr + (long)prevLength;
            *(T*)(startPtr) = value;
        }
        #endregion

        #region Insert
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Insert<T>(ref DynamicBuffer<byte> buffer, int atByteIndex, T value)
            where T : unmanaged
        {
            if (atByteIndex >= 0 && atByteIndex < buffer.Length)
            {
                int sizeOfT = sizeof(T);
                long byteSizeOfRestOfList = buffer.Length - atByteIndex;
                buffer.ResizeUninitialized(buffer.Length + sizeOfT);
                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)atByteIndex;
                byte* destPtr = startPtr + byteSizeOfRestOfList;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                *(T*)(startPtr) = value;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Insert<T>(ref NativeList<byte> list, int atByteIndex, T value)
            where T : unmanaged
        {
            if (atByteIndex >= 0 && atByteIndex < list.Length)
            {
                int sizeOfT = sizeof(T);
                long byteSizeOfRestOfList = list.Length - atByteIndex;
                list.ResizeUninitialized(list.Length + sizeOfT);
                byte* startPtr = list.GetUnsafePtr() + (long)atByteIndex;
                byte* destPtr = startPtr + byteSizeOfRestOfList;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                *(T*)(startPtr) = value;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Insert<T>(ref UnsafeList<byte> list, int atByteIndex, T value)
            where T : unmanaged
        {
            if (atByteIndex >= 0 && atByteIndex < list.Length)
            {
                int sizeOfT = sizeof(T);
                long byteSizeOfRestOfList = list.Length - atByteIndex;
                int newLength = list.Length + sizeOfT;
                if (newLength > list.Capacity)
                {
                    list.SetCapacity(newLength * 2); // UnsafeList resizes don't automatically add extra capacity beyond the desired length
                }
                list.Resize(newLength);
                byte* startPtr = list.Ptr + (long)atByteIndex;
                byte* destPtr = startPtr + byteSizeOfRestOfList;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                *(T*)(startPtr) = value;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Insert<T, L>(ref L list, int atByteIndex, T value)
            where T : unmanaged
            where L : unmanaged, IByteList
        {
            if (atByteIndex >= 0 && atByteIndex < list.Length)
            {
                int sizeOfT = sizeof(T);
                long byteSizeOfRestOfList = list.Length - atByteIndex;
                list.Resize(list.Length + sizeOfT);
                byte* startPtr = list.Ptr + (long)atByteIndex;
                byte* destPtr = startPtr + byteSizeOfRestOfList;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                *(T*)(startPtr) = value;
                return true;
            }
            return false;
        }
        #endregion

        #region Remove
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Remove<T>(ref DynamicBuffer<byte> buffer, int atByteIndex)
            where T : unmanaged
        {
            return RemoveSize(ref buffer, atByteIndex, sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Remove<T>(ref NativeList<byte> list, int atByteIndex)
            where T : unmanaged
        {
            return RemoveSize(ref list, atByteIndex, sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Remove<T>(ref UnsafeList<byte> list, int atByteIndex)
            where T : unmanaged
        {
            return RemoveSize(ref list, atByteIndex, sizeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Remove<T, L>(ref L list, int atByteIndex)
            where T : unmanaged
            where L : unmanaged, IByteList
        {
            return RemoveSize(ref list, atByteIndex, sizeof(T));
        }
        #endregion

        #region RemoveSize
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveSize(ref DynamicBuffer<byte> buffer, int atByteIndex, int size)
        {
            if (atByteIndex >= 0 && atByteIndex + size <= buffer.Length)
            {
                long byteSizeOfRestOfList = buffer.Length - (atByteIndex + size);
                byte* destPtr = (byte*)buffer.GetUnsafePtr() + (long)atByteIndex;
                byte* startPtr = destPtr + size;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                buffer.ResizeUninitialized(buffer.Length - size);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveSize(ref NativeList<byte> list, int atByteIndex, int size)
        {
            if (atByteIndex >= 0 && atByteIndex + size <= list.Length)
            {
                long byteSizeOfRestOfList = list.Length - (atByteIndex + size);
                byte* destPtr = list.GetUnsafePtr() + (long)atByteIndex;
                byte* startPtr = destPtr + size;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                list.ResizeUninitialized(list.Length - size);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveSize(ref UnsafeList<byte> list, int atByteIndex, int size)
        {
            if (atByteIndex >= 0 && atByteIndex + size <= list.Length)
            {
                long byteSizeOfRestOfList = list.Length - (atByteIndex + size);
                byte* destPtr = list.Ptr + (long)atByteIndex;
                byte* startPtr = destPtr + size;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                list.Resize(list.Length - size);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveSize<L>(ref L list, int atByteIndex, int size)
            where L : unmanaged, IByteList
        {
            if (atByteIndex >= 0 && atByteIndex + size <= list.Length)
            {
                long byteSizeOfRestOfList = list.Length - (atByteIndex + size);
                byte* destPtr = list.Ptr + (long)atByteIndex;
                byte* startPtr = destPtr + size;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                list.Resize(list.Length - size);
                return true;
            }
            return false;
        }
        #endregion
    }
}