using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Trove
{
    public unsafe interface IPolymorphicObject
    {
        public int GetBytesSize();
        public void WriteTo(byte* dstPtr, out int writeSize);
    }

    public static class PolymorphicObjectUtilities
    {
        #region UnsafeList
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void AddObject<T>(ref T polymorphicObject, ref UnsafeList<byte> list, out int byteIndex,
            out int writeSize, float growFactor = 1.5f)
            where T : unmanaged, IPolymorphicObject
        {
            writeSize = polymorphicObject.GetBytesSize();
            
            // Check set capacity
            int initialLength = list.Length;
            int newLength = list.Length + writeSize;
            if (newLength >= list.Capacity)
            {
                int newCapacity = (int)math.ceil(list.Capacity * growFactor);
                newCapacity = math.max(newLength, newCapacity);
                list.SetCapacity(newCapacity);
            }
            
            list.Resize(newLength, NativeArrayOptions.UninitializedMemory);

            // Write
            byteIndex = initialLength;
            byte* dstPtr = list.Ptr + (long)byteIndex;
            polymorphicObject.WriteTo(dstPtr, out writeSize);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void InsertObject<T>(ref T polymorphicObject, ref UnsafeList<byte> list, int byteIndex,
            out int writeSize, float growFactor = 1.5f)
            where T : unmanaged, IPolymorphicObject
        {
            writeSize = polymorphicObject.GetBytesSize();
            
            // Check set capacity
            int initialLength = list.Length;
            int newLength = list.Length + writeSize;
            if (newLength >= list.Capacity)
            {
                int newCapacity = (int)math.ceil(list.Capacity * growFactor);
                newCapacity = math.max(newLength, newCapacity);
                list.SetCapacity(newCapacity);
            }
            
            list.Resize(newLength, NativeArrayOptions.UninitializedMemory);
            
            // Move memory
            int moveByteIndex = byteIndex + writeSize;
            byte* srcPtr = list.Ptr + (long)byteIndex;
            byte* dstPtr = list.Ptr + (long)moveByteIndex;
            UnsafeUtility.MemMove(dstPtr, srcPtr, initialLength - byteIndex);

            // Write
            polymorphicObject.WriteTo(srcPtr, out writeSize);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void GetObject<T>(ref UnsafeList<byte> list, int byteIndex, out T polymorphicObject, 
            out int readSize)
            where T : unmanaged, IPolymorphicObject
        {
            byte* srcPtr = list.Ptr + (long)byteIndex;
            polymorphicObject = *(T*)srcPtr;
            readSize = polymorphicObject.GetBytesSize();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool RemoveObject<T>(ref UnsafeList<byte> list, int byteIndex, out T polymorphicObject, 
            out int removedSize)
            where T : unmanaged, IPolymorphicObject
        {
            GetObject(ref list, byteIndex, out polymorphicObject, out removedSize);

            if (list.Length - byteIndex >= removedSize)
            {
                int newLength = list.Length - removedSize;
                
                // Move memory
                int moveByteIndex = byteIndex - removedSize;
                byte* srcPtr = list.Ptr + (long)byteIndex;
                byte* dstPtr = list.Ptr + (long)moveByteIndex;
                UnsafeUtility.MemMove(dstPtr, srcPtr, list.Length - byteIndex);
                
                list.Resize(newLength, NativeArrayOptions.UninitializedMemory);

                return true;
            }

            return false;
        }
        #endregion

        #region NativeList
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void AddObject<T>(ref T polymorphicObject, ref NativeList<byte> list, out int byteIndex,
            out int writeSize, float growFactor = 1.5f)
            where T : unmanaged, IPolymorphicObject
        {
            writeSize = polymorphicObject.GetBytesSize();
            
            // Check set capacity
            int initialLength = list.Length;
            int newLength = list.Length + writeSize;
            if (newLength >= list.Capacity)
            {
                int newCapacity = (int)math.ceil(list.Capacity * growFactor);
                newCapacity = math.max(newLength, newCapacity);
                list.SetCapacity(newCapacity);
            }
            
            list.Resize(newLength, NativeArrayOptions.UninitializedMemory);

            // Write
            byteIndex = initialLength;
            byte* dstPtr = list.GetUnsafePtr() + (long)byteIndex;
            polymorphicObject.WriteTo(dstPtr, out writeSize);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void InsertObject<T>(ref T polymorphicObject, ref NativeList<byte> list, int byteIndex,
            out int writeSize, float growFactor = 1.5f)
            where T : unmanaged, IPolymorphicObject
        {
            writeSize = polymorphicObject.GetBytesSize();
            
            // Check set capacity
            int initialLength = list.Length;
            int newLength = list.Length + writeSize;
            if (newLength >= list.Capacity)
            {
                int newCapacity = (int)math.ceil(list.Capacity * growFactor);
                newCapacity = math.max(newLength, newCapacity);
                list.SetCapacity(newCapacity);
            }
            
            list.Resize(newLength, NativeArrayOptions.UninitializedMemory);
            
            // Move memory
            int moveByteIndex = byteIndex + writeSize;
            byte* srcPtr = list.GetUnsafePtr() + (long)byteIndex;
            byte* dstPtr = list.GetUnsafePtr() + (long)moveByteIndex;
            UnsafeUtility.MemMove(dstPtr, srcPtr, initialLength - byteIndex);

            // Write
            polymorphicObject.WriteTo(srcPtr, out writeSize);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void GetObject<T>(ref NativeList<byte> list, int byteIndex, out T polymorphicObject, 
            out int readSize)
            where T : unmanaged, IPolymorphicObject
        {
            byte* srcPtr = list.GetUnsafeReadOnlyPtr() + (long)byteIndex;
            polymorphicObject = *(T*)srcPtr;
            readSize = polymorphicObject.GetBytesSize();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool RemoveObject<T>(ref NativeList<byte> list, int byteIndex, out T polymorphicObject, 
            out int removedSize)
            where T : unmanaged, IPolymorphicObject
        {
            GetObject(ref list, byteIndex, out polymorphicObject, out removedSize);

            if (list.Length - byteIndex >= removedSize)
            {
                int newLength = list.Length - removedSize;
                
                // Move memory
                int moveByteIndex = byteIndex - removedSize;
                byte* srcPtr = list.GetUnsafePtr() + (long)byteIndex;
                byte* dstPtr = list.GetUnsafePtr() + (long)moveByteIndex;
                UnsafeUtility.MemMove(dstPtr, srcPtr, list.Length - byteIndex);
                
                list.Resize(newLength, NativeArrayOptions.UninitializedMemory);

                return true;
            }

            return false;
        }
        #endregion

        #region DynamicBuffer
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void AddObject<T>(ref T polymorphicObject, ref DynamicBuffer<byte> list, out int byteIndex,
            out int writeSize, float growFactor = 1.5f)
            where T : unmanaged, IPolymorphicObject
        {
            writeSize = polymorphicObject.GetBytesSize();
            
            // Check set capacity
            int initialLength = list.Length;
            int newLength = list.Length + writeSize;
            if (newLength >= list.Capacity)
            {
                int newCapacity = (int)math.ceil(list.Capacity * growFactor);
                newCapacity = math.max(newLength, newCapacity);
                list.EnsureCapacity(newCapacity);
            }
            
            list.Resize(newLength, NativeArrayOptions.UninitializedMemory);

            // Write
            byteIndex = initialLength;
            byte* dstPtr = (byte*)list.GetUnsafePtr() + (long)byteIndex;
            polymorphicObject.WriteTo(dstPtr, out writeSize);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void InsertObject<T>(ref T polymorphicObject, ref DynamicBuffer<byte> list, int byteIndex,
            out int writeSize, float growFactor = 1.5f)
            where T : unmanaged, IPolymorphicObject
        {
            writeSize = polymorphicObject.GetBytesSize();
            
            // Check set capacity
            int initialLength = list.Length;
            int newLength = list.Length + writeSize;
            if (newLength >= list.Capacity)
            {
                int newCapacity = (int)math.ceil(list.Capacity * growFactor);
                newCapacity = math.max(newLength, newCapacity);
                list.EnsureCapacity(newCapacity);
            }
            
            list.Resize(newLength, NativeArrayOptions.UninitializedMemory);
            
            // Move memory
            int moveByteIndex = byteIndex + writeSize;
            byte* srcPtr = (byte*)list.GetUnsafePtr() + (long)byteIndex;
            byte* dstPtr = (byte*)list.GetUnsafePtr() + (long)moveByteIndex;
            UnsafeUtility.MemMove(dstPtr, srcPtr, initialLength - byteIndex);

            // Write
            polymorphicObject.WriteTo(srcPtr, out writeSize);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void GetObject<T>(ref DynamicBuffer<byte> list, int byteIndex, out T polymorphicObject, 
            out int readSize)
            where T : unmanaged, IPolymorphicObject
        {
            byte* srcPtr = (byte*)list.GetUnsafeReadOnlyPtr() + (long)byteIndex;
            polymorphicObject = *(T*)srcPtr;
            readSize = polymorphicObject.GetBytesSize();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool RemoveObject<T>(ref DynamicBuffer<byte> list, int byteIndex, out T polymorphicObject, 
            out int removedSize)
            where T : unmanaged, IPolymorphicObject
        {
            GetObject(ref list, byteIndex, out polymorphicObject, out removedSize);

            if (list.Length - byteIndex >= removedSize)
            {
                int newLength = list.Length - removedSize;
                
                // Move memory
                int moveByteIndex = byteIndex - removedSize;
                byte* srcPtr = (byte*)list.GetUnsafePtr() + (long)byteIndex;
                byte* dstPtr = (byte*)list.GetUnsafePtr() + (long)moveByteIndex;
                UnsafeUtility.MemMove(dstPtr, srcPtr, list.Length - byteIndex);
                
                list.Resize(newLength, NativeArrayOptions.UninitializedMemory);

                return true;
            }

            return false;
        }
        #endregion

        #region NativeStream
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void AddObject<T>(ref T polymorphicObject, ref NativeStream.Writer stream, float growFactor = 1.5f)
            where T : unmanaged, IPolymorphicObject
        {
            int writeSize = polymorphicObject.GetBytesSize();
            
            // Write
            byte* dstPtr = stream.Allocate(writeSize);
            polymorphicObject.WriteTo(dstPtr, out writeSize);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool GetNextObject<T>(ref NativeStream.Reader stream, out T polymorphicObject)
            where T : unmanaged, IPolymorphicObject
        {
            polymorphicObject = default;
            int readSize = polymorphicObject.GetBytesSize();
            
            // Check read
            if (stream.RemainingItemCount > 0)
            {
                byte* srcPtr = stream.ReadUnsafePtr(readSize);
                polymorphicObject = *(T*)srcPtr;
                return true;
            }
            
            return false;
        }
        #endregion
    }
}