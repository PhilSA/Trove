using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Logging;
using Unity.Mathematics;

namespace Trove.EntityVirtualObjects
{
    public interface IEntityVirtualObject<T>
        where T : unmanaged
    {
        void OnCreate(EntityVirtualObjectsManager manager, ref ObjectHandle<T> objectHandle);
        void OnDestroy(EntityVirtualObjectsManager manager, ref ObjectHandle<T> objectHandle);
    }

    public struct EntityVirtualObjectsElement : IBufferElementData
    {
        public byte Data;
    }

    public readonly struct ObjectHandle<T> 
        where T : unmanaged
    {
        public readonly ulong ObjectID;
        public readonly int Index;

        public ObjectHandle(ulong objectID, int index)
        {
            ObjectID = objectID;
            Index = index;
        }

        public bool IsValid()
        {
            return Index > 0;
        }
    }

    public readonly struct MemoryRangeHandle
    {
        public readonly int Index;
        public readonly int Size;

        public MemoryRangeHandle(int index, int size)
        {
            Index = index;
            Size = size;
        }

        public bool IsValid()
        {
            return Index > 0 && Size > 0;
        }
    }

    // Struct always at byte index 0 in the buffer
    public unsafe struct EntityVirtualObjectsManager
    {
        public struct FreeMemoryRange
        {
            public int Start;
            public int End;

            public int AvailableSize => End - Start;
        }

        public bool IsCreated { get; private set; }
        public ulong ObjectIDCounter;
        public ObjectHandle<LinkedBucketsList<FreeMemoryRange>> FreeMemoryRangesHandle;

        private DynamicBuffer<byte> _buffer;

        private const int SizeOf_ObjectID = sizeof(ulong);  
        private const int InitialFreeMemoryRangesBucketCapacity = 50;
        private static int InitialBufferResizePaddingBytes = sizeof(EntityVirtualObjectsManager) + sizeof(LinkedBucketsList<FreeMemoryRange>) + (InitialFreeMemoryRangesBucketCapacity * sizeof(FreeMemoryRange)) + 128;

        public static EntityVirtualObjectsManager Get<T>(ref DynamicBuffer<T> buffer) where T : unmanaged
        {
            if (sizeof(T) != 1)
            {
                Log.Error("Error: virtual objects buffer element must be of size 1");
                return default;
            }


            int sizeOfSelf = sizeof(EntityVirtualObjectsManager);

            // Initial length
            if (buffer.Length < sizeOfSelf + InitialBufferResizePaddingBytes)
            {
                buffer.ResizeUninitialized(sizeOfSelf + InitialBufferResizePaddingBytes);
            }

            // Read the mamanger
            EntityVirtualObjectsManager manager = *(EntityVirtualObjectsManager*)buffer.GetUnsafePtr();
            manager._buffer = buffer.Reinterpret<byte>();

            // Creation
            if (!manager.IsCreated)
            {
                // Free memory ranges
                //manager.FreeMemoryRangesHandle = manager.CreateObject(new LinkedBucketsList<FreeMemoryRange>(InitialFreeMemoryRangesBucketCapacity));
                //if(manager.GetObject(manager.FreeMemoryRangesHandle, out LinkedBucketsList<FreeMemoryRange> freeMemoryRanges))
                //{
                //    freeMemoryRanges.Add(new FreeMemoryRange
                //    {
                //        Start = sizeOfSelf,
                //        End = buffer.Length,
                //    });

                //    manager.IsCreated = true;

                //    // Write manager back
                //    manager.WriteBackChanges();
                //}
                //else
                //{
                //    Log.Error($"Error: could not initialize Virtual Objects Manager");
                //}
            }

            return manager;
        }

        internal void WriteBackChanges()
        {
            Unsafe_Write(0, this);
        }

        public bool Unsafe_Read<T>(int index, out T element)
            where T : unmanaged
        {
            if (index >= 0 && index + sizeof(T) <= _buffer.Length)
            {
                element = *(T*)(Unsafe_GetIndexPtr(index));
                return true;
            }

            element = default;
            return false;
        }

        public bool Unsafe_Write<T>(int index, T element)
            where T : unmanaged
        {
            if (index >= 0 && index + sizeof(T) <= _buffer.Length)
            {
                *(T*)(Unsafe_GetIndexPtr(index)) = element;
                return true;
            }

            element = default;
            return false;
        }

        public byte* Unsafe_GetIndexPtr(int index)
        {
            return (byte*)_buffer.GetUnsafePtr() + (long)(index);
        }

