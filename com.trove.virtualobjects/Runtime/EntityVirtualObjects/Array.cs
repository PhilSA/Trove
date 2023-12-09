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
    public unsafe struct Array<T> : IVirtualObject
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
                    Log.Error("Tried to resize array with negative length");
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
            if (index >= 0 && index < Length)
            {
                return new VirtualAddress(DataHandle.Address.StartByteIndex + (index * sizeof(T)));
            }

            Log.Error("index is out of range.");
            return default;
        }

        public void Resize(ref DynamicBuffer<byte> buffer, int newLength)
        {
            int lengthDiff = newLength - Length;
            int oldLengthBytes = LengthBytes;
            Length += lengthDiff;

            if (lengthDiff < 0)
            {
                VirtualAddress firstElementOutsideOfNewLengthAddress = new VirtualAddress(DataHandle.Address.StartByteIndex + LengthBytes);
                VirtualObjects.Free(ref buffer, firstElementOutsideOfNewLengthAddress, LengthBytes - oldLengthBytes);
                DataHandle = new MemoryRangeHandle(DataHandle.Address, LengthBytes);
            }
            else if (lengthDiff > 0)
            {
                MemoryRangeHandle newDataHandle = new MemoryRangeHandle(VirtualObjects.Allocate(ref buffer, LengthBytes), LengthBytes);
                VirtualObjects.Unsafe_MemCopy(ref buffer, newDataHandle.Address, DataHandle.Address, LengthBytes);
                VirtualObjects.Free(ref buffer, DataHandle);
                DataHandle = newDataHandle;
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

                Log.Error("Could not read element value.");
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

                Log.Error("Could not write element value.");
            }
        }

        public void OnCreate(ref DynamicBuffer<byte> buffer)
        {
            // allocate list memory
            DataHandle = new MemoryRangeHandle(VirtualObjects.Allocate(ref buffer, LengthBytes), LengthBytes);
        }

        public void OnDestroy(ref DynamicBuffer<byte> buffer)
        {
            // free data memory
            VirtualObjects.Free(ref buffer, DataHandle);
        }
    }
}