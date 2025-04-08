using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Trove
{
    public unsafe interface IPolymorphicObject
    {
        public int GetTypeId();
        public int GetDataBytesSize();
        public int GetDataBytesSizeFor(int typeId);
        public void WriteDataTo(byte* dstPtr, out int writeSize);
        public void SetDataFrom(int typeId, byte* srcPtr, out int readSize);
    }
    
    public struct PolymorphicObjectUnsafeListIterator<T> where T : unmanaged, IPolymorphicObject
    {
        private UnsafeList<byte> _list;
        private int _readIndex;
        
        public PolymorphicObjectUnsafeListIterator(UnsafeList<byte> list)
        {
            _list = list;
            _readIndex = 0;
        }

        public bool GetNext(out T result, out int startIndex, out int size)
        {
            startIndex = _readIndex;
            if (_readIndex < _list.Length)
            {
                PolymorphicObjectUtilities.ReadObject(ref _list, _readIndex, out result, out size);
                _readIndex += size;
                return true;
            }

            result = default;
            size = 0;
            return false;
        }
    }

    public struct PolymorphicObjectNativeListIterator<T> where T : unmanaged, IPolymorphicObject
    {
        private NativeList<byte> _list;
        private int _readIndex;
        
        public PolymorphicObjectNativeListIterator(NativeList<byte> list)
        {
            _list = list;
            _readIndex = 0;
        }

        public bool GetNext(out T result, out int startIndex, out int size)
        {
            startIndex = _readIndex;
            if (_readIndex < _list.Length)
            {
                PolymorphicObjectUtilities.ReadObject(ref _list, _readIndex, out result, out size);
                _readIndex += size;
                return true;
            }

            result = default;
            size = 0;
            return false;
        }
    }

    public struct PolymorphicObjectDynamicBufferIterator<T> where T : unmanaged, IPolymorphicObject
    {
        private DynamicBuffer<byte> _list;
        private int _readIndex;
        
        public PolymorphicObjectDynamicBufferIterator(DynamicBuffer<byte> list)
        {
            _list = list;
            _readIndex = 0;
        }

        public bool GetNext(out T result, out int startIndex, out int size)
        {
            startIndex = _readIndex;
            if (_readIndex < _list.Length)
            {
                PolymorphicObjectUtilities.ReadObject(ref _list, _readIndex, out result, out size);
                _readIndex += size;
                return true;
            }

            result = default;
            size = 0;
            return false;
        }
    }

    public struct PolymorphicObjectNativeStreamIterator<T> where T : unmanaged, IPolymorphicObject
    {
        private NativeStream.Reader _stream;
        
        public PolymorphicObjectNativeStreamIterator(NativeStream stream)
        {
            _stream = stream.AsReader();
        }

        public bool GetNext(out T result, out int size)
        {
            return PolymorphicObjectUtilities.GetNextObject(ref _stream, out result, out size);
        }
    }

    public static class PolymorphicObjectUtilities
    {
        public const int SizeOf_TypeId = 4;
        
        #region UnsafeList
        public static PolymorphicObjectUnsafeListIterator<T> GetIterator<T>(UnsafeList<byte> list)
            where T : unmanaged, IPolymorphicObject
        {
            return new PolymorphicObjectUnsafeListIterator<T>(list);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteObject<T>(T polymorphicObject, ref UnsafeList<byte> list, int byteIndex,
            out int writeSize)
            where T : unmanaged, IPolymorphicObject
        {
            writeSize = SizeOf_TypeId + polymorphicObject.GetDataBytesSize();
            
            // Write typeId
            byte* dstPtr = list.Ptr + (long)byteIndex;
            *(int*)dstPtr = polymorphicObject.GetTypeId();
            
            // Write data
            byteIndex += SizeOf_TypeId;
            dstPtr = list.Ptr + (long)byteIndex;
            polymorphicObject.WriteDataTo(dstPtr, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void AddObject<T>(T polymorphicObject, ref UnsafeList<byte> list, out int byteIndex,
            out int writeSize, float growFactor = 1.5f)
            where T : unmanaged, IPolymorphicObject
        {
            writeSize = SizeOf_TypeId + polymorphicObject.GetDataBytesSize();
            
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

            // Write typeId
            byteIndex = initialLength;
            byte* dstPtr = list.Ptr + (long)byteIndex;
            *(int*)dstPtr = polymorphicObject.GetTypeId();
            
            // Write data
            byteIndex += SizeOf_TypeId;
            dstPtr = list.Ptr + (long)byteIndex;
            polymorphicObject.WriteDataTo(dstPtr, out _);
            
            byteIndex = initialLength;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void InsertObject<T>(T polymorphicObject, ref UnsafeList<byte> list, int byteIndex,
            out int writeSize, float growFactor = 1.5f)
            where T : unmanaged, IPolymorphicObject
        {
            writeSize = SizeOf_TypeId + polymorphicObject.GetDataBytesSize();
            
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

            // Write typeId
            dstPtr = list.Ptr + (long)byteIndex;
            *(int*)dstPtr = polymorphicObject.GetTypeId();
            
            // Write data
            byteIndex += SizeOf_TypeId;
            dstPtr = list.Ptr + (long)byteIndex;
            polymorphicObject.WriteDataTo(dstPtr, out _);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ReadObject<T>(ref UnsafeList<byte> list, int byteIndex, out T polymorphicObject, 
            out int readSize)
            where T : unmanaged, IPolymorphicObject
        {
            byte* srcPtr = list.Ptr + (long)byteIndex;
            polymorphicObject = *(T*)srcPtr;
            readSize = SizeOf_TypeId + polymorphicObject.GetDataBytesSize();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool RemoveObject<T>(ref UnsafeList<byte> list, int byteIndex, out T polymorphicObject, 
            out int removedSize)
            where T : unmanaged, IPolymorphicObject
        {
            ReadObject(ref list, byteIndex, out polymorphicObject, out removedSize);

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
        public static PolymorphicObjectNativeListIterator<T> GetIterator<T>(NativeList<byte> list)
            where T : unmanaged, IPolymorphicObject
        {
            return new PolymorphicObjectNativeListIterator<T>(list);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteObject<T>(T polymorphicObject, ref NativeList<byte> list, int byteIndex,
            out int writeSize)
            where T : unmanaged, IPolymorphicObject
        {
            writeSize = SizeOf_TypeId + polymorphicObject.GetDataBytesSize();
            
            // Write typeId
            byte* dstPtr = list.GetUnsafePtr() + (long)byteIndex;
            *(int*)dstPtr = polymorphicObject.GetTypeId();
            
            // Write data
            byteIndex += SizeOf_TypeId;
            dstPtr = list.GetUnsafePtr() + (long)byteIndex;
            polymorphicObject.WriteDataTo(dstPtr, out _);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void AddObject<T>(T polymorphicObject, ref NativeList<byte> list, out int byteIndex,
            out int writeSize, float growFactor = 1.5f)
            where T : unmanaged, IPolymorphicObject
        {
            writeSize = SizeOf_TypeId + polymorphicObject.GetDataBytesSize();
            
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

            // Write typeId
            byteIndex = initialLength;
            byte* dstPtr = list.GetUnsafePtr() + (long)byteIndex;
            *(int*)dstPtr = polymorphicObject.GetTypeId();
            
            // Write data
            byteIndex += SizeOf_TypeId;
            dstPtr = list.GetUnsafePtr() + (long)byteIndex;
            polymorphicObject.WriteDataTo(dstPtr, out _);
            
            byteIndex = initialLength;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void InsertObject<T>(T polymorphicObject, ref NativeList<byte> list, int byteIndex,
            out int writeSize, float growFactor = 1.5f)
            where T : unmanaged, IPolymorphicObject
        {
            writeSize = SizeOf_TypeId + polymorphicObject.GetDataBytesSize();
            
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

            // Write typeId
            dstPtr = list.GetUnsafePtr() + (long)byteIndex;
            *(int*)dstPtr = polymorphicObject.GetTypeId();
            
            // Write data
            byteIndex += SizeOf_TypeId;
            dstPtr = list.GetUnsafePtr() + (long)byteIndex;
            polymorphicObject.WriteDataTo(dstPtr, out _);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ReadObject<T>(ref NativeList<byte> list, int byteIndex, out T polymorphicObject, 
            out int readSize)
            where T : unmanaged, IPolymorphicObject
        {
            byte* srcPtr = list.GetUnsafeReadOnlyPtr() + (long)byteIndex;
            polymorphicObject = *(T*)srcPtr;
            readSize = SizeOf_TypeId + polymorphicObject.GetDataBytesSize();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool RemoveObject<T>(ref NativeList<byte> list, int byteIndex, out T polymorphicObject, 
            out int removedSize)
            where T : unmanaged, IPolymorphicObject
        {
            ReadObject(ref list, byteIndex, out polymorphicObject, out removedSize);

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
        public static PolymorphicObjectDynamicBufferIterator<T> GetIterator<T>(DynamicBuffer<byte> list)
            where T : unmanaged, IPolymorphicObject
        {
            return new PolymorphicObjectDynamicBufferIterator<T>(list);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteObject<T>(T polymorphicObject, ref DynamicBuffer<byte> list, int byteIndex,
            out int writeSize)
            where T : unmanaged, IPolymorphicObject
        {
            writeSize = SizeOf_TypeId + polymorphicObject.GetDataBytesSize();
            
            // Write typeId
            byte* dstPtr = (byte*)list.GetUnsafePtr() + (long)byteIndex;
            *(int*)dstPtr = polymorphicObject.GetTypeId();
            
            // Write data
            byteIndex += SizeOf_TypeId;
            dstPtr = (byte*)list.GetUnsafePtr() + (long)byteIndex;
            polymorphicObject.WriteDataTo(dstPtr, out _);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void AddObject<T>(T polymorphicObject, ref DynamicBuffer<byte> list, out int byteIndex,
            out int writeSize, float growFactor = 1.5f)
            where T : unmanaged, IPolymorphicObject
        {
            writeSize = SizeOf_TypeId + polymorphicObject.GetDataBytesSize();
            
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

            // Write typeId
            byteIndex = initialLength;
            byte* dstPtr = (byte*)list.GetUnsafePtr() + (long)byteIndex;
            *(int*)dstPtr = polymorphicObject.GetTypeId();
            
            // Write data
            byteIndex += SizeOf_TypeId;
            dstPtr = (byte*)list.GetUnsafePtr() + (long)byteIndex;
            polymorphicObject.WriteDataTo(dstPtr, out _);
            
            byteIndex = initialLength;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void InsertObject<T>(T polymorphicObject, ref DynamicBuffer<byte> list, int byteIndex,
            out int writeSize, float growFactor = 1.5f)
            where T : unmanaged, IPolymorphicObject
        {
            writeSize = SizeOf_TypeId + polymorphicObject.GetDataBytesSize();
            
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

            // Write typeId
            dstPtr = (byte*)list.GetUnsafePtr() + (long)byteIndex;
            *(int*)dstPtr = polymorphicObject.GetTypeId();
            
            // Write data
            byteIndex += SizeOf_TypeId;
            dstPtr = (byte*)list.GetUnsafePtr() + (long)byteIndex;
            polymorphicObject.WriteDataTo(dstPtr, out _);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ReadObject<T>(ref DynamicBuffer<byte> list, int byteIndex, out T polymorphicObject, 
            out int readSize)
            where T : unmanaged, IPolymorphicObject
        {
            byte* srcPtr = (byte*)list.GetUnsafeReadOnlyPtr() + (long)byteIndex;
            polymorphicObject = *(T*)srcPtr;
            readSize = SizeOf_TypeId + polymorphicObject.GetDataBytesSize();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool RemoveObject<T>(ref DynamicBuffer<byte> list, int byteIndex, out T polymorphicObject, 
            out int removedSize)
            where T : unmanaged, IPolymorphicObject
        {
            ReadObject(ref list, byteIndex, out polymorphicObject, out removedSize);

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
        public static PolymorphicObjectNativeStreamIterator<T> GetIterator<T>(NativeStream stream)
            where T : unmanaged, IPolymorphicObject
        {
            return new PolymorphicObjectNativeStreamIterator<T>(stream);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void AddObject<T>(T polymorphicObject, ref NativeStream.Writer stream, out int writeSize, float growFactor = 1.5f)
            where T : unmanaged, IPolymorphicObject
        {
            writeSize = polymorphicObject.GetDataBytesSize();
            
            // Write TypeId
            stream.Write(polymorphicObject.GetTypeId());
            
            // Write data
            byte* dstPtr = stream.Allocate(writeSize);
            polymorphicObject.WriteDataTo(dstPtr, out _);
            
            writeSize += SizeOf_TypeId;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool GetNextObject<T>(ref NativeStream.Reader stream, out T polymorphicObject, out int readSize)
            where T : unmanaged, IPolymorphicObject
        {
            polymorphicObject = default;
            
            // Check read for 2 items
            if (stream.RemainingItemCount > 1)
            {
                // Read typeId
                int typeId = stream.Read<int>();
                int dataBytesSize = polymorphicObject.GetDataBytesSizeFor(typeId);
                
                // Read data
                byte* srcPtr = stream.ReadUnsafePtr(dataBytesSize);
                polymorphicObject.SetDataFrom(typeId, srcPtr, out int dataReadSize);
                
                readSize = SizeOf_TypeId + dataReadSize;
                return true;
            }

            readSize = 0;
            return false;
        }
        #endregion
    }
}