        public ObjectHandle<T> CreateObject<T>(T newObject)
            where T : unmanaged, IEntityVirtualObject<T>
        {
            int startIndex = Allocate(SizeOf_ObjectID + sizeof(T));

            // write ID
            ObjectIDCounter++;
            Unsafe_Write(startIndex, ObjectIDCounter);

            // write object
            Unsafe_Write(startIndex + SizeOf_ObjectID, newObject);

            // Create handle
            ObjectHandle<T> handle = new ObjectHandle<T>(ObjectIDCounter, startIndex);

            // Call OnCreate
            newObject.OnCreate(this, ref handle);

            WriteBackChanges();

            return handle;
        }

        public void DestroyObject<T>(ObjectHandle<T> handle)
            where T : unmanaged, IEntityVirtualObject<T>
        {
            // If this is false, it would mean we're trying to destroy an already-destroyed object
            if(GetObject(handle, out T objectInstance))
            {
                // Call OnDestroy
                objectInstance.OnDestroy(this, ref handle);

                Free(handle.Index, SizeOf_ObjectID + sizeof(T));
            }
        }

        private bool IsHandlePointingToValidObject<T>(ObjectHandle<T> handle)
            where T : unmanaged, IEntityVirtualObject<T>
        {
            return handle.IsValid() && 
                Unsafe_Read(handle.Index, out ulong idInMemory) && 
                idInMemory == handle.ObjectID;
        }

        public bool GetObject<T>(ObjectHandle<T> handle, out T result)
            where T : unmanaged, IEntityVirtualObject<T>
        {
            if (IsHandlePointingToValidObject(handle))
            {
                if (Unsafe_Read(handle.Index + SizeOf_ObjectID, out result))
                {
                    return true;
                }
            }

            result = default;
            return false;
        }

        public bool SetObject<T>(ObjectHandle<T> handle, T value)
            where T : unmanaged, IEntityVirtualObject<T>
        {
            if (IsHandlePointingToValidObject(handle))
            {
                if (Unsafe_Write(handle.Index + SizeOf_ObjectID, value))
                {
                    return true;
                }
            }

            return false;
        }

        public int Allocate(int size)
        {
            GetObject(FreeMemoryRangesHandle, out LinkedBucketsList<FreeMemoryRange> freeRanges);

            //// Find first free range with enough size
            //FreeMemoryRange chosenRange = default;
            //int chosenRangeIndex = -1;
            //bool existingLastRangeEndsAtBufferEnd = false;
            //for (int i = 0; i < freeRanges.Length; i++)
            //{
            //    FreeMemoryRange tmpRange = freeRanges.ElementAt(this, i);
            //    if (tmpRange.AvailableSize >= size)
            //    {
            //        chosenRangeIndex = i;
            //        chosenRange = tmpRange;
            //    }
            //    else if (i == freeRanges.Length - 1 && tmpRange.End == _buffer.Length)
            //    {
            //        existingLastRangeEndsAtBufferEnd = true;
            //    }
            //}

            //// If no range found, expand buffer memory
            //if (chosenRangeIndex < 0)
            //{
            //    int prevLength = _buffer.Length;
            //    int sizeIncrease = math.max(_buffer.Length, size);
            //    _buffer.ResizeUninitialized(_buffer.Length + sizeIncrease);

            //    if (existingLastRangeEndsAtBufferEnd)
            //    {
            //        chosenRangeIndex = _buffer.Length - 1;
            //        chosenRange = freeRanges.ElementAt(this, chosenRangeIndex);
            //        chosenRange.End = _buffer.Length;
            //    }
            //    else
            //    {
            //        chosenRangeIndex = freeRanges.Length;
            //        chosenRange = new FreeMemoryRange
            //        {
            //            Start = prevLength,
            //            End = _buffer.Length,
            //        };
            //    }
            //}

            //// Remove size from beginning of chosen range
            //int allocationStartIndex = chosenRange.Start;
            //chosenRange.Start += size;
            //if (chosenRange.AvailableSize > 0)
            //{
            //    freeRanges.SetElementAt(this, chosenRangeIndex, chosenRange);
            //}
            //else
            //{
            //    freeRanges.RemoveAt(this, chosenRangeIndex);
            //}

            //return allocationStartIndex;

            return default;
        }

        public void Free(MemoryRangeHandle memoryRangeHandle)
        {
            Free(memoryRangeHandle.Index, memoryRangeHandle.Size);
        }

        public void Free(int index, int size)
        {

            // todo: add to free ranges (and potentially merge ranges)
        }

        public void TrimMemory()
        {
            // todo: resize buffer capacity to fit required memory
        }
    }
}