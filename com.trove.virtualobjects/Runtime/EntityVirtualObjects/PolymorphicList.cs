using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Trove.PolymorphicElements;
using Unity.Entities;
using Unity.Logging;

namespace Trove.VirtualObjects
{
    public unsafe struct PolymorphicList : IVirtualObject
    {
        public struct PolymorphicElementMetaData
        {
            public int StartByteIndex;
            public int Size;
        }

        private List<PolymorphicElementMetaData> _metaDatas;
        public MemoryRangeHandle DataHandle { get; internal set; }

        public int LengthBytes { get; private set; }
        public int CapacityBytes { get; private set; }

        public PolymorphicList(int initialBytesCapacity)
        {
            LengthBytes = default;
            CapacityBytes = initialBytesCapacity;

            DataHandle = default;

            _metaDatas = new List<PolymorphicElementMetaData>(16);
        }

        public VirtualAddress GetAddressOfElementAtByteIndex(int index)
        {
            if (index >= 0 && index < LengthBytes)
            {
                return new VirtualAddress(DataHandle.Address.StartByteIndex + (index * sizeof(T)));
            }

            Log.Error("index is out of range.");
            return default;
        }

        private void CheckModifyCapacityForAdd(ref DynamicBuffer<byte> buffer, int addedBytesCount)
        {
            if (LengthBytes + addedBytesCount > CapacityBytes)
            {
                SetCapacity(ref buffer, LengthBytes * 2);
            }
        }

        public void SetCapacity(ref DynamicBuffer<byte> buffer, int newCapacity)
        {
            int oldCapacity = CapacityBytes;
            CapacityBytes = newCapacity;

            if (CapacityBytes < LengthBytes)
            {
                LengthBytes = CapacityBytes;
                VirtualAddress firstElementOutsideOfNewCapacityAddress = new VirtualAddress(DataHandle.Address.StartByteIndex + CapacityBytes);
                VirtualObjects.Free(ref buffer, firstElementOutsideOfNewCapacityAddress, LengthBytes - CapacityBytes);
                DataHandle = new MemoryRangeHandle(DataHandle.Address, CapacityBytes);
            }
            else if (CapacityBytes > oldCapacity)
            {
                MemoryRangeHandle newDataHandle = new MemoryRangeHandle(VirtualObjects.Allocate(ref buffer, CapacityBytes), CapacityBytes);
                VirtualObjects.Unsafe_MemCopy(ref buffer, newDataHandle.Address, DataHandle.Address, LengthBytes);
                VirtualObjects.Free(ref buffer, DataHandle);
                DataHandle = newDataHandle;
            }
        }

        public void Resize(ref DynamicBuffer<byte> buffer, int newLength)
        {
            int lengthDiff = newLength - LengthBytes;
            LengthBytes += lengthDiff;

            if (lengthDiff > 0)
            {
                CheckModifyCapacityForAdd(ref buffer, lengthDiff);
            }
        }

        public PolymorphicElementMetaData Add<T>(ref DynamicBuffer<byte> buffer, T writer)
            where T : IPolymorphicElementWriter
        {
            int prevLength = LengthBytes;
            LengthBytes += 1;
            VirtualAddress writeAddress = GetAddressOfElementAtByteIndex(prevLength);
            if (writeAddress.IsValid())
            {
                PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
                {
                    StartByteIndex = writeAddress.StartByteIndex,
                    Size = writer.GetTotalSize(),
                };
                _metaDatas.Add(ref buffer, metaData);

                CheckModifyCapacityForAdd(ref buffer, 1);
                writer.Write(VirtualObjects.Unsafe_GetAddressPtr(ref buffer, writeAddress));

                return metaData;
            }

            Log.Error($"Invalid write address");
            return default;
        }

        public PolymorphicElementMetaData InsertAt<T>(ref DynamicBuffer<byte> buffer, int index, T writer)
            where T : IPolymorphicElementWriter
        {
            int prevLength = LengthBytes;
            LengthBytes += 1;
            VirtualAddress writeAddress = GetAddressOfElementAtByteIndex(index);
            if (writeAddress.IsValid())
            {
                PolymorphicElementMetaData metaData = new PolymorphicElementMetaData
                {
                    StartByteIndex = writeAddress.StartByteIndex,
                    Size = writer.GetTotalSize(),
                };
                _metaDatas.InsertAt(ref buffer, index, metaData);

                CheckModifyCapacityForAdd(ref buffer, 1);
                VirtualAddress copyDestinationAddress = new VirtualAddress(writeAddress.StartByteIndex + sizeof(T));
                int lengthToCopy = LengthBytes - writeAddress.StartByteIndex;
                VirtualObjects.Unsafe_MemCopy(ref buffer, copyDestinationAddress, writeAddress, lengthToCopy);
                writer.Write(VirtualObjects.Unsafe_GetAddressPtr(ref buffer, writeAddress));
            }

            Log.Error($"Invalid write address");
            return default;
        }

        public PolymorphicElementMetaData GetElementMetaDataAt(ref DynamicBuffer<byte> buffer, int index)
        {
            VirtualAddress readAddress = GetAddressOfElementAtByteIndex(index);
            if (readAddress.IsValid())
            {
                if (VirtualObjects.Unsafe_Read(ref buffer, readAddress, out T element))
                {
                    return element;
                }

                Log.Error($"Could not read element of size {sizeof(T)} at address {readAddress.StartByteIndex} in buffer of length {buffer.Length}");
            }

            return default;
        }

        public void SetElementAt(ref DynamicBuffer<byte> buffer, int index, T element)
        {
            VirtualAddress writeAddress = GetAddressOfElementAtByteIndex(index);
            if (writeAddress.IsValid())
            {
                if (VirtualObjects.Unsafe_Write(ref buffer, writeAddress, element))
                {
                    return;
                }

                Log.Error("Could not write element value.");
            }
        }

        public void RemoveAt(ref DynamicBuffer<byte> buffer, int index)
        {
            VirtualAddress removeAddress = GetAddressOfElementAtByteIndex(index);
            if (removeAddress.IsValid())
            {
                VirtualAddress copySourceAddress = new VirtualAddress(removeAddress.StartByteIndex - sizeof(T));
                int lengthToCopy = LengthBytes - copySourceAddress.StartByteIndex;
                VirtualObjects.Unsafe_MemCopy(ref buffer, removeAddress, copySourceAddress, lengthToCopy);
                LengthBytes -= 1;
            }
        }

        public void RemoveAtSwapBack(ref DynamicBuffer<byte> buffer, int index)
        {
            VirtualAddress removeAddress = GetAddressOfElementAtByteIndex(index);
            if (removeAddress.IsValid())
            {
                if (LengthBytes > 1)
                {
                    VirtualAddress lastElementAddress = GetAddressOfElementAtByteIndex(LengthBytes - 1);
                    VirtualObjects.Unsafe_MemCopy(ref buffer, removeAddress, lastElementAddress, sizeof(T));
                }
                LengthBytes -= 1;
            }
        }

        public void OnCreate(ref DynamicBuffer<byte> buffer)
        {
            // allocate list memory
            _metaDatas.OnCreate(ref buffer);
            DataHandle = new MemoryRangeHandle(VirtualObjects.Allocate(ref buffer, CapacityBytes), CapacityBytes);
        }

        public void OnDestroy(ref DynamicBuffer<byte> buffer)
        {
            // free data memory
            VirtualObjects.Free(ref buffer, DataHandle);
            _metaDatas.OnDestroy(ref buffer);
        }
    }
}