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
        public void OnCreate(ref VirtualObjectsManager manager, ref ObjectHandle<T> objectHandle);
        public void OnDestroy(ref VirtualObjectsManager manager, ref ObjectHandle<T> objectHandle);
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

    public readonly struct ObjectHeader
    {
        public readonly ulong ObjectID;

        public ObjectHeader(ulong objectID)
        {
            ObjectID = objectID;
        }
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
    public unsafe struct VirtualObjectsManager
    {
        public struct FreeMemoryRange
        {
            public readonly int Start;
            public readonly int End;
            public readonly int AvailableSize;

            public FreeMemoryRange(int start, int end)
            {
                Start = start;
                End = end;
                AvailableSize = end - start;
            }
        }

        public bool IsCreated { get; private set; }
        public ulong ObjectIDCounter;
        public ObjectHandle<List<FreeMemoryRange>> FreeMemoryRangesHandle;

        private DynamicBuffer<byte> _buffer;

        private const int InitialFreeMemoryRangesCapacity = 50;
        private const int InitialObjectMemorySizeBytes = 256;
        private static int InitialAvailableMemorySize = sizeof(VirtualObjectsManager) + sizeof(List<FreeMemoryRange>) + (InitialFreeMemoryRangesCapacity * sizeof(FreeMemoryRange)) + InitialObjectMemorySizeBytes;

        public static ref VirtualObjectsManager GetRef(ref DynamicBuffer<byte> buffer)
        {
            int sizeOfSelf = sizeof(VirtualObjectsManager);

            // Initial length
            if (buffer.Length < sizeOfSelf + InitialAvailableMemorySize)
            {
                buffer.ResizeUninitialized(sizeOfSelf + InitialAvailableMemorySize);
            }

            // Read the mamanger
            ref VirtualObjectsManager manager = ref *(VirtualObjectsManager*)buffer.GetUnsafePtr();
            manager._buffer = buffer;

            // Creation
            if (!manager.IsCreated)
            {
                manager.FreeMemoryRangesHandle = manager.CreateObject(new List<FreeMemoryRange>(InitialFreeMemoryRangesCapacity));
                ref List<FreeMemoryRange> freeMemoryRanges = ref manager.GetObjectRef<List<FreeMemoryRange>>(manager.FreeMemoryRangesHandle, out bool success);
                if (success)
                {
                    freeMemoryRanges.Add(ref manager, new FreeMemoryRange(sizeOfSelf, buffer.Length));
                    manager.IsCreated = true;
                }
                else
                {
                    throw new Exception("Failed to initialize Virtual Objects Manager");
                }
            }

            return ref manager;
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
            VirtualAddress objectAddress = Allocate(sizeof(ObjectHeader) + sizeof(T));

            // write header
            ObjectIDCounter++;
            ObjectHeader header = new ObjectHeader(ObjectIDCounter);
            Unsafe_Write(objectAddress, header);

            // write object
            Unsafe_Write(objectAddress, sizeof(ObjectHeader), newObject);

            // Create handle
            ObjectHandle<T> handle = new ObjectHandle<T>(ObjectIDCounter, objectAddress);

            // Call OnCreate
            newObject.OnCreate(ref this, ref handle);

            return handle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DestroyObject<T>(ObjectHandle<T> handle)
            where T : unmanaged, IEntityVirtualObject<T>
        {
            // If this is false, it would mean we're trying to destroy an already-destroyed object
            if(GetObjectCopy(handle, out T objectInstance))
            {
                // Call OnDestroy
                objectInstance.OnDestroy(ref this, ref handle);

                Free(handle.Address, sizeof(ObjectHeader) + sizeof(T));
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
        public bool GetObjectCopy<T>(ObjectHandle<T> handle, out T result)
            where T : unmanaged, IEntityVirtualObject<T>
        {
            if (IsHandlePointingToValidObject(handle))
            {
                if (Unsafe_Read(handle.Address, sizeof(ObjectHeader), out result))
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
                return ref Unsafe_ReadAsRef<T>(handle.Address, sizeof(ObjectHeader), out success);
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
                if (Unsafe_Write(handle.Address, sizeof(ObjectHeader), value))
                {
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VirtualAddress Allocate(int sizeBytes)
        {
            ref List<FreeMemoryRange> freeMemoryRanges = ref GetObjectRef(FreeMemoryRangesHandle, out bool success);
            if(success)
            {
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

                // TODO: do a mem clear

                //return allocationStartIndex;
            }

            throw new Exception("Failed to get free memory ranges list");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(MemoryRangeHandle memoryRangeHandle)
        {
            Free(memoryRangeHandle.Address, memoryRangeHandle.Size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(VirtualAddress address, int sizeBytes)
        {
            // todo: add to free ranges (and potentially merge ranges)
            ref List<FreeMemoryRange> freeMemoryRanges = ref GetObjectRef(FreeMemoryRangesHandle, out bool success);
            if (success)
            {
                // todo; if freed the last range, trim buffer memory?
            }

            throw new Exception("Failed to get free memory ranges list");
        }
    }
}