using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Logging;
using Unity.Mathematics;

namespace Trove.EntityVirtualObjects
{
    public interface IEntityVirtualObject<T>
        where T : unmanaged
    {
        public void OnCreate(ref EntityVirtualObjectsManager manager, ref ObjectHandle<T> objectHandle);
        public void OnDestroy(ref EntityVirtualObjectsManager manager, ref ObjectHandle<T> objectHandle);
    }

    public struct VirtualAddress
    {
        public int Value;

        public bool IsValid()
        {
            return Value != 0;
        }
    }

    public struct EntityVirtualObjectsElement : IBufferElementData
    {
        public byte Data;
    }

    public readonly struct ObjectHandle<T> 
        where T : unmanaged
    {
        public readonly ulong ObjectID;
        public readonly VirtualAddress Address;

        public ObjectHandle(ulong objectID, VirtualAddress Address)
        {
            ObjectID = objectID;
            this.Address = Address;
        }

        public bool IsValid()
        {
            return Address.IsValid();
        }
    }

    public readonly struct MemoryRangeHandle
    {
        public readonly VirtualAddress Address;
        public readonly int Size;

        public MemoryRangeHandle(VirtualAddress address, int size)
        {
            Address = address;
            Size = size;
        }

        public bool IsValid()
        {
            return Address.IsValid() && Size > 0;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void WriteBackChanges()
        {
            Unsafe_Write(default, this); // Default address is the manager (starts at byte 0)
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Unsafe_Read<T>(VirtualAddress address, out T element)
            where T : unmanaged
        {
            if (address.IsValid() && address.Value + sizeof(T) <= _buffer.Length)
            {
                element = *(T*)(Unsafe_GetAddressPtr(address));
                return true;
            }

            element = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Unsafe_ReadAsRef<T>(VirtualAddress address, out bool success)
            where T : unmanaged
        {
            if (address.IsValid() && address.Value + sizeof(T) <= _buffer.Length)
            {
                success = true;
                return ref *(T*)(Unsafe_GetAddressPtr(address));
            }

            success = false;
            return ref *(T*)_buffer.GetUnsafePtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Unsafe_Read<T>(VirtualAddress address, int offset, out T element)
            where T : unmanaged
        {
            address.Value += offset;
            return Unsafe_Read<T>(address, out element);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Unsafe_ReadAsRef<T>(VirtualAddress address, int offset, out bool success)
            where T : unmanaged
        {
            address.Value += offset;
            return ref Unsafe_ReadAsRef<T>(address, out success);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Unsafe_Write<T>(VirtualAddress address, T element)
            where T : unmanaged
        {
            // TODO: make it impossible to overwrite the manager at default address?

            if (address.IsValid() && address.Value + sizeof(T) <= _buffer.Length)
            {
                *(T*)(Unsafe_GetAddressPtr(address)) = element;
                return true;
            }

            element = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Unsafe_Write<T>(VirtualAddress address, int offset, T element)
            where T : unmanaged
        {
            address.Value += offset;
            return Unsafe_Write<T>(address, element);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* Unsafe_GetAddressPtr(VirtualAddress address)
        {
            return (byte*)_buffer.GetUnsafePtr() + (long)(address.Value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Unsafe_MemCopy(VirtualAddress destination, VirtualAddress source, int size)
        {
            if(size > 0 && destination.IsValid() && source.IsValid())
            {
                UnsafeUtility.MemCpy(Unsafe_GetAddressPtr(destination), Unsafe_GetAddressPtr(source), size);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ObjectHandle<T> CreateObject<T>(T newObject)
            where T : unmanaged, IEntityVirtualObject<T>
        {
            VirtualAddress objectAddress = Allocate(SizeOf_ObjectID + sizeof(T));

            // write ID
            ObjectIDCounter++;
            Unsafe_Write(objectAddress, ObjectIDCounter);

            // write object
            Unsafe_Write(objectAddress, SizeOf_ObjectID, newObject);

            // Create handle
            ObjectHandle<T> handle = new ObjectHandle<T>(ObjectIDCounter, objectAddress);

            // Call OnCreate
            newObject.OnCreate(ref this, ref handle);

            WriteBackChanges();

            return handle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DestroyObject<T>(ObjectHandle<T> handle)
            where T : unmanaged, IEntityVirtualObject<T>
        {
            // If this is false, it would mean we're trying to destroy an already-destroyed object
            if(GetObject(handle, out T objectInstance))
            {
                // Call OnDestroy
                objectInstance.OnDestroy(ref this, ref handle);

                Free(handle.Address, SizeOf_ObjectID + sizeof(T));

                WriteBackChanges();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsHandlePointingToValidObject<T>(ObjectHandle<T> handle)
            where T : unmanaged, IEntityVirtualObject<T>
        {
            return handle.IsValid() && 
                Unsafe_Read(handle.Address, out ulong idInMemory) && 
                idInMemory == handle.ObjectID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetObject<T>(ObjectHandle<T> handle, out T result)
            where T : unmanaged, IEntityVirtualObject<T>
        {
            if (IsHandlePointingToValidObject(handle))
            {
                if (Unsafe_Read(handle.Address, SizeOf_ObjectID, out result))
                {
                    return true;
                }
            }

            result = default;
            return false;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetObjectRef<T>(ObjectHandle<T> handle, out bool success)
            where T : unmanaged, IEntityVirtualObject<T>
        {
            if (IsHandlePointingToValidObject(handle))
            {
                return ref Unsafe_ReadAsRef<T>(handle.Address, SizeOf_ObjectID, out success);
            }

            success = false;
            return ref *(T*)_buffer.GetUnsafePtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetObject<T>(ObjectHandle<T> handle, T value)
            where T : unmanaged, IEntityVirtualObject<T>
        {
            if (IsHandlePointingToValidObject(handle))
            {
                if (Unsafe_Write(handle.Address, SizeOf_ObjectID, value))
                {
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VirtualAddress Allocate(int size)
        {
            // INDEX 0 MUST MEAN INVALID BECAUSE THAT'S WHERE THE MANAGER IS

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(MemoryRangeHandle memoryRangeHandle)
        {
            NativeList<int>
            Free(memoryRangeHandle.Address, memoryRangeHandle.Size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(VirtualAddress address, int sizeBytes)
        {

            // todo: add to free ranges (and potentially merge ranges)
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TrimMemory()
        {
            // TODO: do this automatically on free?
            // todo: resize buffer capacity to fit required memory
        }
    }
}