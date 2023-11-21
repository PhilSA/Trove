
using Unity.Entities;
using Unity.Collections;
using Unity.Logging;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.CompilerServices;

namespace Trove.PolymorphicElements
{
    public unsafe interface IStreamWriterWrapper
    {
        public void Write<T>(T value) where T : unmanaged;
        public byte* Allocate(int size);
    }

    public unsafe interface IStreamReaderWrapper
    {
        public int RemainingItemCount();
        public T Read<T>() where T : unmanaged;
        public byte* ReadPtr(int size);
    }

    public unsafe interface IByteCollectionWrapper
    {
        public byte* Ptr();
        public int Length();
        public void Resize(int newLength);
    }

    public struct NativeStreamWriterWrapper : IStreamWriterWrapper
    {
        public NativeStream.Writer StreamWriter;

        public NativeStreamWriterWrapper(NativeStream.Writer streamWriter)
        {
            StreamWriter = streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* Allocate(int size)
        {
            return StreamWriter.Allocate(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(T value) where T : unmanaged
        {
            StreamWriter.Write(value);
        }
    }

    public struct UnsafeStreamWriterWrapper : IStreamWriterWrapper
    {
        public UnsafeStream.Writer StreamWriter;

        public UnsafeStreamWriterWrapper(UnsafeStream.Writer streamWriter)
        {
            StreamWriter = streamWriter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* Allocate(int size)
        {
            return StreamWriter.Allocate(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write<T>(T value) where T : unmanaged
        {
            StreamWriter.Write(value);
        }
    }

    public struct NativeStreamReaderWrapper : IStreamReaderWrapper
    {
        public NativeStream.Reader StreamReader;

        public NativeStreamReaderWrapper(NativeStream.Reader streamReader)
        {
            StreamReader = streamReader;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T>() where T : unmanaged
        {
            return StreamReader.Read<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* ReadPtr(int size)
        {
            return StreamReader.ReadUnsafePtr(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int RemainingItemCount()
        {
            return StreamReader.RemainingItemCount;
        }
    }

    public struct UnsafeStreamReaderWrapper : IStreamReaderWrapper
    {
        public UnsafeStream.Reader StreamReader;

        public UnsafeStreamReaderWrapper(UnsafeStream.Reader streamReader)
        {
            StreamReader = streamReader;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read<T>() where T : unmanaged
        {
            return StreamReader.Read<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* ReadPtr(int size)
        {
            return StreamReader.ReadUnsafePtr(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int RemainingItemCount()
        {
            return StreamReader.RemainingItemCount;
        }
    }

    public struct DynamicBufferWrapper<T> : IByteCollectionWrapper
        where T : unmanaged
    {
        public DynamicBuffer<T> Buffer;

        public DynamicBufferWrapper(DynamicBuffer<T> buffer)
        {
            Buffer = buffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Length()
        {
            return Buffer.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* Ptr()
        {
            return (byte*)Buffer.GetUnsafePtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Resize(int newLength)
        {
            Buffer.ResizeUninitialized(newLength);
        }
    }

    public struct NativeListWrapper<T> : IByteCollectionWrapper
        where T : unmanaged
    {
        public NativeList<T> List;

        public NativeListWrapper(NativeList<T> list)
        {
            List = list;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Length()
        {
            return List.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* Ptr()
        {
            return (byte*)List.GetUnsafePtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Resize(int newLength)
        {
            List.ResizeUninitialized(newLength);
        }
    }

    public struct UnsafeListWrapper<T> : IByteCollectionWrapper
        where T : unmanaged
    {
        public UnsafeList<T> List;

        public UnsafeListWrapper(UnsafeList<T> list)
        {
            List = list;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Length()
        {
            return List.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe byte* Ptr()
        {
            return (byte*)List.Ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Resize(int newLength)
        {
            List.Resize(newLength);
        }
    }

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

    public static unsafe class PolymorphicElementsUtility
    {
        public const int SizeOfElementTypeId = sizeof(ushort);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddStreamElement<C, T>(ref C stream, T writer)
            where C : unmanaged, IStreamWriterWrapper
            where T : unmanaged, IPolymorphicElementWriter
        {
            byte* ptr = stream.Allocate(writer.GetTotalSize());
            writer.Write(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElement<C, T>(ref C collection, T writer)
            where C : unmanaged, IByteCollectionWrapper
            where T : unmanaged, IPolymorphicElementWriter
        {
            int totalElementSize = writer.GetTotalSize();
            int lengthBeforeResize = collection.Length();
            collection.Resize(lengthBeforeResize + totalElementSize);
            byte* ptr = collection.Ptr() + (long)lengthBeforeResize;
            writer.Write(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElementGetMetaData<C, T>(ref C collection, T writer, out PolymorphicElementMetaData metaData)
            where C : unmanaged, IByteCollectionWrapper
            where T : unmanaged, IPolymorphicElementWriter
        {
            int totalElementSize = writer.GetTotalSize();
            int lengthBeforeResize = collection.Length();
            metaData = new PolymorphicElementMetaData
            {
                TypeId = writer.GetTypeId(),
                StartByteIndex = lengthBeforeResize,
                TotalSizeWithId = totalElementSize,
            };
            collection.Resize(lengthBeforeResize + totalElementSize);
            byte* ptr = collection.Ptr() + (long)lengthBeforeResize;
            writer.Write(ptr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsertElement<C, T>(ref C collection, T writer, int atByteIndex)
            where C : unmanaged, IByteCollectionWrapper
            where T : unmanaged, IPolymorphicElementWriter
        {
            int lengthBeforeResize = collection.Length();
            if (atByteIndex >= 0 && atByteIndex < lengthBeforeResize)
            {
                long byteSizeOfRestOfList = lengthBeforeResize - atByteIndex;
                collection.Resize(lengthBeforeResize + writer.GetTotalSize());
                byte* startPtr = collection.Ptr() + (long)atByteIndex;
                byte* destPtr = startPtr + byteSizeOfRestOfList;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                writer.Write(startPtr);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsertElementGetMetaData<C, T>(ref C collection, T writer, int atByteIndex, out PolymorphicElementMetaData metaData)
            where C : unmanaged, IByteCollectionWrapper
            where T : unmanaged, IPolymorphicElementWriter
        {
            int lengthBeforeResize = collection.Length();
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
                collection.Resize(lengthBeforeResize + totalElementSize);
                byte* startPtr = collection.Ptr() + (long)atByteIndex;
                byte* destPtr = startPtr + byteSizeOfRestOfList;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                writer.Write(startPtr);
                return true;
            }
            metaData = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValueNoResize<C, T>(ref C collection, int elementStartIndex, T value)
            where C : unmanaged, IByteCollectionWrapper
            where T : unmanaged
        {
            elementStartIndex += SizeOfElementTypeId;
            int sizeOfT = sizeof(T);
            if (elementStartIndex >= 0 && elementStartIndex + sizeOfT <= collection.Length())
            {
                byte* startPtr = collection.Ptr() + (long)elementStartIndex;
                *(T*)(startPtr) = value;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadElementValue<C, T>(ref C collection, int startByteIndex, out int readSize, out T value)
            where C : unmanaged, IByteCollectionWrapper
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + sizeOfT <= collection.Length())
            {
                byte* startPtr = collection.Ptr() + (long)(startByteIndex + sizeOfT);
                value = *(T*)startPtr;
                readSize = SizeOfElementTypeId + sizeOfT;
                return true;
            }
            value = default;
            readSize = 0;
            return false;
        }

        /// <summary>
        /// Unsafe because if the collection gets moved in memory after the ref is gotten, 
        /// changing the ref's value will change invalid memory
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T ReadElementValueAsRefUnsafe<C, T>(ref C collection, int startByteIndex, out int readSize, out bool success)
            where C : unmanaged, IByteCollectionWrapper
            where T : unmanaged
        {
            int sizeOfT = sizeof(T);
            if (startByteIndex >= 0 && startByteIndex + sizeOfT <= collection.Length())
            {
                byte* startPtr = collection.Ptr() + (long)(startByteIndex + sizeOfT);
                readSize = SizeOfElementTypeId + sizeOfT;
                success = true;
                return ref *(T*)startPtr;
            }
            success = false;
            readSize = 0;
            return ref *(T*)collection.Ptr();
        }

        /// <summary>
        /// Removes element based on provided size and preserves ordering of following elements
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveElement<C, T>(ref C collection, int atByteIndex, int size, PolymorphicElementMetaData elementMetaData)
            where C : unmanaged, IByteCollectionWrapper
            where T : unmanaged
        {
            int collectionLengthBeforeResize = collection.Length();
            if (atByteIndex >= 0 && atByteIndex + size <= collectionLengthBeforeResize)
            {
                long byteSizeOfRestOfList = collectionLengthBeforeResize - (atByteIndex + size);
                byte* destPtr = collection.Ptr() + (long)atByteIndex;
                byte* startPtr = destPtr + size;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                collection.Resize(collectionLengthBeforeResize - size);
                return true;
            }
            return false;
        }
    }
}