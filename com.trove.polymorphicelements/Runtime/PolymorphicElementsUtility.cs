
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

    public static unsafe class PolymorphicElementsUtility
    {
        public const int SizeOfElementTypeId = sizeof(ushort);

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

        #region Add
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AppendElement<T>(ref NativeStream.Writer stream, ushort typeId, T value)
            where T : unmanaged
        {
            stream.Write(typeId);
            stream.Write(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AppendElement<T>(ref UnsafeStream.Writer stream, ushort typeId, T value)
            where T : unmanaged
        {
            stream.Write(typeId);
            stream.Write(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AppendElement<T, S>(ref S stream, ushort typeId, T value)
            where T : unmanaged
            where S : unmanaged, IStreamWriter
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
            ByteCollectionUtility.AddPair(ref buffer, typeId, value);
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
            ByteCollectionUtility.AddPair(ref buffer, typeId, value);
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
            ByteCollectionUtility.AddPair(ref list, typeId, value);
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
            ByteCollectionUtility.AddPair(ref list, typeId, value);
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
            ByteCollectionUtility.AddPair(ref list, typeId, value);
            return metaData;
        }
        #endregion

        #region Insert
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData InsertElement<T>(ref DynamicBuffer<byte> list, int atByteIndex, ushort typeId, T value)
            where T : unmanaged
        {
            if (ByteCollectionUtility.InsertPair(ref list, atByteIndex, typeId, value))
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
                {
                    TypeId = typeId,
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
                };
                return metaData;
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData InsertElement<T, B>(ref DynamicBuffer<B> list, int atByteIndex, ushort typeId, T value)
            where T : unmanaged
            where B : unmanaged, IBufferElementData, IByteBufferElement
        {
            if (ByteCollectionUtility.InsertPair(ref list, atByteIndex, typeId, value))
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
                {
                    TypeId = typeId,
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
                };
                return metaData;
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData InsertElement<T>(ref NativeList<byte> list, int atByteIndex, ushort typeId, T value)
            where T : unmanaged
        {
            if (ByteCollectionUtility.InsertPair(ref list, atByteIndex, typeId, value))
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
                {
                    TypeId = typeId,
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
                };
                return metaData;
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData InsertElement<T>(ref UnsafeList<byte> list, int atByteIndex, ushort typeId, T value)
            where T : unmanaged
        {
            if (ByteCollectionUtility.InsertPair(ref list, atByteIndex, typeId, value))
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
                {
                    TypeId = typeId,
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
                };
                return metaData;
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PolymorphicElementMetaData InsertElement<T, L>(ref L list, int atByteIndex, ushort typeId, T value)
            where T : unmanaged
            where L : unmanaged, IByteList
        {
            if (ByteCollectionUtility.InsertPair(ref list, atByteIndex, typeId, value))
            {
                int sizeOfT = UnsafeUtility.SizeOf<T>();
                PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
                {
                    TypeId = typeId,
                    StartByteIndex = atByteIndex,
                    TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
                };
                return metaData;
            }

            return default;
        }
        #endregion

        #region WriteValueNoResize
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValueNoResize<T>(ref DynamicBuffer<byte> buffer, int atByteIndex, T value)
            where T : unmanaged
        {
            atByteIndex += SizeOfElementTypeId;
            return ByteCollectionUtility.WriteNoResize(ref buffer, atByteIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValueNoResize<T, B>(ref DynamicBuffer<B> buffer, int atByteIndex, T value)
            where T : unmanaged
            where B : unmanaged, IBufferElementData, IByteBufferElement
        {
            atByteIndex += SizeOfElementTypeId;
            return ByteCollectionUtility.WriteNoResize(ref buffer, atByteIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValueNoResize<T>(ref NativeList<byte> list, int atByteIndex, T value)
            where T : unmanaged
        {
            atByteIndex += SizeOfElementTypeId;
            return ByteCollectionUtility.WriteNoResize(ref list, atByteIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValueNoResize<T>(ref UnsafeList<byte> list, int atByteIndex, T value)
            where T : unmanaged
        {
            atByteIndex += SizeOfElementTypeId;
            return ByteCollectionUtility.WriteNoResize(ref list, atByteIndex, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool WriteElementValueNoResize<T, L>(ref L list, int atByteIndex, T value)
            where T : unmanaged
            where L : unmanaged, IByteList
        {
            atByteIndex += SizeOfElementTypeId;
            return ByteCollectionUtility.WriteNoResize(ref list, atByteIndex, value);
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