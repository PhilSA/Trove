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

namespace Trove.VirtualObjects
{
    public interface IVirtualObject
    {
        public void OnCreate(ref VirtualObjectsManager manager);
        public void OnDestroy(ref VirtualObjectsManager manager);
    }

    public struct VirtualAddress
    {
        public int StartByteIndex;

        public VirtualAddress(int startByteIndex)
        {
            StartByteIndex = startByteIndex;
        }

        public bool IsValid()
        {
            return StartByteIndex > 0;
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
        public List<FreeMemoryRange> FreeMemoryRanges;

        private DynamicBuffer<byte> _buffer;

        private const int InitialFreeMemoryRangesCapacity = 100;
        private const int InitialObjectMemorySizeBytes = 256;
        private static int InitialAvailableMemorySize = sizeof(VirtualObjectsManager) + sizeof(List<FreeMemoryRange>) + (InitialFreeMemoryRangesCapacity * sizeof(FreeMemoryRange)) + InitialObjectMemorySizeBytes;

        public static VirtualObjectsManager Get(ref DynamicBuffer<byte> buffer)
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
                // Init free ranges list 
                manager.FreeMemoryRanges = new List<FreeMemoryRange>(InitialFreeMemoryRangesCapacity);
                int listBytesCapacity = manager.FreeMemoryRanges.CapacityBytes;
                manager.FreeMemoryRanges.DataHandle = new MemoryRangeHandle(new VirtualAddress(sizeOfSelf), listBytesCapacity);
                manager.FreeMemoryRanges.Add(ref manager, new FreeMemoryRange(manager.FreeMemoryRanges.DataHandle.Address.StartByteIndex + manager.FreeMemoryRanges.DataHandle.Size, buffer.Length));
                
                manager.IsCreated = true;
                manager.WriteBackChanges();
            }

