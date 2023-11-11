
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.CompilerServices;

namespace Trove.PolymorphicElements
{
    public struct PolymorphicElementMetaData
    {
        public ushort TypeId;
        public int StartByteIndex;
        public int TotalSizeWithId;
    }

    public static unsafe class PolymorphicElementsUtility
    {
        public const int SizeOfElementTypeId = sizeof(ushort);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElement<T>(ref NativeStream.Writer stream, ushort typeId, T value)
            where T : unmanaged
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

            buffer.ResizeUninitialized(metaData.StartByteIndex + metaData.TotalSizeWithId);
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

            list.ResizeUninitialized(metaData.StartByteIndex + metaData.TotalSizeWithId);
            byte* startPtr = list.GetUnsafePtr() + (long)metaData.StartByteIndex;
            *(ushort*)(startPtr) = typeId;
            startPtr += (long)SizeOfElementTypeId;
            *(T*)(startPtr) = value;
            return metaData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadElementValue<T>(ref DynamicBuffer<byte> buffer, int startByteIndex, out int newStartByteIndex, out T value)
            where T : unmanaged
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
        public static bool RemoveElement<T>(ref DynamicBuffer<byte> buffer, ref DynamicBuffer<PolymorphicElementMetaData> elementMetaDatas, PolymorphicElementMetaData removedElementMetaData)
            where T : unmanaged
        {
            if (RemoveElement<T>(ref buffer, removedElementMetaData))
            {
                // Handle remapping element start byte indexes
                for (int i = elementMetaDatas.Length - 1; i >= 0; i--)
                {
                    PolymorphicElementMetaData iteratedMetaData = elementMetaDatas[i];
                    if (iteratedMetaData.StartByteIndex == removedElementMetaData.StartByteIndex)
                    {
                        elementMetaDatas.RemoveAt(i);
                    }
                    else if (iteratedMetaData.StartByteIndex > removedElementMetaData.StartByteIndex)
                    {
                        iteratedMetaData.StartByteIndex -= removedElementMetaData.TotalSizeWithId;
                        elementMetaDatas[i] = iteratedMetaData;
                    }
                }

                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RemoveElement<T>(ref NativeList<byte> list, ref NativeList<PolymorphicElementMetaData> elementMetaDatas, PolymorphicElementMetaData removedElementMetaData)
            where T : unmanaged
        {
            if (RemoveElement<T>(ref list, removedElementMetaData))
            {
                // Handle remapping element start byte indexes
                for (int i = elementMetaDatas.Length - 1; i >= 0; i--)
                {
                    PolymorphicElementMetaData iteratedMetaData = elementMetaDatas[i];
                    if (iteratedMetaData.StartByteIndex == removedElementMetaData.StartByteIndex)
                    {
                        elementMetaDatas.RemoveAt(i);
                    }
                    else if (iteratedMetaData.StartByteIndex > removedElementMetaData.StartByteIndex)
                    {
                        iteratedMetaData.StartByteIndex -= removedElementMetaData.TotalSizeWithId;
                        elementMetaDatas[i] = iteratedMetaData;
                    }
                }

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
            if(elementMetaData.TotalSizeWithId <= collectionLength - elementMetaData.StartByteIndex)
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
            if(elementMetaData.TotalSizeWithId <= collectionLength - elementMetaData.StartByteIndex)
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

        public static class InternalUse
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool ReadAny<T>(ref NativeStream.Reader stream, out T t)
                where T : unmanaged
            {
                if (stream.RemainingItemCount > 1)
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
                if (startByteIndex >= 0 && sizeOfT <= buffer.Length - startByteIndex)
                {
                    byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)startByteIndex;
                    t = *(T*)startPtr;
                    startByteIndex += sizeOfT;
                    newStartByteIndex = startByteIndex;
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
                if (startByteIndex >= 0 && sizeOfT <= list.Length - startByteIndex)
                {
                    byte* startPtr = list.GetUnsafePtr() + (long)startByteIndex;
                    t = *(T*)startPtr;
                    startByteIndex += sizeOfT;
                    newStartByteIndex = startByteIndex;
                    return true;
                }

                t = default;
                newStartByteIndex = startByteIndex;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void WriteAny<T>(ref DynamicBuffer<byte> buffer, int startByteIndex, T t)
                where T : unmanaged
            {
                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)startByteIndex;
                *(T*)(startPtr) = t;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void WriteAny<T>(ref NativeList<byte> list, int startByteIndex, T t)
                where T : unmanaged
            {
                byte* startPtr = list.GetUnsafePtr() + (long)startByteIndex;
                *(T*)(startPtr) = t;
            }
        }
    }
}