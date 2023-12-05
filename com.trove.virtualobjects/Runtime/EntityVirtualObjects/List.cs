using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Logging;

namespace Trove.VirtualObjects
{
    /// <summary>
    /// All elements guaranteed contiguous in memory
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public unsafe struct List<T> : IVirtualObject
        where T : unmanaged
    {
        private int _length;
        public int Length
        {
            get
            {
                return _length;
            }
            private set
            {
                _length = value;
                LengthBytes = value * sizeof(T);
                if (_length < 0)
                {
                    throw new Exception("Tried to resize list with negative length");
                }
            }
        }
        private int _capacity;
        public int Capacity
        {
            get
            {
                return _capacity;
            }
            private set
            {
                _capacity = value;
                CapacityBytes = value * sizeof(T);
                if (_capacity < 0)
                {
                    throw new Exception("Tried to set negative list capacity");
                }
            }
        }
        public MemoryRangeHandle DataHandle { get; internal set; }

        public int LengthBytes { get; private set; }
        public int CapacityBytes { get; private set; }

        public List(int initialCapacity)
        {
            _length = 0;
            _capacity = initialCapacity;

            LengthBytes = default;
            CapacityBytes = default;

            DataHandle = default;

            Length = _length;
            Capacity = _capacity;
        }

        public VirtualAddress GetAddressOfElementAtIndex(int index)
        {
            if (index >= 0 && index < Length)
            {
                return new VirtualAddress(DataHandle.Address.StartByteIndex + (index * sizeof(T)));
            }

            throw new ArgumentOutOfRangeException("index is out of range.");
        }

        private void CheckModifyCapacityForAdd(ref DynamicBuffer<byte> buffer, int addedElementsCount)
        {
            if (Length + addedElementsCount > Capacity)
            {
                SetCapacity(ref buffer, Length * 2);
            }
        }

        public void SetCapacity(ref DynamicBuffer<byte> buffer, int newCapacity)
        {
            int oldCapacity = Capacity;
            Capacity = newCapacity;

            if (Capacity < Length)
            {
                Length = Capacity;
                VirtualAddress firstElementOutsideOfNewCapacityAddress = new VirtualAddress(DataHandle.Address.StartByteIndex + CapacityBytes);
                VirtualObjects.Free(ref buffer, firstElementOutsideOfNewCapacityAddress, LengthBytes - CapacityBytes);
                DataHandle = new MemoryRangeHandle(DataHandle.Address, CapacityBytes);
            }
            else if (Capacity > oldCapacity)
            {
                MemoryRangeHandle newDataHandle = new MemoryRangeHandle(VirtualObjects.Allocate(ref buffer, CapacityBytes), CapacityBytes);
                VirtualObjects.Unsafe_MemCopy(ref buffer, newDataHandle.Address, DataHandle.Address, LengthBytes);
                VirtualObjects.Free(ref buffer, DataHandle);
                DataHandle = newDataHandle;
            }
        }

        public void Resize(ref DynamicBuffer<byte> buffer, int newLength)
        {
            int lengthDiff = newLength - Length;
            Length += lengthDiff;

            if (lengthDiff > 0)
            {
                CheckModifyCapacityForAdd(ref buffer, lengthDiff);
            }
        }

        public void Add(ref DynamicBuffer<byte> buffer, T element)
        {
            int prevLength = Length;
            Length += 1;
            VirtualAddress writeAddress = GetAddressOfElementAtIndex(prevLength);
            if (writeAddress.IsValid())
            {
                CheckModifyCapacityForAdd(ref buffer, 1);
                VirtualObjects.Unsafe_Write(ref buffer, writeAddress, element);
            }
            else
            {
                Length = prevLength;
            }
        }

        public void InsertAt(ref DynamicBuffer<byte> buffer, int index, T element)
        {
            int prevLength = Length;
            Length += 1;
            VirtualAddress writeAddress = GetAddressOfElementAtIndex(index);
            if (writeAddress.IsValid())
            {
                CheckModifyCapacityForAdd(ref buffer, 1);
                VirtualAddress copyDestinationAddress = new VirtualAddress(writeAddress.StartByteIndex + sizeof(T));
                int lengthToCopy = LengthBytes - writeAddress.StartByteIndex;
                VirtualObjects.Unsafe_MemCopy(ref buffer, copyDestinationAddress, writeAddress, lengthToCopy);
                VirtualObjects.Unsafe_Write(ref buffer, writeAddress, element);
            }
            else
            {
                Length = prevLength;
            }
        }

        public T GetElementAt(ref DynamicBuffer<byte> buffer, int index)
        {
            VirtualAddress readAddress = GetAddressOfElementAtIndex(index);
            if (readAddress.IsValid())
            {
                if (VirtualObjects.Unsafe_Read(ref buffer, readAddress, out T element))
                {
                    return element;
                }

                throw new Exception($"Could not read element of size {sizeof(T)} at address {readAddress.StartByteIndex} in buffer of length {buffer.Length}");
            }

            return default;
        }

        public void SetElementAt(ref DynamicBuffer<byte> buffer, int index, T element)
        {
            VirtualAddress writeAddress = GetAddressOfElementAtIndex(index);
            if (writeAddress.IsValid())
            {
                if (VirtualObjects.Unsafe_Write(ref buffer, writeAddress, element))
                {
                    return;
                }

                throw new Exception("Could not write element value.");
            }
        }

        public void RemoveAt(ref DynamicBuffer<byte> buffer, int index)
        {
            VirtualAddress removeAddress = GetAddressOfElementAtIndex(index);
            if (removeAddress.IsValid())
            {
                VirtualAddress copySourceAddress = new VirtualAddress(removeAddress.StartByteIndex - sizeof(T));
                int lengthToCopy = LengthBytes - copySourceAddress.StartByteIndex;
                VirtualObjects.Unsafe_MemCopy(ref buffer, removeAddress, copySourceAddress, lengthToCopy);
                Length -= 1;
            }
        }

        public void RemoveAtSwapBack(ref DynamicBuffer<byte> buffer, int index)
        {
            VirtualAddress removeAddress = GetAddressOfElementAtIndex(index);
            if (removeAddress.IsValid())
            {
                if (Length > 1)
                {
                    VirtualAddress lastElementAddress = GetAddressOfElementAtIndex(Length - 1);
                    VirtualObjects.Unsafe_MemCopy(ref buffer, removeAddress, lastElementAddress, sizeof(T));
                }
                Length -= 1;
            }
        }

        public void OnCreate(ref DynamicBuffer<byte> buffer)
        {
            // allocate list memory
            DataHandle = new MemoryRangeHandle(VirtualObjects.Allocate(ref buffer, CapacityBytes), CapacityBytes);
        }

        public void OnDestroy(ref DynamicBuffer<byte> buffer)
        {
            // free data memory
            VirtualObjects.Free(ref buffer, DataHandle);
        }
    }
}