            return manager;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteBackChanges()
        {
            *(VirtualObjectsManager*)_buffer.GetUnsafePtr() = this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Unsafe_Read<T>(VirtualAddress address, out T element)
            where T : unmanaged
        {
            if (address.IsValid() && address.StartByteIndex + sizeof(T) <= _buffer.Length)
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
            if (address.IsValid() && address.StartByteIndex + sizeof(T) <= _buffer.Length)
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
            address.StartByteIndex += offset;
            return Unsafe_Read<T>(address, out element);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Unsafe_ReadAsRef<T>(VirtualAddress address, int offset, out bool success)
            where T : unmanaged
        {
            address.StartByteIndex += offset;
            return ref Unsafe_ReadAsRef<T>(address, out success);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Unsafe_Write<T>(VirtualAddress address, T element)
            where T : unmanaged
        {
            // TODO: make it impossible to overwrite the manager at default address?

            if (address.IsValid() && address.StartByteIndex + sizeof(T) <= _buffer.Length)
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
            address.StartByteIndex += offset;
            return Unsafe_Write<T>(address, element);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte* Unsafe_GetAddressPtr(VirtualAddress address)
        {
            return (byte*)_buffer.GetUnsafePtr() + (long)(address.StartByteIndex);
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
        public ObjectHandle<T> CreateObject<T>(ref T newObject)
            where T : unmanaged, IVirtualObject
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
            newObject.OnCreate(ref this);

            WriteBackChanges();

            return handle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DestroyObject<T>(ObjectHandle<T> handle)
            where T : unmanaged, IVirtualObject
        {
            // If this is false, it would mean we're trying to destroy an already-destroyed object
            if(GetObjectCopy(handle, out T objectInstance))
            {
                // Call OnDestroy
                objectInstance.OnDestroy(ref this);

                Free(handle.Address, sizeof(ObjectHeader) + sizeof(T));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsHandlePointingToValidObject<T>(ObjectHandle<T> handle)
            where T : unmanaged, IVirtualObject
        {
            return handle.IsValid() && 
                Unsafe_Read(handle.Address, out ObjectHeader header) &&
                header.ObjectID == handle.ObjectID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetObjectCopy<T>(ObjectHandle<T> handle, out T result)
            where T : unmanaged, IVirtualObject
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

        /// <summary>
        /// Unsafe because any new object allocation after getting the ref might grow and move the dynamicBuffer
        /// memory that backs the objects, therefore making the ref point to invalid memory.
        /// 
        /// Keep in mind adding elements to a list, for example, may create new allocations and therefore invalidate refs.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Unsafe_GetObjectRef<T>(ObjectHandle<T> handle, out bool success)
            where T : unmanaged, IVirtualObject
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
            where T : unmanaged, IVirtualObject
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
            // Find first free range with enough size
            FreeMemoryRange chosenRange = default;
            int chosenRangeIndex = -1;
            for (int i = 0; i < FreeMemoryRanges.Length; i++)
            {
                FreeMemoryRange evaluatedRange = FreeMemoryRanges.GetElementAt(ref this, i);
                if (evaluatedRange.AvailableSize >= sizeBytes)
                {
                    chosenRangeIndex = i;
                    chosenRange = evaluatedRange;
                    break;
                }
            }

            // If no range found, expand buffer memory and add new free range
            if (chosenRangeIndex < 0)
            {
                // Resize buffer
                int prevLength = _buffer.Length;
                int sizeIncrease = math.max(_buffer.Length, sizeBytes);
                _buffer.ResizeUninitialized(_buffer.Length + sizeIncrease);

                // Add new range
                {
                    FreeMemoryRange newRange = new FreeMemoryRange(prevLength, _buffer.Length);
                    FreeMemoryRange lastCurrentRange = FreeMemoryRanges.GetElementAt(ref this, FreeMemoryRanges.Length - 1);
                        
                    // If the last free memory range ended where the new one would start, just expand the old one
                    if (lastCurrentRange.End == newRange.Start)
                    {
                        chosenRange = new FreeMemoryRange(lastCurrentRange.Start, newRange.End);
                        FreeMemoryRanges.SetElementAt(ref this, FreeMemoryRanges.Length - 1, chosenRange);
                    }
                    // Otherwise, create a new range
                    else
                    {
                        chosenRange = newRange;
                        FreeMemoryRanges.Add(ref this, newRange);
                    }
                }

                chosenRangeIndex = FreeMemoryRanges.Length - 1;
            }

            // Remove size from beginning of chosen range
            VirtualAddress allocatedAddress = new VirtualAddress(chosenRange.Start);
            FreeMemoryRange modifiedChosenRange = new FreeMemoryRange(chosenRange.Start + sizeBytes, chosenRange.End);
            if (modifiedChosenRange.AvailableSize > 0)
            {
                FreeMemoryRanges.SetElementAt(ref this, chosenRangeIndex, modifiedChosenRange);
            }
            else
            {
                FreeMemoryRanges.RemoveAt(ref this, chosenRangeIndex);
            }

            WriteBackChanges();

            return allocatedAddress;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(MemoryRangeHandle memoryRangeHandle)
        {
            Free(memoryRangeHandle.Address, memoryRangeHandle.Size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Free(VirtualAddress address, int sizeBytes)
        {
            if (address.IsValid())
            {
                FreeMemoryRange freedRange = new FreeMemoryRange(address.StartByteIndex, address.StartByteIndex + sizeBytes);

                if (freedRange.End > _buffer.Length)
                {
                    throw new Exception("Tried to free memory that was outside the length of the buffer");
                }

                // Clear freed memory (this is required because valid object detection depends on it)
                UnsafeUtility.MemClear(Unsafe_GetAddressPtr(address), sizeBytes);

                // Insert range in order
                bool addedRange = false;
                for (int i = 0; i < FreeMemoryRanges.Length; i++)
                {
                    FreeMemoryRange evaluatedRange = FreeMemoryRanges.GetElementAt(ref this, i);
                    if(evaluatedRange.End == freedRange.Start)
                    {
                        FreeMemoryRanges.SetElementAt(ref this, i, new FreeMemoryRange(evaluatedRange.Start, freedRange.End));
                        addedRange = true;
                        break;
                    }
                    else if (freedRange.End == evaluatedRange.Start)
                    {
                        FreeMemoryRanges.SetElementAt(ref this, i, new FreeMemoryRange(freedRange.Start, evaluatedRange.End));
                        addedRange = true;
                        break;
                    }
                    // Insert before evaluated range that has a higher start index
                    else if(evaluatedRange.Start > freedRange.Start)
                    {
                        FreeMemoryRanges.InsertAt(ref this, i, freedRange);
                        addedRange = true;
                        break;
                    }
                }

                if(!addedRange)
                {
                    FreeMemoryRanges.Add(ref this, freedRange);
                }

                WriteBackChanges();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TrimBufferMemory(int maxFreeTrailingBytes)
        {
            FreeMemoryRange lastFreeMemoryRange = FreeMemoryRanges.GetElementAt(ref this, FreeMemoryRanges.Length - 1);
            if(lastFreeMemoryRange.End == _buffer.Length && lastFreeMemoryRange.AvailableSize > maxFreeTrailingBytes)
            {
                int newRangeEnd = lastFreeMemoryRange.Start + maxFreeTrailingBytes;
                int trimBytesAmount = lastFreeMemoryRange.End - newRangeEnd;
                lastFreeMemoryRange = new FreeMemoryRange(lastFreeMemoryRange.Start, newRangeEnd);

                _buffer.ResizeUninitialized(newRangeEnd);
                FreeMemoryRanges.SetElementAt(ref this, FreeMemoryRanges.Length - 1, lastFreeMemoryRange);

                WriteBackChanges();
            }
        }
    }
}