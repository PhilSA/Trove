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
    public unsafe struct Array<T> : IEntityVirtualObject<Array<T>>
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
                    throw new Exception("Tried to resize array with negative length");
                }
            }
        }
        public MemoryRangeHandle DataHandle { get; private set; }

        public int LengthBytes { get; private set; }

        public Array(int length)
        {
            _length = length;
            LengthBytes = default;
            DataHandle = default;
            Length = _length;
        }

        public VirtualAddress GetAddressOfElementAtIndex(int index)
        {
            if(index >= 0 && index < Length)
            {
                return new VirtualAddress { Value = DataHandle.Address.Value + (index * sizeof(T)) };
            }

            throw new ArgumentOutOfRangeException("index is out of range.");
        }

        public void Resize(ref VirtualObjectsManager manager, int newLength)
        {
            int lengthDiff = newLength - Length;
            int oldLengthBytes = LengthBytes;
            Length += lengthDiff;

            if (lengthDiff < 0)
            {
                VirtualAddress firstElementOutsideOfNewLengthAddress = new VirtualAddress { Value = DataHandle.Address.Value + LengthBytes };
                manager.Free(firstElementOutsideOfNewLengthAddress, LengthBytes - oldLengthBytes);
                DataHandle = new MemoryRangeHandle(DataHandle.Address, LengthBytes);
            }
            else if (lengthDiff > 0)
            {
                MemoryRangeHandle newDataHandle = new MemoryRangeHandle(manager.Allocate(LengthBytes), LengthBytes);
                manager.Unsafe_MemCopy(newDataHandle.Address, DataHandle.Address, LengthBytes);
                manager.Free(DataHandle);
                DataHandle = newDataHandle;
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

        public void OnCreate(ref VirtualObjectsManager manager, ref ObjectHandle<Array<T>> handle)
        {
            // allocate list memory
            DataHandle = new MemoryRangeHandle(manager.Allocate(LengthBytes), LengthBytes);
        }

        public void OnDestroy(ref VirtualObjectsManager manager, ref ObjectHandle<Array<T>> handle)
        {
            // free data memory
            manager.Free(DataHandle);
        }
    }
}