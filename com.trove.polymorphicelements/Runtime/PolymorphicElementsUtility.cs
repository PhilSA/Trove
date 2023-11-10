
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.CompilerServices;

namespace Trove.PolymorphicElements
{
    public unsafe struct ReferenceOf<T> where T : unmanaged
    {
        private unsafe readonly byte* _Data;
        public unsafe ref T Value
        {
            get
            {
                return ref UnsafeUtility.AsRef<T>(_Data);
            }
        }

        public ReferenceOf(byte* ptr)
        {
            _Data = ptr;
        }
    }

    public struct PolymorphicElementMetaData
    {
        public int InstanceId;
        public int StartByteIndex;
        public int TotalSizeWithId;
    }

    public static unsafe class PolymorphicElementsUtility
    {
        public const int SizeOfElementTypeId = sizeof(ushort);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElement<T>(ref NativeStream.Writer stream, ushort typeId, T t)
            where T : unmanaged
        {
            stream.Write(typeId);
            stream.Write(t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElement<T>(ref DynamicBuffer<byte> buffer, ushort typeId, T t, int instanceId, out PolymorphicElementMetaData metaData)
            where T : unmanaged
        {
            int sizeOfT = UnsafeUtility.SizeOf<T>();
            metaData = new PolymorphicElementMetaData
            {
                InstanceId = instanceId,
                StartByteIndex = buffer.Length,
                TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
            };

            buffer.ResizeUninitialized(metaData.StartByteIndex + metaData.TotalSizeWithId);
            byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)metaData.StartByteIndex;
            *(ushort*)(startPtr) = typeId;
            startPtr += (long)SizeOfElementTypeId;
            *(T*)(startPtr) = t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddElement<T>(ref NativeList<byte> list, ushort typeId, T t, int instanceId, out PolymorphicElementMetaData metaData)
            where T : unmanaged
        {
            int sizeOfT = UnsafeUtility.SizeOf<T>();
            metaData = new PolymorphicElementMetaData
            {
                InstanceId = instanceId,
                StartByteIndex = list.Length,
                TotalSizeWithId = SizeOfElementTypeId + sizeOfT,
            };

            list.ResizeUninitialized(metaData.StartByteIndex + metaData.TotalSizeWithId);
            byte* startPtr = list.GetUnsafePtr() + (long)metaData.StartByteIndex;
            *(ushort*)(startPtr) = typeId;
            startPtr += (long)SizeOfElementTypeId;
            *(T*)(startPtr) = t;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Read<T>(ref NativeStream.Reader stream, out T t)
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
        public static bool Read<T>(ref DynamicBuffer<byte> buffer, ref int startByteIndex, out T t)
            where T : unmanaged
        {
            int sizeOfT = UnsafeUtility.SizeOf<T>();
            if (sizeOfT <= buffer.Length - startByteIndex)
            {
                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)startByteIndex;
                t = *(T*)startPtr;
                startByteIndex += sizeOfT;
                return true;
            }

            t = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Read<T>(ref NativeList<byte> list, ref int startByteIndex, out T t)
            where T : unmanaged
        {
            int sizeOfT = UnsafeUtility.SizeOf<T>();
            if (sizeOfT <= list.Length - startByteIndex)
            {
                byte* startPtr = list.GetUnsafePtr() + (long)startByteIndex;
                t = *(T*)startPtr;
                startByteIndex += sizeOfT;
                return true;
            }

            t = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadAsRef<T>(ref DynamicBuffer<byte> buffer, ref int startByteIndex, out ReferenceOf<T> t)
            where T : unmanaged
        {
            int sizeOfT = UnsafeUtility.SizeOf<T>();
            if (sizeOfT <= buffer.Length - startByteIndex)
            {
                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)startByteIndex;
                t = new ReferenceOf<T>(startPtr);
                startByteIndex += sizeOfT;
                return true;
            }

            t = default;
            return false;
        }
         
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ReadAsRef<T>(ref NativeList<byte> list, ref int startByteIndex, out ReferenceOf<T> t)
            where T : unmanaged
        {
            int sizeOfT = UnsafeUtility.SizeOf<T>();
            if (sizeOfT <= list.Length - startByteIndex)
            {
                byte* startPtr = list.GetUnsafePtr() + (long)startByteIndex;
                t = new ReferenceOf<T>(startPtr);
                startByteIndex += sizeOfT;
                return true;
            }

            t = default;
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
    }
}