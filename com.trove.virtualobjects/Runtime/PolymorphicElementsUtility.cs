
using Unity.Entities;
using Unity.Collections;
using Unity.Logging;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.CompilerServices;

namespace Trove.PolymorphicElements
{
    public struct PolymorphicElementMetaData
    {
        public ushort TypeId;
        public int StartByteIndex;
        public int TotalSizeWithId;

        public bool IsValid()
        {
            return TotalSizeWithId > 0;
        }
    }

    public static unsafe partial class PolymorphicElementsUtility
    {
        public const int SizeOfElementTypeId = sizeof(ushort);

        #region AddStreamElement
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddStreamElement<T>(ref NativeStream.Writer stream, T writer)
            where T : unmanaged, IPolymorphicElementWriter
        {
            stream.Write(writer.GetTypeId());
            stream.Write(writer);
        }

        public static void AddStreamElement<T>(ref UnsafeStream.Writer stream, T writer)
            where T : unmanaged, IPolymorphicElementWriter
        {
            stream.Write(writer.GetTypeId());
            stream.Write(writer);
        }

        public static void AddStreamElement<C, T>(ref C stream, T writer)
            where C : unmanaged, IStreamWriter
            where T : unmanaged, IPolymorphicElementWriter
        {
            stream.Write(writer.GetTypeId());
            stream.Write(writer);
        }
        #endregion

