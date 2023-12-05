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
        public void OnCreate(ref DynamicBuffer<byte> buffer);
        public void OnDestroy(ref DynamicBuffer<byte> buffer);
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

    public unsafe static class VirtualObjects
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VirtualObjectsManagerData GetManagerData(ref DynamicBuffer<byte> buffer)
        {
            VirtualObjectsManagerData managerData = *(VirtualObjectsManagerData*)buffer.GetUnsafePtr();

            // Creation
            if (!managerData.IsCreated)
            {
                int sizeOfSelf = sizeof(VirtualObjectsManagerData);

                managerData.ObjectIDCounter = 0;

                // Init free ranges list 
                managerData.FreeMemoryRanges = new List<FreeMemoryRange>(VirtualObjectsManagerData.InitialFreeMemoryRangesCapacity);
                int listBytesCapacity = managerData.FreeMemoryRanges.CapacityBytes;
                buffer.ResizeUninitialized(sizeOfSelf + listBytesCapacity + VirtualObjectsManagerData.InitialObjectMemorySizeBytes);
                managerData.FreeMemoryRanges.DataHandle = new MemoryRangeHandle(new VirtualAddress(sizeOfSelf), listBytesCapacity);
                managerData.FreeMemoryRanges.Add(ref buffer, new FreeMemoryRange(managerData.FreeMemoryRanges.DataHandle.Address.StartByteIndex + managerData.FreeMemoryRanges.DataHandle.Size, buffer.Length));

                managerData.IsCreated = true;
                SetManagerData(ref buffer, managerData);

                Log.Debug($"Creating VOManager with free ranges {managerData.FreeMemoryRanges.GetElementAt(ref buffer, 0).Start} - {managerData.FreeMemoryRanges.GetElementAt(ref buffer, 0).End}");
            }

            if (buffer.Length < sizeof(VirtualObjectsManagerData))
            {
                Log.Error("Could not read VirtualObjectsManagerData");
            }

            return managerData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetManagerData(ref DynamicBuffer<byte> buffer, VirtualObjectsManagerData managerData)
        {
            if (buffer.Length < sizeof(VirtualObjectsManagerData))
            {
                Log.Error("Could not write VirtualObjectsManagerData");
            }

            *(VirtualObjectsManagerData*)buffer.GetUnsafePtr() = managerData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Unsafe_Read<T>(ref DynamicBuffer<byte> buffer, VirtualAddress address, out T element)
            where T : unmanaged
        {
            if (address.IsValid() && address.StartByteIndex + sizeof(T) <= buffer.Length)
            {
                element = *(T*)(Unsafe_GetAddressPtr(ref buffer, address));
                return true;
            }

            element = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Unsafe_ReadAsRef<T>(ref DynamicBuffer<byte> buffer, VirtualAddress address, out bool success)
            where T : unmanaged
        {
            if (address.IsValid() && address.StartByteIndex + sizeof(T) <= buffer.Length)
            {
                success = true;
                return ref *(T*)(Unsafe_GetAddressPtr(ref buffer, address));
            }

            success = false;
            return ref *(T*)buffer.GetUnsafePtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Unsafe_Read<T>(ref DynamicBuffer<byte> buffer, VirtualAddress address, int offset, out T element)
            where T : unmanaged
        {
            address.StartByteIndex += offset;
            return Unsafe_Read<T>(ref buffer, address, out element);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T Unsafe_ReadAsRef<T>(ref DynamicBuffer<byte> buffer, VirtualAddress address, int offset, out bool success)
            where T : unmanaged
        {
            address.StartByteIndex += offset;
            return ref Unsafe_ReadAsRef<T>(ref buffer, address, out success);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Unsafe_Write<T>(ref DynamicBuffer<byte> buffer, VirtualAddress address, T element)
            where T : unmanaged
        {
            // TODO: make it impossible to overwrite the manager at default address?

            if (address.IsValid() && address.StartByteIndex + sizeof(T) <= buffer.Length)
            {
                *(T*)(Unsafe_GetAddressPtr(ref buffer, address)) = element;
                return true;
            }

            element = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Unsafe_Write<T>(ref DynamicBuffer<byte> buffer, VirtualAddress address, int offset, T element)
            where T : unmanaged
        {
            address.StartByteIndex += offset;
            return Unsafe_Write<T>(ref buffer, address, element);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte* Unsafe_GetAddressPtr(ref DynamicBuffer<byte> buffer, VirtualAddress address)
        {
            return (byte*)buffer.GetUnsafePtr() + (long)(address.StartByteIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Unsafe_MemCopy(ref DynamicBuffer<byte> buffer, VirtualAddress destination, VirtualAddress source, int size)
        {
            if (size > 0 && destination.IsValid() && source.IsValid())
            {
                UnsafeUtility.MemCpy(Unsafe_GetAddressPtr(ref buffer, destination), Unsafe_GetAddressPtr(ref buffer, source), size);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ObjectHandle<T> CreateObject<T>(ref DynamicBuffer<byte> buffer, ref T newObject)
            where T : unmanaged, IVirtualObject
        {
            VirtualObjectsManagerData managerData = GetManagerData(ref buffer);

            VirtualAddress objectAddress = Allocate(ref buffer, sizeof(ObjectHeader) + sizeof(T));

            // write header
            managerData.ObjectIDCounter++;
            ObjectHeader header = new ObjectHeader(managerData.ObjectIDCounter);
            Unsafe_Write(ref buffer, objectAddress, header);

            // write object
            Unsafe_Write(ref buffer, objectAddress, sizeof(ObjectHeader), newObject);

            // Create handle
            ObjectHandle<T> handle = new ObjectHandle<T>(managerData.ObjectIDCounter, objectAddress);

            // Call OnCreate
            newObject.OnCreate(ref buffer);

            SetManagerData(ref buffer, managerData);

            return handle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DestroyObject<T>(ref DynamicBuffer<byte> buffer, ObjectHandle<T> handle)
            where T : unmanaged, IVirtualObject
        {
            // If this is false, it would mean we're trying to destroy an already-destroyed object
            if (GetObjectCopy(ref buffer, handle, out T objectInstance))
            {
                // Call OnDestroy
                objectInstance.OnDestroy(ref buffer);

                Free(ref buffer, handle.Address, sizeof(ObjectHeader) + sizeof(T));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsHandlePointingToValidObject<T>(ref DynamicBuffer<byte> buffer, ObjectHandle<T> handle)
            where T : unmanaged, IVirtualObject
        {
            return handle.IsValid() &&
                Unsafe_Read(ref buffer, handle.Address, out ObjectHeader header) &&
                header.ObjectID == handle.ObjectID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetObjectCopy<T>(ref DynamicBuffer<byte> buffer, ObjectHandle<T> handle, out T result)
            where T : unmanaged, IVirtualObject
        {
            if (IsHandlePointingToValidObject(ref buffer, handle))
            {
                if (Unsafe_Read(ref buffer, handle.Address, sizeof(ObjectHeader), out result))
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
        public static ref T Unsafe_GetObjectRef<T>(ref DynamicBuffer<byte> buffer, ObjectHandle<T> handle, out bool success)
            where T : unmanaged, IVirtualObject
        {
            if (IsHandlePointingToValidObject(ref buffer, handle))
            {
                return ref Unsafe_ReadAsRef<T>(ref buffer, handle.Address, sizeof(ObjectHeader), out success);
            }

            success = false;
            return ref *(T*)buffer.GetUnsafePtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SetObject<T>(ref DynamicBuffer<byte> buffer, ObjectHandle<T> handle, T value)
            where T : unmanaged, IVirtualObject
        {
            if (IsHandlePointingToValidObject(ref buffer, handle))
            {
                if (Unsafe_Write(ref buffer, handle.Address, sizeof(ObjectHeader), value))
                {
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VirtualAddress Allocate(ref DynamicBuffer<byte> buffer, int sizeBytes)
        {
            VirtualObjectsManagerData managerData = GetManagerData(ref buffer);

            // Find first free range with enough size
            FreeMemoryRange chosenRange = default;
            int chosenRangeIndex = -1;
            for (int i = 0; i < managerData.FreeMemoryRanges.Length; i++)
            {
                FreeMemoryRange evaluatedRange = managerData.FreeMemoryRanges.GetElementAt(ref buffer, i);
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
                int prevLength = buffer.Length;
                int sizeIncrease = math.max(buffer.Length, sizeBytes);
                buffer.ResizeUninitialized(buffer.Length + sizeIncrease);

                // Add new range
                {
                    FreeMemoryRange newRange = new FreeMemoryRange(prevLength, buffer.Length);
                    FreeMemoryRange lastCurrentRange = managerData.FreeMemoryRanges.GetElementAt(ref buffer, managerData.FreeMemoryRanges.Length - 1);

                    // If the last free memory range ended where the new one would start, just expand the old one
                    if (lastCurrentRange.End == newRange.Start)
                    {
                        chosenRange = new FreeMemoryRange(lastCurrentRange.Start, newRange.End);
                        managerData.FreeMemoryRanges.SetElementAt(ref buffer, managerData.FreeMemoryRanges.Length - 1, chosenRange);
                    }
                    // Otherwise, create a new range
                    else
                    {
                        chosenRange = newRange;
                        managerData.FreeMemoryRanges.Add(ref buffer, newRange);
                    }
                }

                chosenRangeIndex = managerData.FreeMemoryRanges.Length - 1;
            }

            Log.Debug($"Allocating.... chosenRangeIndex {chosenRangeIndex} chosenRange {chosenRange.Start} - {chosenRange.End}");

            // Remove size from beginning of chosen range
            VirtualAddress allocatedAddress = new VirtualAddress(chosenRange.Start);
            FreeMemoryRange modifiedChosenRange = new FreeMemoryRange(chosenRange.Start + sizeBytes, chosenRange.End);
            if (modifiedChosenRange.AvailableSize > 0)
            {
                managerData.FreeMemoryRanges.SetElementAt(ref buffer, chosenRangeIndex, modifiedChosenRange);
            }
            else
            {
                managerData.FreeMemoryRanges.RemoveAt(ref buffer, chosenRangeIndex);
            }

            SetManagerData(ref buffer, managerData);

            return allocatedAddress;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Free(ref DynamicBuffer<byte> buffer, MemoryRangeHandle memoryRangeHandle)
        {
            Free(ref buffer, memoryRangeHandle.Address, memoryRangeHandle.Size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Free(ref DynamicBuffer<byte> buffer, VirtualAddress address, int sizeBytes)
        {
            if (address.IsValid())
            {
                VirtualObjectsManagerData managerData = GetManagerData(ref buffer);

                FreeMemoryRange freedRange = new FreeMemoryRange(address.StartByteIndex, address.StartByteIndex + sizeBytes);

                if (freedRange.End > buffer.Length)
                {
                    throw new Exception("Tried to free memory that was outside the length of the buffer");
                }

                // Clear freed memory (this is required because valid object detection depends on it)
                UnsafeUtility.MemClear(Unsafe_GetAddressPtr(ref buffer, address), sizeBytes);

                // Insert range in order
                bool addedRange = false;
                for (int i = 0; i < managerData.FreeMemoryRanges.Length; i++)
                {
                    FreeMemoryRange evaluatedRange = managerData.FreeMemoryRanges.GetElementAt(ref buffer, i);
                    if (evaluatedRange.End == freedRange.Start)
                    {
                        managerData.FreeMemoryRanges.SetElementAt(ref buffer, i, new FreeMemoryRange(evaluatedRange.Start, freedRange.End));
                        addedRange = true;
                        break;
                    }
                    else if (freedRange.End == evaluatedRange.Start)
                    {
                        managerData.FreeMemoryRanges.SetElementAt(ref buffer, i, new FreeMemoryRange(freedRange.Start, evaluatedRange.End));
                        addedRange = true;
                        break;
                    }
                    // Insert before evaluated range that has a higher start index
                    else if (evaluatedRange.Start > freedRange.Start)
                    {
                        managerData.FreeMemoryRanges.InsertAt(ref buffer, i, freedRange);
                        addedRange = true;
                        break;
                    }
                }

                if (!addedRange)
                {
                    managerData.FreeMemoryRanges.Add(ref buffer, freedRange);
                }

                SetManagerData(ref buffer, managerData);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void TrimBufferMemory(ref DynamicBuffer<byte> buffer, int maxFreeTrailingBytes)
        {
            VirtualObjectsManagerData managerData = GetManagerData(ref buffer);
            FreeMemoryRange lastFreeMemoryRange = managerData.FreeMemoryRanges.GetElementAt(ref buffer, managerData.FreeMemoryRanges.Length - 1);
            if (lastFreeMemoryRange.End == buffer.Length && lastFreeMemoryRange.AvailableSize > maxFreeTrailingBytes)
            {

                int newRangeEnd = lastFreeMemoryRange.Start + maxFreeTrailingBytes;
                int trimBytesAmount = lastFreeMemoryRange.End - newRangeEnd;
                lastFreeMemoryRange = new FreeMemoryRange(lastFreeMemoryRange.Start, newRangeEnd);

                buffer.ResizeUninitialized(newRangeEnd);
                managerData.FreeMemoryRanges.SetElementAt(ref buffer, managerData.FreeMemoryRanges.Length - 1, lastFreeMemoryRange);

                SetManagerData(ref buffer, managerData);
            }
        }
    }

    // Struct always at byte index 0 in the buffer
    public unsafe struct VirtualObjectsManagerData
    {
        internal bool IsCreated;
        internal ulong ObjectIDCounter;
        internal List<FreeMemoryRange> FreeMemoryRanges;

        internal const int InitialFreeMemoryRangesCapacity = 100;
        internal const int InitialObjectMemorySizeBytes = 256;
    }

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
}