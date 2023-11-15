
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.CompilerServices;

namespace Trove.PolymorphicElements
{
    public interface IByteBufferElement
    {
    }

    public interface IByteStreamWriter
    {
        void Write<T>(T t) where T : unmanaged;
    }

    public interface IByteStreamReader
    {
        int RemainingItemCount { get; }
        T Read<T>() where T : unmanaged;
    }

    public unsafe interface IByteList
    {
        int Length { get; }
        byte* Ptr { get; }
        void Resize(int newLength);
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
        public static void AppendElement<T>(ref NativeStream.Writer stream, ushort typeId, T value)
            where T : unmanaged
        {
            stream.Write(typeId);
            stream.Write(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AppendElement<T, S>(ref S stream, ushort typeId, T value)
            where T : unmanaged
            where S : unmanaged, IByteStreamWriter
        {
            stream.Write(typeId);
            stream.Write(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData AddElement<T>(ref DynamicBuffer<byte> buffer, ushort typeId, T value)
            where T : unmanaged
        {
            int sizeOfT = UnsafeUtility.SizeOf<T>();
            PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
            {
                TypeId = typeId,
                StartByteIndex = buffer.Length,
                TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
            };

            buffer.ResizeUninitialized(buffer.Length + metaData.TotalSizeWithId);
            byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)metaData.StartByteIndex;
            *(ushort*)(startPtr) = typeId;
            startPtr += (long)SizeOfElementTypeId;
            *(T*)(startPtr) = value;
            return metaData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData AddElement<T, B>(ref DynamicBuffer<B> buffer, ushort typeId, T value)
            where T : unmanaged
            where B : unmanaged, IBufferElementData, IByteBufferElement
        {
            int sizeOfT = UnsafeUtility.SizeOf<T>();
            PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
            {
                TypeId = typeId,
                StartByteIndex = buffer.Length,
                TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
            };

            buffer.ResizeUninitialized(buffer.Length + metaData.TotalSizeWithId);
            byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)metaData.StartByteIndex;
            *(ushort*)(startPtr) = typeId;
            startPtr += (long)SizeOfElementTypeId;
            *(T*)(startPtr) = value;
            return metaData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData AddElement<T>(ref NativeList<byte> list, ushort typeId, T value)
            where T : unmanaged
        {
            int sizeOfT = UnsafeUtility.SizeOf<T>();
            PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
            {
                TypeId = typeId,
                StartByteIndex = list.Length,
                TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
            };

            list.ResizeUninitialized(list.Length + metaData.TotalSizeWithId);
            byte* startPtr = list.GetUnsafePtr() + (long)metaData.StartByteIndex;
            *(ushort*)(startPtr) = typeId;
            startPtr += (long)SizeOfElementTypeId;
            *(T*)(startPtr) = value;
            return metaData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData AddElement<T>(ref UnsafeList<byte> list, ushort typeId, T value)
            where T : unmanaged
        {
            int sizeOfT = UnsafeUtility.SizeOf<T>();
            PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
            {
                TypeId = typeId,
                StartByteIndex = list.Length,
                TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
            };

            list.Resize(list.Length + metaData.TotalSizeWithId);
            byte* startPtr = list.Ptr + (long)metaData.StartByteIndex;
            *(ushort*)(startPtr) = typeId;
            startPtr += (long)SizeOfElementTypeId;
            *(T*)(startPtr) = value;
            return metaData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData AddElement<T, L>(ref L list, ushort typeId, T value)
            where T : unmanaged
            where L : unmanaged, IByteList
        {
            int sizeOfT = UnsafeUtility.SizeOf<T>();
            PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
            {
                TypeId = typeId,
                StartByteIndex = list.Length,
                TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
            };

            list.Resize(list.Length + metaData.TotalSizeWithId);
            byte* startPtr = list.Ptr + (long)metaData.StartByteIndex;
            *(ushort*)(startPtr) = typeId;
            startPtr += (long)SizeOfElementTypeId;
            *(T*)(startPtr) = value;
            return metaData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData InsertElement<T>(ref DynamicBuffer<byte> buffer, int atByteIndex, ushort typeId, T value)
            where T : unmanaged
        {
            if (atByteIndex >= 0 && atByteIndex < buffer.Length)
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
                {
                    TypeId = typeId,
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
                };

                buffer.ResizeUninitialized(buffer.Length + metaData.TotalSizeWithId);
                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)atByteIndex;
                *(ushort*)(startPtr) = typeId;
                startPtr += (long)SizeOfElementTypeId;
                *(T*)(startPtr) = value;
                return metaData;
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData InsertElement<T, B>(ref DynamicBuffer<B> buffer, int atByteIndex, ushort typeId, T value)
            where T : unmanaged
            where B : unmanaged, IBufferElementData, IByteBufferElement
        {
            if (atByteIndex >= 0 && atByteIndex < buffer.Length)
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
                {
                    TypeId = typeId,
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
                };

                buffer.ResizeUninitialized(buffer.Length + metaData.TotalSizeWithId);
                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)atByteIndex;
                *(ushort*)(startPtr) = typeId;
                startPtr += (long)SizeOfElementTypeId;
                *(T*)(startPtr) = value;
                return metaData;
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData InsertElement<T>(ref NativeList<byte> list, int atByteIndex, ushort typeId, T value)
            where T : unmanaged
        {
            if (atByteIndex >= 0 && atByteIndex < list.Length)
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
                {
                    TypeId = typeId,
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
                };

                list.ResizeUninitialized(list.Length + metaData.TotalSizeWithId);
                byte* startPtr = list.GetUnsafePtr() + (long)atByteIndex;
                *(ushort*)(startPtr) = typeId;
                startPtr += (long)SizeOfElementTypeId;
                *(T*)(startPtr) = value;
                return metaData;
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData InsertElement<T>(ref UnsafeList<byte> list, int atByteIndex, ushort typeId, T value)
            where T : unmanaged
        {
            if (atByteIndex >= 0 && atByteIndex < list.Length)
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
                {
                    TypeId = typeId,
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
                };

                list.Resize(list.Length + metaData.TotalSizeWithId);
                byte* startPtr = list.Ptr + (long)atByteIndex;
                *(ushort*)(startPtr) = typeId;
                startPtr += (long)SizeOfElementTypeId;
                *(T*)(startPtr) = value;
                return metaData;
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData InsertElement<T, L>(ref L list, int atByteIndex, ushort typeId, T value)
            where T : unmanaged
            where L : unmanaged, IByteList
        {
            if (atByteIndex >= 0 && atByteIndex < list.Length)
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
                {
                    TypeId = typeId,
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
                };

                list.Resize(list.Length + metaData.TotalSizeWithId);
                byte* startPtr = list.Ptr + (long)atByteIndex;
                *(ushort*)(startPtr) = typeId;
                startPtr += (long)SizeOfElementTypeId;
                *(T*)(startPtr) = value;
                return metaData;
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData TryOverwriteBytesAtNoResize<T>(ref DynamicBuffer<byte> buffer, int atByteIndex, ushort typeId, T value)
            where T : unmanaged
        {
            int sizeOfT = UnsafeUtility.SizeOf<T>();
            if (atByteIndex >= 0 && atByteIndex + SizeOfElementTypeId + sizeOfT <= buffer.Length)
            {
                PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
                {
                    TypeId = typeId,
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
                };

                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)atByteIndex;
                *(ushort*)(startPtr) = typeId;
                startPtr += (long)SizeOfElementTypeId;
                *(T*)(startPtr) = value;
                return metaData;
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData TryOverwriteBytesAtNoResize<T, B>(ref DynamicBuffer<B> buffer, int atByteIndex, ushort typeId, T value)
            where T : unmanaged
            where B : unmanaged, IBufferElementData, IByteBufferElement
        {
            int sizeOfT = UnsafeUtility.SizeOf<T>();
            if (atByteIndex >= 0 && atByteIndex + SizeOfElementTypeId + sizeOfT <= buffer.Length)
            {
                PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
                {
                    TypeId = typeId,
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
                };

                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)atByteIndex;
                *(ushort*)(startPtr) = typeId;
                startPtr += (long)SizeOfElementTypeId;
                *(T*)(startPtr) = value;
                return metaData;
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData TryOverwriteBytesAtNoResize<T>(ref NativeList<byte> list, int atByteIndex, ushort typeId, T value)
            where T : unmanaged
        {
            int sizeOfT = UnsafeUtility.SizeOf<T>();
            if (atByteIndex >= 0 && atByteIndex + SizeOfElementTypeId + sizeOfT <= list.Length)
            {
                PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
                {
                    TypeId = typeId,
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
                };

                byte* startPtr = list.GetUnsafePtr() + (long)atByteIndex;
                *(ushort*)(startPtr) = typeId;
                startPtr += (long)SizeOfElementTypeId;
                *(T*)(startPtr) = value;
                return metaData;
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData TryOverwriteBytesAtNoResize<T>(ref UnsafeList<byte> list, int atByteIndex, ushort typeId, T value)
            where T : unmanaged
        {
            int sizeOfT = UnsafeUtility.SizeOf<T>();
            if (atByteIndex >= 0 && atByteIndex + SizeOfElementTypeId + sizeOfT <= list.Length)
            {
                PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
                {
                    TypeId = typeId,
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
                };

                byte* startPtr = list.Ptr + (long)atByteIndex;
                *(ushort*)(startPtr) = typeId;
                startPtr += (long)SizeOfElementTypeId;
                *(T*)(startPtr) = value;
                return metaData;
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData TryOverwriteBytesAtNoResize<T, L>(ref L list, int atByteIndex, ushort typeId, T value)
            where T : unmanaged
            where L : unmanaged, IByteList
        {
            int sizeOfT = UnsafeUtility.SizeOf<T>();
            if (atByteIndex >= 0 && atByteIndex + SizeOfElementTypeId + sizeOfT <= list.Length)
            {
                PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
                {
                    TypeId = typeId,
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
                };

                byte* startPtr = list.Ptr + (long)atByteIndex;
                *(ushort*)(startPtr) = typeId;
                startPtr += (long)SizeOfElementTypeId;
                *(T*)(startPtr) = value;
                return metaData;
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadElementValue<T>(ref DynamicBuffer<byte> buffer, int startByteIndex, out int newStartByteIndex, out T value)
            where T : unmanaged
        {
            return InternalUse.ReadAny(ref buffer, startByteIndex + SizeOfElementTypeId, out newStartByteIndex, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadElementValue<T, B>(ref DynamicBuffer<B> buffer, int startByteIndex, out int newStartByteIndex, out T value)
            where T : unmanaged
            where B : unmanaged, IBufferElementData, IByteBufferElement
        {
            return InternalUse.ReadAny(ref buffer, startByteIndex + SizeOfElementTypeId, out newStartByteIndex, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadElementValue<T>(ref NativeList<byte> list, int startByteIndex, out int newStartByteIndex, out T value)
            where T : unmanaged
        {
            return InternalUse.ReadAny(ref list, startByteIndex + SizeOfElementTypeId, out newStartByteIndex, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadElementValue<T>(ref UnsafeList<byte> list, int startByteIndex, out int newStartByteIndex, out T value)
            where T : unmanaged
        {
            return InternalUse.ReadAny(ref list, startByteIndex + SizeOfElementTypeId, out newStartByteIndex, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadElementValue<T, L>(ref L list, int startByteIndex, out int newStartByteIndex, out T value)
            where T : unmanaged
            where L : unmanaged, IByteList
        {
            return InternalUse.ReadAny(ref list, startByteIndex + SizeOfElementTypeId, out newStartByteIndex, out value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValue<T>(ref DynamicBuffer<byte> buffer, int startByteIndex, T value)
            where T : unmanaged
        {
            if (startByteIndex + UnsafeUtility.SizeOf<T>() <= buffer.Length)
            {
                InternalUse.WriteAny(ref buffer, startByteIndex + SizeOfElementTypeId, value);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValue<T, B>(ref DynamicBuffer<B> buffer, int startByteIndex, T value)
            where T : unmanaged
            where B : unmanaged, IBufferElementData, IByteBufferElement
        {
            if (startByteIndex + UnsafeUtility.SizeOf<T>() <= buffer.Length)
            {
                InternalUse.WriteAny(ref buffer, startByteIndex + SizeOfElementTypeId, value);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValue<T>(ref NativeList<byte> list, int startByteIndex, T value)
            where T : unmanaged
        {
            if (startByteIndex + UnsafeUtility.SizeOf<T>() <= list.Length)
            {
                InternalUse.WriteAny(ref list, startByteIndex + SizeOfElementTypeId, value);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValue<T>(ref UnsafeList<byte> list, int startByteIndex, T value)
            where T : unmanaged
        {
            if (startByteIndex + UnsafeUtility.SizeOf<T>() <= list.Length)
            {
                InternalUse.WriteAny(ref list, startByteIndex + SizeOfElementTypeId, value);
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValue<T, L>(ref L list, int startByteIndex, T value)
            where T : unmanaged
            where L : unmanaged, IByteList
        {
            if (startByteIndex + UnsafeUtility.SizeOf<T>() <= list.Length)
            {
                InternalUse.WriteAny(ref list, startByteIndex + SizeOfElementTypeId, value);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes element based on provided size and preserves ordering of following elements
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveElement<T>(ref DynamicBuffer<byte> buffer, PolymorphicElementMetaData elementMetaData)
            where T : unmanaged
        {
            int collectionLength = buffer.Length;
            if (elementMetaData.TotalSizeWithId <= collectionLength - elementMetaData.StartByteIndex)
            {
                byte* removedElementPtr = (byte*)buffer.GetUnsafePtr() + (long)elementMetaData.StartByteIndex;
                byte* nextElementPtr = removedElementPtr + (long)elementMetaData.TotalSizeWithId;
                int collectionLengthAfterRemovedElement = collectionLength - (elementMetaData.StartByteIndex + elementMetaData.TotalSizeWithId);
                UnsafeUtility.MemCpy(removedElementPtr, nextElementPtr, collectionLengthAfterRemovedElement);
                buffer.ResizeUninitialized(collectionLength - elementMetaData.TotalSizeWithId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes element based on provided size and preserves ordering of following elements
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveElement<T, B>(ref DynamicBuffer<B> buffer, PolymorphicElementMetaData elementMetaData)
            where T : unmanaged
            where B : unmanaged, IBufferElementData, IByteBufferElement
        {
            int collectionLength = buffer.Length;
            if (elementMetaData.TotalSizeWithId <= collectionLength - elementMetaData.StartByteIndex)
            {
                byte* removedElementPtr = (byte*)buffer.GetUnsafePtr() + (long)elementMetaData.StartByteIndex;
                byte* nextElementPtr = removedElementPtr + (long)elementMetaData.TotalSizeWithId;
                int collectionLengthAfterRemovedElement = collectionLength - (elementMetaData.StartByteIndex + elementMetaData.TotalSizeWithId);
                UnsafeUtility.MemCpy(removedElementPtr, nextElementPtr, collectionLengthAfterRemovedElement);
                buffer.ResizeUninitialized(collectionLength - elementMetaData.TotalSizeWithId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes element based on provided size and preserves ordering of following elements
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveElement<T>(ref NativeList<byte> list, PolymorphicElementMetaData elementMetaData)
            where T : unmanaged
        {
            int collectionLength = list.Length;
            if (elementMetaData.TotalSizeWithId <= collectionLength - elementMetaData.StartByteIndex)
            {
                byte* removedElementPtr = list.GetUnsafePtr() + (long)elementMetaData.StartByteIndex;
                byte* nextElementPtr = removedElementPtr + (long)elementMetaData.TotalSizeWithId;
                int collectionLengthAfterRemovedElement = collectionLength - (elementMetaData.StartByteIndex + elementMetaData.TotalSizeWithId);
                UnsafeUtility.MemCpy(removedElementPtr, nextElementPtr, collectionLengthAfterRemovedElement);
                list.ResizeUninitialized(collectionLength - elementMetaData.TotalSizeWithId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes element based on provided size and preserves ordering of following elements
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveElement<T>(ref UnsafeList<byte> list, PolymorphicElementMetaData elementMetaData)
            where T : unmanaged
        {
            int collectionLength = list.Length;
            if (elementMetaData.TotalSizeWithId <= collectionLength - elementMetaData.StartByteIndex)
            {
                byte* removedElementPtr = list.Ptr + (long)elementMetaData.StartByteIndex;
                byte* nextElementPtr = removedElementPtr + (long)elementMetaData.TotalSizeWithId;
                int collectionLengthAfterRemovedElement = collectionLength - (elementMetaData.StartByteIndex + elementMetaData.TotalSizeWithId);
                UnsafeUtility.MemCpy(removedElementPtr, nextElementPtr, collectionLengthAfterRemovedElement);
                list.Resize(collectionLength - elementMetaData.TotalSizeWithId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Removes element based on provided size and preserves ordering of following elements
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveElement<T, L>(ref L list, PolymorphicElementMetaData elementMetaData)
            where T : unmanaged
            where L : unmanaged, IByteList
        {
            int collectionLength = list.Length;
            if (elementMetaData.TotalSizeWithId <= collectionLength - elementMetaData.StartByteIndex)
            {
                byte* removedElementPtr = list.Ptr + (long)elementMetaData.StartByteIndex;
                byte* nextElementPtr = removedElementPtr + (long)elementMetaData.TotalSizeWithId;
                int collectionLengthAfterRemovedElement = collectionLength - (elementMetaData.StartByteIndex + elementMetaData.TotalSizeWithId);
                UnsafeUtility.MemCpy(removedElementPtr, nextElementPtr, collectionLengthAfterRemovedElement);
                list.Resize(collectionLength - elementMetaData.TotalSizeWithId);
                return true;
            }

            return false;
        }

        public static class InternalUse
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int GetLength<L, T>(ref L list) where L : unmanaged, INativeList<T> where T : unmanaged
            {
                return buffer.Length;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int GetLength<T>(ref DynamicBuffer<T> buffer)
            {

            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool ReadAny<T>(ref NativeStream.Reader stream, out T t)
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
            public static bool ReadAny<T, S>(ref S stream, out T t)
                where T : unmanaged
                where S : unmanaged, IByteStreamReader
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
            public static bool ReadAny<T>(ref DynamicBuffer<byte> buffer, int startByteIndex, out int newStartByteIndex, out T t)
                where T : unmanaged
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                if (startByteIndex >= 0 && startByteIndex + sizeOfT <= buffer.Length)
                {
                    byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)startByteIndex;
                    t = *(T*)startPtr;
                    newStartByteIndex = startByteIndex + sizeOfT;
                    return true;
                }

                t = default;
                newStartByteIndex = startByteIndex;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool ReadAny<T, B>(ref DynamicBuffer<B> buffer, int startByteIndex, out int newStartByteIndex, out T t)
                where T : unmanaged
                where B : unmanaged, IBufferElementData, IByteBufferElement
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                if (startByteIndex >= 0 && startByteIndex + sizeOfT <= buffer.Length)
                {
                    byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)startByteIndex;
                    t = *(T*)startPtr;
                    newStartByteIndex = startByteIndex + sizeOfT;
                    return true;
                }

                t = default;
                newStartByteIndex = startByteIndex;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool ReadAny<T>(ref NativeList<byte> list, int startByteIndex, out int newStartByteIndex, out T t)
                where T : unmanaged
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                if (startByteIndex >= 0 && startByteIndex + sizeOfT <= list.Length)
                {
                    byte* startPtr = list.GetUnsafePtr() + (long)startByteIndex;
                    t = *(T*)startPtr;
                    newStartByteIndex = startByteIndex + sizeOfT;
                    return true;
                }

                t = default;
                newStartByteIndex = startByteIndex;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool ReadAny<T>(ref UnsafeList<byte> list, int startByteIndex, out int newStartByteIndex, out T t)
                where T : unmanaged
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                if (startByteIndex >= 0 && startByteIndex + sizeOfT <= list.Length)
                {
                    byte* startPtr = list.Ptr + (long)startByteIndex;
                    t = *(T*)startPtr;
                    newStartByteIndex = startByteIndex + sizeOfT;
                    return true;
                }

                t = default;
                newStartByteIndex = startByteIndex;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool ReadAny<T, L>(ref L list, int startByteIndex, out int newStartByteIndex, out T t)
                where T : unmanaged
            where L : unmanaged, IByteList
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                if (startByteIndex >= 0 && startByteIndex + sizeOfT <= list.Length)
                {
                    byte* startPtr = list.Ptr + (long)startByteIndex;
                    t = *(T*)startPtr;
                    newStartByteIndex = startByteIndex + sizeOfT;
                    return true;
                }

                t = default;
                newStartByteIndex = startByteIndex;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ref T ReadAnyAsRef<T>(ref DynamicBuffer<byte> buffer, int startByteIndex, out int newStartByteIndex, out bool success)
                where T : unmanaged
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                if (startByteIndex >= 0 && startByteIndex + sizeOfT <= buffer.Length)
                {
                    byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)startByteIndex;
                    newStartByteIndex = startByteIndex + sizeOfT;
                    success = true;
                    return ref *(T*)startPtr;
                }

                success = false;
                newStartByteIndex = startByteIndex;
                return ref *(T*)buffer.GetUnsafePtr();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ref T ReadAnyAsRef<T, B>(ref DynamicBuffer<B> buffer, int startByteIndex, out int newStartByteIndex, out bool success)
                where T : unmanaged
                where B : unmanaged, IBufferElementData, IByteBufferElement
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                if (startByteIndex >= 0 && startByteIndex + sizeOfT <= buffer.Length)
                {
                    byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)startByteIndex;
                    newStartByteIndex = startByteIndex + sizeOfT;
                    success = true;
                    return ref *(T*)startPtr;
                }

                success = false;
                newStartByteIndex = startByteIndex;
                return ref *(T*)buffer.GetUnsafePtr();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ref T ReadAnyAsRef<T>(ref NativeList<byte> list, int startByteIndex, out int newStartByteIndex, out bool success)
                where T : unmanaged
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                if (startByteIndex >= 0 && startByteIndex + sizeOfT <= list.Length)
                {
                    byte* startPtr = list.GetUnsafePtr() + (long)startByteIndex;
                    newStartByteIndex = startByteIndex + sizeOfT;
                    success = true;
                    return ref *(T*)startPtr;
                }

                success = false;
                newStartByteIndex = startByteIndex;
                return ref *(T*)list.GetUnsafePtr();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ref T ReadAnyAsRef<T>(ref UnsafeList<byte> list, int startByteIndex, out int newStartByteIndex, out bool success)
                where T : unmanaged
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                if (startByteIndex >= 0 && startByteIndex + sizeOfT <= list.Length)
                {
                    byte* startPtr = list.Ptr + (long)startByteIndex;
                    newStartByteIndex = startByteIndex + sizeOfT;
                    success = true;
                    return ref *(T*)startPtr;
                }

                success = false;
                newStartByteIndex = startByteIndex;
                return ref *(T*)list.Ptr;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static ref T ReadAnyAsRef<T, L>(ref L list, int startByteIndex, out int newStartByteIndex, out bool success)
                where T : unmanaged
                where L : unmanaged, IByteList
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                if (startByteIndex >= 0 && startByteIndex + sizeOfT <= list.Length)
                {
                    byte* startPtr = list.Ptr + (long)startByteIndex;
                    newStartByteIndex = startByteIndex + sizeOfT;
                    success = true;
                    return ref *(T*)startPtr;
                }

                success = false;
                newStartByteIndex = startByteIndex;
                return ref *(T*)list.Ptr;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool WriteAny<T>(ref DynamicBuffer<byte> buffer, int startByteIndex, T t)
                where T : unmanaged
            {
                if (startByteIndex >= 0 && startByteIndex < buffer.Length)
                {
                    byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)startByteIndex;
                    *(T*)(startPtr) = t;
                    return true;
                }
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool WriteAny<T, B>(ref DynamicBuffer<B> buffer, int startByteIndex, T t)
                where T : unmanaged
                where B : unmanaged, IBufferElementData, IByteBufferElement
            {
                if (startByteIndex >= 0 && startByteIndex < buffer.Length)
                {
                    byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)startByteIndex;
                    *(T*)(startPtr) = t;
                    return true;
                }
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool WriteAny<T>(ref NativeList<byte> list, int startByteIndex, T t)
                where T : unmanaged
            {
                if (startByteIndex >= 0 && startByteIndex < list.Length)
                {
                    byte* startPtr = list.GetUnsafePtr() + (long)startByteIndex;
                    *(T*)(startPtr) = t;
                    return true;
                }
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool WriteAny<T>(ref UnsafeList<byte> list, int startByteIndex, T t)
                where T : unmanaged
            {
                if (startByteIndex >= 0 && startByteIndex < list.Length)
                {
                    byte* startPtr = list.Ptr + (long)startByteIndex;
                    *(T*)(startPtr) = t;
                    return true;
                }
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool WriteAny<T, L>(ref L list, int startByteIndex, T t)
                where T : unmanaged
            where L : unmanaged, IByteList
            {
                if (startByteIndex >= 0 && startByteIndex < list.Length)
                {
                    byte* startPtr = list.Ptr + (long)startByteIndex;
                    *(T*)(startPtr) = t;
                    return true;
                }
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool InsertAny<T>(ref DynamicBuffer<byte> buffer, int atByteIndex, T value)
                where T : unmanaged
            {
                if (atByteIndex >= 0 && atByteIndex < buffer.Length)
                {
                    int sizeOfT = UnsafeUtility.SizeOf<T>();
                    buffer.ResizeUninitialized(buffer.Length + sizeOfT);
                    byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)atByteIndex;
                    int sizeOfMovedMemory = 
                    byte* movedPtr = startPtr + (long)sizeOfT;
                    UnsafeUtility.MemCpy(movedPtr, startPtr, )
                    *(T*)(startPtr) = value;
                    return true;
                }
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool InsertAny<T, B>(ref DynamicBuffer<B> buffer, int atByteIndex, T value)
                where T : unmanaged
                where B : unmanaged, IBufferElementData, IByteBufferElement
            {
                if (atByteIndex >= 0 && atByteIndex < buffer.Length)
                {
                    int sizeOfT = UnsafeUtility.SizeOf<T>();
                    buffer.ResizeUninitialized(buffer.Length + sizeOfT);
                    byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)atByteIndex;
                    *(T*)(startPtr) = value;
                    return true;
                }
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool InsertAny<T>(ref NativeList<byte> list, int atByteIndex, T value)
                where T : unmanaged
            {
                if (atByteIndex >= 0 && atByteIndex < list.Length)
                {
                    int sizeOfT = UnsafeUtility.SizeOf<T>();
                    list.ResizeUninitialized(list.Length + sizeOfT);
                    byte* startPtr = list.GetUnsafePtr() + (long)atByteIndex;
                    *(T*)(startPtr) = value;
                    return true;
                }
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool InsertAny<T>(ref UnsafeList<byte> list, int atByteIndex, T value)
                where T : unmanaged
            {
                if (atByteIndex >= 0 && atByteIndex < list.Length)
                {
                    int sizeOfT = UnsafeUtility.SizeOf<T>();
                    list.Resize(list.Length + sizeOfT);
                    byte* startPtr = list.Ptr + (long)atByteIndex;
                    *(T*)(startPtr) = value;
                    return true;
                }
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool InsertAny<T, L>(ref L list, int atByteIndex, T value)
                where T : unmanaged
                where L : unmanaged, IByteList
            {
                if (atByteIndex >= 0 && atByteIndex < list.Length)
                {
                    int sizeOfT = UnsafeUtility.SizeOf<T>();
                    list.Resize(list.Length + sizeOfT);
                    byte* startPtr = list.Ptr + (long)atByteIndex;
                    *(T*)(startPtr) = value;
                    return true;
                }
                return false;
            }
        }
    }
}