        #region AddElement
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElement<T>(ref DynamicBuffer<byte> buffer, T writer)
            where T : unmanaged, IPolymorphicElementWriter
        {
            int totalElementSize = writer.GetTotalSize();
            int lengthBeforeResize = buffer.Length;
            buffer.ResizeUninitialized(lengthBeforeResize + totalElementSize);
            byte* ptr = (byte*)buffer.GetUnsafePtr() + (long)lengthBeforeResize;
            writer.Write(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElement<T>(ref NativeList<byte> list, T writer)
            where T : unmanaged, IPolymorphicElementWriter
        {
            int totalElementSize = writer.GetTotalSize();
            int lengthBeforeResize = list.Length;
            list.ResizeUninitialized(lengthBeforeResize + totalElementSize);
            byte* ptr = list.GetUnsafePtr() + (long)lengthBeforeResize;
            writer.Write(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElement<T>(ref UnsafeList<byte> list, T writer)
            where T : unmanaged, IPolymorphicElementWriter
        {
            int totalElementSize = writer.GetTotalSize();
            int lengthBeforeResize = list.Length;
            list.Resize(lengthBeforeResize + totalElementSize);
            byte* ptr = list.Ptr + (long)lengthBeforeResize;
            writer.Write(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElement<C, T>(ref C list, T writer)
            where C : unmanaged, IByteList
            where T : unmanaged, IPolymorphicElementWriter
        {
            int totalElementSize = writer.GetTotalSize();
            int lengthBeforeResize = list.Length;
            list.Resize(lengthBeforeResize + totalElementSize);
            byte* ptr = list.Ptr + (long)lengthBeforeResize;
            writer.Write(ptr);
        }
        #endregion

        #region AddElementGetMetaData
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElementGetMetaData<T>(ref DynamicBuffer<byte> buffer, T writer, out PolymorphicElementMetaData metaData)
            where T : unmanaged, IPolymorphicElementWriter
        {
            int totalElementSize = writer.GetTotalSize();
            int lengthBeforeResize = buffer.Length;
            metaData = new PolymorphicElementMetaData
            {
                TypeId = writer.GetTypeId(),
                StartByteIndex = lengthBeforeResize,
                TotalSizeWithId = totalElementSize,
            };
            buffer.ResizeUninitialized(lengthBeforeResize + totalElementSize);
            byte* ptr = (byte*)buffer.GetUnsafePtr() + (long)lengthBeforeResize;
            writer.Write(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElementGetMetaData<T>(ref NativeList<byte> list, T writer, out PolymorphicElementMetaData metaData)
            where T : unmanaged, IPolymorphicElementWriter
        {
            int totalElementSize = writer.GetTotalSize();
            int lengthBeforeResize = list.Length;
            metaData = new PolymorphicElementMetaData
            {
                TypeId = writer.GetTypeId(),
                StartByteIndex = lengthBeforeResize,
                TotalSizeWithId = totalElementSize,
            };
            list.ResizeUninitialized(lengthBeforeResize + totalElementSize);
            byte* ptr = list.GetUnsafePtr() + (long)lengthBeforeResize;
            writer.Write(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElementGetMetaData<T>(ref UnsafeList<byte> list, T writer, out PolymorphicElementMetaData metaData)
            where T : unmanaged, IPolymorphicElementWriter
        {
            int totalElementSize = writer.GetTotalSize();
            int lengthBeforeResize = list.Length;
            metaData = new PolymorphicElementMetaData
            {
                TypeId = writer.GetTypeId(),
                StartByteIndex = lengthBeforeResize,
                TotalSizeWithId = totalElementSize,
            };
            list.Resize(lengthBeforeResize + totalElementSize);
            byte* ptr = list.Ptr + (long)lengthBeforeResize;
            writer.Write(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElementGetMetaData<C, T>(ref C list, T writer, out PolymorphicElementMetaData metaData)
            where C : unmanaged, IByteList
            where T : unmanaged, IPolymorphicElementWriter
        {
            int totalElementSize = writer.GetTotalSize();
            int lengthBeforeResize = list.Length;
            metaData = new PolymorphicElementMetaData
            {
                TypeId = writer.GetTypeId(),
                StartByteIndex = lengthBeforeResize,
                TotalSizeWithId = totalElementSize,
            };
            list.Resize(lengthBeforeResize + totalElementSize);
            byte* ptr = list.Ptr + (long)lengthBeforeResize;
            writer.Write(ptr);
        }
        #endregion

        #region InsertElement
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsertElement<T>(ref DynamicBuffer<byte> buffer, T writer, int atByteIndex)
            where T : unmanaged, IPolymorphicElementWriter
        {
            int lengthBeforeResize = buffer.Length;
            if (atByteIndex >= 0 && atByteIndex < lengthBeforeResize)
            {
                long byteSizeOfRestOfList = lengthBeforeResize - atByteIndex;
                buffer.ResizeUninitialized(lengthBeforeResize + writer.GetTotalSize());
                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)atByteIndex;
                byte* destPtr = startPtr + byteSizeOfRestOfList;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                writer.Write(startPtr);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsertElement<T>(ref NativeList<byte> list, T writer, int atByteIndex)
            where T : unmanaged, IPolymorphicElementWriter
        {
            int lengthBeforeResize = list.Length;
            if (atByteIndex >= 0 && atByteIndex < lengthBeforeResize)
            {
                long byteSizeOfRestOfList = lengthBeforeResize - atByteIndex;
                list.ResizeUninitialized(lengthBeforeResize + writer.GetTotalSize());
                byte* startPtr = list.GetUnsafePtr() + (long)atByteIndex;
                byte* destPtr = startPtr + byteSizeOfRestOfList;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                writer.Write(startPtr);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsertElement<T>(ref UnsafeList<byte> collection, T writer, int atByteIndex)
            where T : unmanaged, IPolymorphicElementWriter
        {
            int lengthBeforeResize = collection.Length;
            if (atByteIndex >= 0 && atByteIndex < lengthBeforeResize)
            {
                long byteSizeOfRestOfList = lengthBeforeResize - atByteIndex;
                collection.Resize(lengthBeforeResize + writer.GetTotalSize());
                byte* startPtr = collection.Ptr + (long)atByteIndex;
                byte* destPtr = startPtr + byteSizeOfRestOfList;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                writer.Write(startPtr);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsertElement<C, T>(ref C collection, T writer, int atByteIndex)
            where C : unmanaged, IByteList
            where T : unmanaged, IPolymorphicElementWriter
        {
            int lengthBeforeResize = collection.Length;
            if (atByteIndex >= 0 && atByteIndex < lengthBeforeResize)
            {
                long byteSizeOfRestOfList = lengthBeforeResize - atByteIndex;
                collection.Resize(lengthBeforeResize + writer.GetTotalSize());
                byte* startPtr = collection.Ptr + (long)atByteIndex;
                byte* destPtr = startPtr + byteSizeOfRestOfList;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                writer.Write(startPtr);
                return true;
            }
            return false;
        }
        #endregion

        #region InsertElementGetMetaData
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsertElementGetMetaData<T>(ref DynamicBuffer<byte> buffer, T writer, int atByteIndex, out PolymorphicElementMetaData metaData)
            where T : unmanaged, IPolymorphicElementWriter
        {
            int lengthBeforeResize = buffer.Length;
            if (atByteIndex >= 0 && atByteIndex < lengthBeforeResize)
            {
                int totalElementSize = writer.GetTotalSize();
                metaData = new PolymorphicElementMetaData
                {
                    TypeId = writer.GetTypeId(),
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = totalElementSize,
                };
                long byteSizeOfRestOfList = lengthBeforeResize - atByteIndex;
                buffer.ResizeUninitialized(lengthBeforeResize + totalElementSize);
                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)atByteIndex;
                byte* destPtr = startPtr + byteSizeOfRestOfList;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                writer.Write(startPtr);
                return true;
            }
            metaData = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsertElementGetMetaData<T>(ref NativeList<byte> list, T writer, int atByteIndex, out PolymorphicElementMetaData metaData)
            where T : unmanaged, IPolymorphicElementWriter
        {
            int lengthBeforeResize = list.Length;
            if (atByteIndex >= 0 && atByteIndex < lengthBeforeResize)
            {
                int totalElementSize = writer.GetTotalSize();
                metaData = new PolymorphicElementMetaData
                {
                    TypeId = writer.GetTypeId(),
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = totalElementSize,
                };
                long byteSizeOfRestOfList = lengthBeforeResize - atByteIndex;
                list.ResizeUninitialized(lengthBeforeResize + totalElementSize);
                byte* startPtr = list.GetUnsafePtr() + (long)atByteIndex;
                byte* destPtr = startPtr + byteSizeOfRestOfList;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                writer.Write(startPtr);
                return true;
            }
            metaData = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsertElementGetMetaData<T>(ref UnsafeList<byte> list, T writer, int atByteIndex, out PolymorphicElementMetaData metaData)
            where T : unmanaged, IPolymorphicElementWriter
        {
            int lengthBeforeResize = list.Length;
            if (atByteIndex >= 0 && atByteIndex < lengthBeforeResize)
            {
                int totalElementSize = writer.GetTotalSize();
                metaData = new PolymorphicElementMetaData
                {
                    TypeId = writer.GetTypeId(),
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = totalElementSize,
                };
                long byteSizeOfRestOfList = lengthBeforeResize - atByteIndex;
                list.Resize(lengthBeforeResize + totalElementSize);
                byte* startPtr = list.Ptr + (long)atByteIndex;
                byte* destPtr = startPtr + byteSizeOfRestOfList;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                writer.Write(startPtr);
                return true;
            }
            metaData = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsertElementGetMetaData<C, T>(ref C list, T writer, int atByteIndex, out PolymorphicElementMetaData metaData)
            where C : unmanaged, IByteList
            where T : unmanaged, IPolymorphicElementWriter
        {
            int lengthBeforeResize = list.Length;
            if (atByteIndex >= 0 && atByteIndex < lengthBeforeResize)
            {
                int totalElementSize = writer.GetTotalSize();
                metaData = new PolymorphicElementMetaData
                {
                    TypeId = writer.GetTypeId(),
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = totalElementSize,
                };
                long byteSizeOfRestOfList = lengthBeforeResize - atByteIndex;
                list.Resize(lengthBeforeResize + totalElementSize);
                byte* startPtr = list.Ptr + (long)atByteIndex;
                byte* destPtr = startPtr + byteSizeOfRestOfList;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                writer.Write(startPtr);
                return true;
            }
            metaData = default;
            return false;
        }
        #endregion

        #region WriteElementValueNoResize
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValueNoResize<T>(ref DynamicBuffer<byte> buffer, int elementStartIndex, T value)
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            if (elementStartIndex >= 0 && elementStartIndex + SizeOfElementTypeId + sizeOfT <= buffer.Length)
            {
                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)(elementStartIndex + SizeOfElementTypeId);
                *(T*)(startPtr) = value;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValueNoResize<T>(ref NativeList<byte> list, int elementStartIndex, T value)
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            if (elementStartIndex >= 0 && elementStartIndex + SizeOfElementTypeId + sizeOfT <= list.Length)
            {
                byte* startPtr = list.GetUnsafePtr() + (long)(elementStartIndex + SizeOfElementTypeId);
                *(T*)(startPtr) = value;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValueNoResize<T>(ref UnsafeList<byte> list, int elementStartIndex, T value)
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            if (elementStartIndex >= 0 && elementStartIndex + SizeOfElementTypeId + sizeOfT <= list.Length)
            {
                byte* startPtr = list.Ptr + (long)(elementStartIndex + SizeOfElementTypeId);
                *(T*)(startPtr) = value;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValueNoResize<C, T>(ref C list, int elementStartIndex, T value)
            where C : unmanaged, IByteList
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            if (elementStartIndex >= 0 && elementStartIndex + SizeOfElementTypeId + sizeOfT <= list.Length)
            {
                byte* startPtr = list.Ptr + (long)(elementStartIndex + SizeOfElementTypeId);
                *(T*)(startPtr) = value;
                return true;
            }
            return false;
        }
        #endregion

        #region ReadElementValue
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadElementValue<T>(ref DynamicBuffer<byte> buffer, int startByteIndex, out T value)
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + SizeOfElementTypeId + sizeOfT <= buffer.Length)
            {
                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)(startByteIndex + SizeOfElementTypeId);
                value = *(T*)startPtr;
                return true;
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadElementValue<T>(ref NativeList<byte> list, int startByteIndex, out T value)
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + SizeOfElementTypeId + sizeOfT <= list.Length)
            {
                byte* startPtr = list.GetUnsafePtr() + (long)(startByteIndex + SizeOfElementTypeId);
                value = *(T*)startPtr;
                return true;
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadElementValue<T>(ref UnsafeList<byte> list, int startByteIndex, out T value)
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + SizeOfElementTypeId + sizeOfT <= list.Length)
            {
                byte* startPtr = list.Ptr + (long)(startByteIndex + SizeOfElementTypeId);
                value = *(T*)startPtr;
                return true;
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadElementValue<C, T>(ref C list, int startByteIndex, out T value)
            where C : unmanaged, IByteList
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + SizeOfElementTypeId + sizeOfT <= list.Length)
            {
                byte* startPtr = list.Ptr + (long)(startByteIndex + SizeOfElementTypeId);
                value = *(T*)startPtr;
                return true;
            }
            value = default;
            return false;
        }
        #endregion

        #region ReadElementValueAsRefUnsafe
        /// <summary>
        /// Unsafe because if the collection gets moved in memory after the ref is gotten, 
        /// changing the ref's value will change invalid memory
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T ReadElementValueAsRefUnsafe<T>(ref DynamicBuffer<byte> buffer, int startByteIndex, out bool success)
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + sizeOfT <= buffer.Length)
            {
                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)(startByteIndex + SizeOfElementTypeId);
                success = true;
                return ref *(T*)startPtr;
            }
            success = false;
            return ref *(T*)buffer.GetUnsafePtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T ReadElementValueAsRefUnsafe<T>(ref NativeList<byte> list, int startByteIndex, out bool success)
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + sizeOfT <= list.Length)
            {
                byte* startPtr = list.GetUnsafePtr() + (long)(startByteIndex + SizeOfElementTypeId);
                success = true;
                return ref *(T*)startPtr;
            }
            success = false;
            return ref *(T*)list.GetUnsafePtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T ReadElementValueAsRefUnsafe<T>(ref UnsafeList<byte> list, int startByteIndex, out bool success)
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + sizeOfT <= list.Length)
            {
                byte* startPtr = list.Ptr + (long)(startByteIndex + SizeOfElementTypeId);
                success = true;
                return ref *(T*)startPtr;
            }
            success = false;
            return ref *(T*)list.Ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T ReadElementValueAsRefUnsafe<C, T>(ref C collection, int startByteIndex, out bool success)
            where C : unmanaged, IByteList
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + sizeOfT <= collection.Length)
            {
                byte* startPtr = collection.Ptr + (long)(startByteIndex + SizeOfElementTypeId);
                success = true;
                return ref *(T*)startPtr;
            }
            success = false;
            return ref *(T*)collection.Ptr;
        }
        #endregion

        #region RemoveElement
        /// <summary>
        /// Removes element based on provided size and preserves ordering of following elements
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveElement<T>(ref DynamicBuffer<byte> buffer, int atByteIndex, int size)
            where T : unmanaged
        {
            int collectionLengthBeforeResize = buffer.Length;
            if (atByteIndex >= 0 && atByteIndex + size <= collectionLengthBeforeResize)
            {
                long byteSizeOfRestOfList = collectionLengthBeforeResize - (atByteIndex + size);
                byte* destPtr = (byte*)buffer.GetUnsafePtr() + (long)atByteIndex;
                byte* startPtr = destPtr + size;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                buffer.ResizeUninitialized(collectionLengthBeforeResize - size);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveElement<T>(ref NativeList<byte> list, int atByteIndex, int size)
            where T : unmanaged
        {
            int collectionLengthBeforeResize = list.Length;
            if (atByteIndex >= 0 && atByteIndex + size <= collectionLengthBeforeResize)
            {
                long byteSizeOfRestOfList = collectionLengthBeforeResize - (atByteIndex + size);
                byte* destPtr = list.GetUnsafePtr() + (long)atByteIndex;
                byte* startPtr = destPtr + size;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                list.ResizeUninitialized(collectionLengthBeforeResize - size);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveElement<T>(ref UnsafeList<byte> list, int atByteIndex, int size)
            where T : unmanaged
        {
            int collectionLengthBeforeResize = list.Length;
            if (atByteIndex >= 0 && atByteIndex + size <= collectionLengthBeforeResize)
            {
                long byteSizeOfRestOfList = collectionLengthBeforeResize - (atByteIndex + size);
                byte* destPtr = list.Ptr + (long)atByteIndex;
                byte* startPtr = destPtr + size;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                list.Resize(collectionLengthBeforeResize - size);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveElement<C, T>(ref C list, int atByteIndex, int size)
            where C : unmanaged, IByteList
            where T : unmanaged
        {
            int collectionLengthBeforeResize = list.Length;
            if (atByteIndex >= 0 && atByteIndex + size <= collectionLengthBeforeResize)
            {
                long byteSizeOfRestOfList = collectionLengthBeforeResize - (atByteIndex + size);
                byte* destPtr = list.Ptr + (long)atByteIndex;
                byte* startPtr = destPtr + size;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                list.Resize(collectionLengthBeforeResize - size);
                return true;
            }
            return false;
        }
        #endregion
    }
}