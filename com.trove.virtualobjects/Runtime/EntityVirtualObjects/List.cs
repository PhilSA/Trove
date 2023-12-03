using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Logging;

namespace Trove.EntityVirtualObjects
{
    /// <summary>
    /// All elements guaranteed contiguous in memory
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public unsafe struct List<T> : IEntityVirtualObject<List<T>>
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
        public MemoryRangeHandle DataHandle { get; private set; }

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
            if(index >= 0 && index < Length)
            {
                return new VirtualAddress { Value = DataHandle.Address.Value + (index * sizeof(T)) };
            }

            throw new ArgumentOutOfRangeException("index is out of range.");
        }

        private void CheckModifyCapacityForAdd(ref VirtualObjectsManager manager, int addedElementsCount)
        {
            if(Length + addedElementsCount > Capacity)
            {
                SetCapacity(ref manager, Length * 2);
            }
        }

        public void SetCapacity(ref VirtualObjectsManager manager, int newCapacity)
        {
            int oldCapacity = Capacity;
            Capacity = newCapacity;

            if (Capacity < Length)
            {
                Length = Capacity;
                VirtualAddress firstElementOutsideOfNewCapacityAddress = new VirtualAddress { Value = DataHandle.Address.Value + CapacityBytes };
                manager.Free(firstElementOutsideOfNewCapacityAddress, LengthBytes - CapacityBytes);
                DataHandle = new MemoryRangeHandle(DataHandle.Address, CapacityBytes);
            }
            else if(Capacity > oldCapacity)
            {
                MemoryRangeHandle newDataHandle = new MemoryRangeHandle(manager.Allocate(CapacityBytes), CapacityBytes);
                manager.Unsafe_MemCopy(newDataHandle.Address, DataHandle.Address, LengthBytes);
                manager.Free(DataHandle);
                DataHandle = newDataHandle;
            }
        }

        public void Resize(ref VirtualObjectsManager manager, int newLength)
        {
            int lengthDiff = newLength - Length;
            Length += lengthDiff;

            if (lengthDiff > 0)
            {
                CheckModifyCapacityForAdd(ref manager, lengthDiff);
            }
        }

        public void Add(ref VirtualObjectsManager manager, T element)
        {
            VirtualAddress writeAddress = GetAddressOfElementAtIndex(Length);
            if (writeAddress.IsValid())
            {
                CheckModifyCapacityForAdd(ref manager, 1);
                manager.Unsafe_Write(writeAddress, element);
                Length += 1;
            }
        }

        public void InsertAt(ref VirtualObjectsManager manager, int index, T element)
        {
            VirtualAddress writeAddress = GetAddressOfElementAtIndex(index);
            if (writeAddress.IsValid())
            {
                CheckModifyCapacityForAdd(ref manager, 1);
                VirtualAddress copyDestinationAddress = new VirtualAddress { Value = writeAddress.Value + sizeof(T) };
                int lengthToCopy = LengthBytes - writeAddress.Value;
                manager.Unsafe_MemCopy(copyDestinationAddress, writeAddress, lengthToCopy);
                manager.Unsafe_Write(writeAddress, element);
                Length += 1;
            }
        }

        public T ElementAt(ref VirtualObjectsManager manager, int index)
        {
            VirtualAddress readAddress = GetAddressOfElementAtIndex(index);
            if (readAddress.IsValid())
            {
                if(manager.Unsafe_Read(readAddress, out T element))
                {
                    return element;
                }

                throw new Exception("Could not read element value.");
            }

            return default;
        }

        public void SetElementAt(ref VirtualObjectsManager manager, int index, T element)
        {
            VirtualAddress writeAddress = GetAddressOfElementAtIndex(index);
            if (writeAddress.IsValid())
            {
                if(manager.Unsafe_Write(writeAddress, element))
                {
                    return;
                }

                throw new Exception("Could not write element value.");
            }
        }

        public void RemoveAt(ref VirtualObjectsManager manager, int index)
        {
            VirtualAddress removeAddress = GetAddressOfElementAtIndex(index);
            if (removeAddress.IsValid())
            {
                VirtualAddress copySourceAddress = new VirtualAddress { Value = removeAddress.Value - sizeof(T) };
                int lengthToCopy = LengthBytes - copySourceAddress.Value;
                manager.Unsafe_MemCopy(removeAddress, copySourceAddress, lengthToCopy);
                Length -= 1;
            }
        }

        public void RemoveAtSwapBack(ref VirtualObjectsManager manager, int index)
        {
            VirtualAddress removeAddress = GetAddressOfElementAtIndex(index);
            if (removeAddress.IsValid())
            {
                if (Length > 1)
                {
                    VirtualAddress lastElementAddress = GetAddressOfElementAtIndex(Length - 1);
                    manager.Unsafe_MemCopy(removeAddress, lastElementAddress, sizeof(T));
                }
                Length -= 1;
            }
        }

        public void OnCreate(ref VirtualObjectsManager manager, ref ObjectHandle<List<T>> handle)
        {
            // allocate list memory
            DataHandle = new MemoryRangeHandle(manager.Allocate(CapacityBytes), CapacityBytes);
        }

        public void OnDestroy(ref VirtualObjectsManager manager, ref ObjectHandle<List<T>> handle)
        {
            // free data memory
            manager.Free(DataHandle);
        }
    }
}