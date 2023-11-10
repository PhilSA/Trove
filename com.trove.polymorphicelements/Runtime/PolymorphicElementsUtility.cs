
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Trove.PolymorphicElements
{
    public struct PolymorphicElementMetaData
    {
        public int StartIndex;
        public int ValueStartIndex => StartIndex + PolymorphicElementsUtility.SizeOfElementTypeId;
        
        public int Size;
        public int ValueSize => Size - PolymorphicElementsUtility.SizeOfElementTypeId;
    }

    public static unsafe class PolymorphicElementsUtility
    {
        public const int SizeOfElementTypeId = sizeof(ushort);
        
        public static void Write<T>(T t, ref NativeStream.Writer stream)
            where T : unmanaged
        {
            stream.Write(t);
        }
        
        public static void Write<T>(T t, ref DynamicBuffer<byte> buffer)
            where T : unmanaged
        {
            Write(t, UnsafeUtility.SizeOf<T>(), ref buffer);
        }
        
        public static void Write<T>(T t, int size, ref DynamicBuffer<byte> buffer)
            where T : unmanaged
        {
            int prevLength = buffer.Length;
            buffer.ResizeUninitialized(prevLength + size);
            byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)prevLength;
            *(T*)(startPtr) = t;
        }
    
        public static void Write<T>(T t, ref NativeList<byte> list)
            where T : unmanaged
        {
            Write(t, UnsafeUtility.SizeOf<T>(), ref list);
        }
    
        public static void Write<T>(T t, int size, ref NativeList<byte> list)
            where T : unmanaged
        {
            int prevLength = list.Length;
            list.ResizeUninitialized(prevLength + size);
            byte* startPtr = list.GetUnsafePtr() + (long)prevLength;
            *(T*)(startPtr) = t;
        }
    
        public static void Write<T, Target>(T t, ref Target target)
            where T : unmanaged
            where Target : struct
        {
            Write(t, ref target, UnsafeUtility.SizeOf<T>(), UnsafeUtility.SizeOf<Target>());
        }
    
        public static void Write<T, Target>(T t, ref Target target, int sizeT, int sizeTarget)
            where T : unmanaged
            where Target : struct
        {
            if (sizeTarget >= sizeT)
            {
                WriteUnsafe(t, ref target);
            }
        }
    
        public static void WriteUnsafe<T, Target>(T t, ref Target target)
            where T : unmanaged
            where Target : struct
        {
            void* startPtr = UnsafeUtility.AddressOf(ref target);
            *(T*)(startPtr) = t;
        }
    
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
        
        public static bool Read<T>(ref DynamicBuffer<byte> buffer, ref int index, out T t)
            where T : unmanaged
        {
            return Read(ref buffer, UnsafeUtility.SizeOf<T>(), ref index, out t);
        }
        
        public static bool Read<T>(ref DynamicBuffer<byte> buffer, int size, ref int index, out T t)
            where T : unmanaged
        {
            if(size <= buffer.Length - index)
            {
                byte* startPtr = (byte*)buffer.GetUnsafePtr() + (long)index;
                t = *(T*)startPtr;
                index += size;
                return true;
            }

            t = default;
            return false;
        }

        public static bool Read<T>(ref NativeList<byte> list, ref int index, out T t)
            where T : unmanaged
        {
            return Read(ref list, UnsafeUtility.SizeOf<T>(), ref index, out t);
        }

        public static bool Read<T>(ref NativeList<byte> list, int size, ref int index, out T t)
            where T : unmanaged
        {
            if(size <= list.Length - index)
            {
                byte* startPtr = list.GetUnsafePtr() + (long)index;
                t = *(T*)startPtr;
                index += size;
                return true;
            }

            t = default;
            return false;
        }

        public static void ReadAs<T, Target>(ref Target target, out T t)
            where T : unmanaged
            where Target : struct
        {
            void* startPtr = UnsafeUtility.AddressOf(ref target);
            t = *(T*)startPtr;
        }

        public static ref T ReadAsRef<T, Target>(ref Target target)
            where T : unmanaged
            where Target : struct
        {
            void* startPtr = UnsafeUtility.AddressOf(ref target);
            return ref *(T*)startPtr;
        }

        public static ref T GetElementValueRef<T>(ref DynamicBuffer<byte> buffer, int index)
            where T : unmanaged
        {
            byte* elementPtr = (byte*)buffer.GetUnsafePtr() + (long)(index + SizeOfElementTypeId);
            return ref *(T*)elementPtr;
        }
        
        public static ref T GetElementValueRef<T>(ref NativeList<byte> list, int index)
            where T : unmanaged
        {
            byte* elementPtr = list.GetUnsafePtr() + (long)(index + SizeOfElementTypeId);
            return ref *(T*)elementPtr;
        }

        /// <summary>
        /// Removes element based on provided size and preserves ordering of following elements
        /// </summary>
        public static bool RemoveElement(ref DynamicBuffer<byte> buffer, int index, int elementSize)
        {
            int collectionLength = buffer.Length;
            if(elementSize <= collectionLength - index)
            {
                byte* removedElementPtr = (byte*)buffer.GetUnsafePtr() + (long)index;
                byte* nextElementPtr = removedElementPtr + (long)elementSize;
                int collectionLengthAfterRemovedElement = collectionLength - (index + elementSize);
                UnsafeUtility.MemCpy(removedElementPtr, nextElementPtr, collectionLengthAfterRemovedElement);
                buffer.ResizeUninitialized(collectionLength - elementSize);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes element based on provided size and preserves ordering of following elements
        /// </summary>
        public static bool RemoveElement(ref NativeList<byte> list, int index, int elementSize)
        {
            int collectionLength = list.Length;
            if(elementSize <= collectionLength - index)
            {
                byte* removedElementPtr = list.GetUnsafePtr() + (long)index;
                byte* nextElementPtr = removedElementPtr + (long)elementSize;
                int collectionLengthAfterRemovedElement = collectionLength - (index + elementSize);
                UnsafeUtility.MemCpy(removedElementPtr, nextElementPtr, collectionLengthAfterRemovedElement);
                list.ResizeUninitialized(collectionLength - elementSize);
                return true;
            }
            return false;
        }
    }
}