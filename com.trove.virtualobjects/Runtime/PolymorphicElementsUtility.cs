
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

    public unsafe struct PolymorphicElementPtr
    {
        public byte* Ptr;

        public static implicit operator byte*(PolymorphicElementPtr e) => e.Ptr;
        public static implicit operator PolymorphicElementPtr(byte* e) => new PolymorphicElementPtr { Ptr = e };
    }

    public static unsafe class PolymorphicElementsUtility
    {
        public const int SizeOfElementTypeId = sizeof(ushort);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CheckIndexValid(int index, int length)
        {
            return index >= 0 && index < length;
        }

        #region GetPtr
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetPtrOfNextStreamElement(NativeStream.Reader stream, out PolymorphicElementPtr ptr)
        {
            if (stream.RemainingItemCount > 1)
            {
                int elementSize = stream.Read<int>();
                ptr = stream.ReadUnsafePtr(elementSize);
                return true;
            }
            ptr = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetPtrOfNextStreamElement(UnsafeStream.Reader stream, out PolymorphicElementPtr ptr)
        {
            if (stream.RemainingItemCount > 1)
            {
                int elementSize = stream.Read<int>();
                ptr = stream.ReadUnsafePtr(elementSize);
                return true;
            }
            ptr = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetPtrOfByteIndex(DynamicBuffer<byte> buffer, int byteIndex, out PolymorphicElementPtr ptr)
        {
            if (CheckIndexValid(byteIndex, buffer.Length))
            {
                ptr = (byte*)buffer.GetUnsafePtr() + (long)byteIndex;
                return true;
            }
            ptr = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetPtrOfByteIndex(NativeList<byte> list, int byteIndex, out PolymorphicElementPtr ptr)
        {
            if (CheckIndexValid(byteIndex, list.Length))
            {
                ptr = list.GetUnsafePtr() + (long)byteIndex;
                return true;
            }
            ptr = default;
            return false;
        }
        #endregion

        #region AddElement
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElement<T>(ref NativeStream.Writer stream, T writer)
            where T : unmanaged, IPolymorphicElementWriter
        {
            stream.Write(writer.GetTotalSize());
            byte* startPtr = stream.Allocate(writer.GetTotalSize());
            writer.Write(startPtr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElement<T>(ref DynamicBuffer<byte> buffer, T writer)
            where T : unmanaged, IPolymorphicElementWriter
        {
            int totalElementSize = writer.GetTotalSize();
            int prevLength = buffer.Length;
            buffer.ResizeUninitialized(buffer.Length + totalElementSize);
            byte* writePtr = (byte*)buffer.GetUnsafePtr() + (long)prevLength;
            writer.Write(writePtr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElementGetMetaData<T>(ref DynamicBuffer<byte> buffer, T writer, out PolymorphicElementMetaData metaData)
            where T : unmanaged, IPolymorphicElementWriter
        {
            int totalElementSize = writer.GetTotalSize();
            metaData = new PolymorphicElementMetaData
            {
                TypeId = writer.GetTypeId(),
                StartByteIndex = buffer.Length,
                TotalSizeWithId = totalElementSize,
            };
            int prevLength = buffer.Length;
            buffer.ResizeUninitialized(buffer.Length + totalElementSize);
            byte* writePtr = (byte*)buffer.GetUnsafePtr() + (long)prevLength;
            writer.Write(writePtr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElement<T>(ref NativeList<byte> list, T writer)
            where T : unmanaged, IPolymorphicElementWriter
        {
            int totalElementSize = writer.GetTotalSize();
            int prevLength = list.Length;
            list.ResizeUninitialized(list.Length + totalElementSize);
            byte* writePtr = list.GetUnsafePtr() + (long)prevLength;
            writer.Write(writePtr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElementGetMetaData<T>(ref NativeList<byte> list, T writer, out PolymorphicElementMetaData metaData)
            where T : unmanaged, IPolymorphicElementWriter
        {
            int totalElementSize = writer.GetTotalSize();
            metaData = new PolymorphicElementMetaData
            {
                TypeId = writer.GetTypeId(),
                StartByteIndex = list.Length,
                TotalSizeWithId = totalElementSize,
            };
            int prevLength = list.Length;
            list.ResizeUninitialized(list.Length + totalElementSize);
            byte* writePtr = list.GetUnsafePtr() + (long)prevLength;
            writer.Write(writePtr);
        }
        #endregion

        #region InsertElement
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsertElement<T>(ref DynamicBuffer<byte> buffer, int atByteIndex, T writer)
            where T : unmanaged, IPolymorphicElementWriter
        {
            if (CheckIndexValid(atByteIndex, buffer.Length))
            {
                long byteSizeOfRestOfList = buffer.Length - atByteIndex;
                buffer.ResizeUninitialized(buffer.Length + writer.GetTotalSize());
                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)atByteIndex;
                byte* destPtr = startPtr + byteSizeOfRestOfList;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                writer.Write(startPtr);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InsertElementGetMetaData<T>(ref DynamicBuffer<byte> buffer, int atByteIndex, T writer, out PolymorphicElementMetaData metaData)
            where T : unmanaged, IPolymorphicElementWriter
        {
            if (CheckIndexValid(atByteIndex, buffer.Length))
            {
                int totalElementSize = writer.GetTotalSize();
                metaData = new PolymorphicElementMetaData
                {
                    TypeId = writer.GetTypeId(),
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = totalElementSize,
                };
                long byteSizeOfRestOfList = buffer.Length - atByteIndex;
                buffer.ResizeUninitialized(buffer.Length + totalElementSize);
                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)atByteIndex;
                byte* destPtr = startPtr + byteSizeOfRestOfList;
                UnsafeUtility.MemCpy(destPtr, startPtr, byteSizeOfRestOfList);
                writer.Write(startPtr);
                return true;
            }
            metaData = default;
            return false;
        }
        #endregion



















        #region WriteValueNoResize
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValueNoResize<T>(ref DynamicBuffer<byte> buffer, int elementStartIndex, T value)
            where T : unmanaged
        {
            elementStartIndex += SizeOfElementTypeId;
            return ByteCollectionUtility.WriteNoResize(ref buffer, elementStartIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValueNoResize<T, B>(ref DynamicBuffer<B> buffer, int elementStartIndex, T value)
            where T : unmanaged
            where B : unmanaged, IBufferElementData, IByteBufferElement
        {
            elementStartIndex += SizeOfElementTypeId;
            return ByteCollectionUtility.WriteNoResize(ref buffer, elementStartIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValueNoResize<T>(ref NativeList<byte> list, int elementStartIndex, T value)
            where T : unmanaged
        {
            elementStartIndex += SizeOfElementTypeId;
            return ByteCollectionUtility.WriteNoResize(ref list, elementStartIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValueNoResize<T>(ref UnsafeList<byte> list, int elementStartIndex, T value)
            where T : unmanaged
        {
            elementStartIndex += SizeOfElementTypeId;
            return ByteCollectionUtility.WriteNoResize(ref list, elementStartIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValueNoResize<T, L>(ref L list, int elementStartIndex, T value)
            where T : unmanaged
            where L : unmanaged, IByteList
        {
            elementStartIndex += SizeOfElementTypeId;
            return ByteCollectionUtility.WriteNoResize(ref list, elementStartIndex, value);
        }
        #endregion

        #region Read
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadElementValue<T>(ref DynamicBuffer<byte> buffer, int startByteIndex, out int newStartByteIndex, out T value)
            where T : unmanaged
        {
            if(ByteCollectionUtility.Read(ref buffer, startByteIndex + SizeOfElementTypeId, out newStartByteIndex, out value))
            {
                return true;
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadElementValue<T, B>(ref DynamicBuffer<B> buffer, int startByteIndex, out int newStartByteIndex, out T value)
            where T : unmanaged
            where B : unmanaged, IBufferElementData, IByteBufferElement
        {
            if (ByteCollectionUtility.Read(ref buffer, startByteIndex + SizeOfElementTypeId, out newStartByteIndex, out value))
            {
                return true;
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadElementValue<T>(ref NativeList<byte> list, int startByteIndex, out int newStartByteIndex, out T value)
            where T : unmanaged
        {
            if (ByteCollectionUtility.Read(ref list, startByteIndex + SizeOfElementTypeId, out newStartByteIndex, out value))
            {
                return true;
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadElementValue<T>(ref UnsafeList<byte> list, int startByteIndex, out int newStartByteIndex, out T value)
            where T : unmanaged
        {
            if (ByteCollectionUtility.Read(ref list, startByteIndex + SizeOfElementTypeId, out newStartByteIndex, out value))
            {
                return true;
            }
            value = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadElementValue<T, L>(ref L list, int startByteIndex, out int newStartByteIndex, out T value)
            where T : unmanaged
            where L : unmanaged, IByteList
        {
            if (ByteCollectionUtility.Read(ref list, startByteIndex + SizeOfElementTypeId, out newStartByteIndex, out value))
            {
                return true;
            }
            value = default;
            return false;
        }
        #endregion

        #region Remove
        /// <summary>
        /// Removes element based on provided size and preserves ordering of following elements
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveElement<T>(ref DynamicBuffer<byte> buffer, PolymorphicElementMetaData elementMetaData)
            where T : unmanaged
        {
            return ByteCollectionUtility.RemoveSize(ref buffer, elementMetaData.StartByteIndex, elementMetaData.TotalSizeWithId);
        }

        /// <summary>
        /// Removes element based on provided size and preserves ordering of following elements
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveElement<T, B>(ref DynamicBuffer<B> buffer, PolymorphicElementMetaData elementMetaData)
            where T : unmanaged
            where B : unmanaged, IBufferElementData, IByteBufferElement
        {
            return ByteCollectionUtility.RemoveSize(ref buffer, elementMetaData.StartByteIndex, elementMetaData.TotalSizeWithId);
        }

        /// <summary>
        /// Removes element based on provided size and preserves ordering of following elements
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveElement<T>(ref NativeList<byte> list, PolymorphicElementMetaData elementMetaData)
            where T : unmanaged
        {
            return ByteCollectionUtility.RemoveSize(ref list, elementMetaData.StartByteIndex, elementMetaData.TotalSizeWithId);
        }

        /// <summary>
        /// Removes element based on provided size and preserves ordering of following elements
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveElement<T>(ref UnsafeList<byte> list, PolymorphicElementMetaData elementMetaData)
            where T : unmanaged
        {
            return ByteCollectionUtility.RemoveSize(ref list, elementMetaData.StartByteIndex, elementMetaData.TotalSizeWithId);
        }

        /// <summary>
        /// Removes element based on provided size and preserves ordering of following elements
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveElement<T, L>(ref L list, PolymorphicElementMetaData elementMetaData)
            where T : unmanaged
            where L : unmanaged, IByteList
        {
            return ByteCollectionUtility.RemoveSize(ref list, elementMetaData.StartByteIndex, elementMetaData.TotalSizeWithId);
        }
        #endregion
    }
}