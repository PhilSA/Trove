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

        private void CheckModifyCapacityForAdd(ref EntityVirtualObjectsManager manager, int addedElementsCount)
        {
            if(Length + addedElementsCount > Capacity)
            {
                SetCapacity(ref manager, Length * 2);
            }
        }

        public void SetCapacity(ref EntityVirtualObjectsManager manager, int newCapacity)
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

        public void Resize(ref EntityVirtualObjectsManager manager, int newLength)
        {
            int lengthDiff = newLength - Length;
            Length += lengthDiff;

            if (lengthDiff > 0)
            {
                CheckModifyCapacityForAdd(ref manager, lengthDiff);
            }
        }

        public void Add(ref EntityVirtualObjectsManager manager, T element)
        {
            VirtualAddress writeAddress = GetAddressOfElementAtIndex(Length);
            if (writeAddress.IsValid())
            {
                CheckModifyCapacityForAdd(ref manager, 1);
                manager.Unsafe_Write(writeAddress, element);
                Length += 1;
            }
        }

        public void InsertAt(ref EntityVirtualObjectsManager manager, int index, T element)
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

        public T ElementAt(ref EntityVirtualObjectsManager manager, int index)
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

        public void SetElementAt(ref EntityVirtualObjectsManager manager, int index, T element)
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

        public void RemoveAt(ref EntityVirtualObjectsManager manager, int index)
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

        public void RemoveAtSwapBack(ref EntityVirtualObjectsManager manager, int index)
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

        public void OnCreate(ref EntityVirtualObjectsManager manager, ref ObjectHandle<List<T>> handle)
        {
            // allocate list memory
            DataHandle = new MemoryRangeHandle(manager.Allocate(CapacityBytes), CapacityBytes);
        }

        public void OnDestroy(ref EntityVirtualObjectsManager manager, ref ObjectHandle<List<T>> handle)
        {
            // free data memory
            manager.Free(DataHandle);
        }
    }

    //public unsafe struct LinkedBucketsList<T> : IEntityVirtualObject<LinkedBucketsList<T>>
    //    where T : unmanaged
    //{
    //    public struct Bucket : IEntityVirtualObject<Bucket>
    //    {
    //        private ObjectHandle<Bucket> _selfHandle;

    //        public int Length;
    //        public int Capacity;
    //        public MemoryRangeHandle DataHandle;
    //        public ObjectHandle<Bucket> NextBucketHandle;

    //        public int LengthyBytes => Length * sizeof(T);
    //        public int CapacityBytes => Capacity * sizeof(T);

    //        public void OnCreate(EntityVirtualObjectsManager manager, ref ObjectHandle<Bucket> objectHandle)
    //        {
    //            _selfHandle = objectHandle;
    //        }

    //        public void OnDestroy(EntityVirtualObjectsManager manager, ref ObjectHandle<Bucket> objectHandle)
    //        {
    //        }
    //    }

    //    public struct Iterator
    //    {
    //        public int CurrentIndex;

    //        public bool NextElement(out int index, out T element)
    //        {
    //            // TODO
    //            CurrentIndex++;

    //            index = default;
    //            element = default;
    //            return false;
    //        }
    //    }

    //    private ObjectHandle<LinkedBucketsList<T>> _selfHandle;

    //    public Bucket FirstBucket;

    //    //public LinkedBucketsList(int bucketCapacity)
    //    //{
    //    //    FirstBucket = new Bucket
    //    //    {
    //    //        SelfHandle = 

    //    //        Length = 0,
    //    //        Capacity = bucketCapacity,
    //    //        DataHandle = new MemoryRangeHandle
    //    //        {
    //    //            Index = manager.Allocate(bucketCapacity),
    //    //            Size = bucketCapacity,
    //    //        },
    //    //        NextBucketHandle = default,
    //    //    };

    //    //    _selfHandle = default;
    //    //}

    //    public void Add(T element)
    //    {
    //        // TODO:
    //    }

    //    public T ElementAt(EntityVirtualObjectsManager manager, int index)
    //    {
    //        // TODO:
    //        return default;
    //    }

    //    public void SetElementAt<T>(EntityVirtualObjectsManager manager, int index, T element)
    //        where T : unmanaged
    //    {
    //        // TODO:
    //    }

    //    public void RemoveAt(EntityVirtualObjectsManager manager, int index)
    //    {
    //        // TODO:
    //    }

    //    public void OnCreate(EntityVirtualObjectsManager manager, ref ObjectHandle<LinkedBucketsList<T>> handle)
    //    {
    //        _selfHandle = handle;
            
    //        // TODO: allocate data memory
    //    }

    //    public void OnDestroy(EntityVirtualObjectsManager manager, ref ObjectHandle<LinkedBucketsList<T>> handle)
    //    {
    //        // TODO: free data memory
    //    }
    //}

    //public unsafe struct HashMap<K, V> : IEntityVirtualObject<HashMap<K, V>>
    //    where K : unmanaged
    //    where V : unmanaged
    //{
    //    public struct HashAndValue
    //    {
    //        public Hash128 Hash;
    //        public V Value;
    //    }

    //    private ObjectHandle<HashMap<K, V>> _selfHandle;
    //    private List<HashAndValue> _sortedHashesAndValues;

    //    //public HashMap(int initialCapacity)
    //    //{
    //    //    _selfHandle = default;
    //    //    _sortedHashesAndValues = new List<HashAndValue>(manager, initialCapacity);
    //    //}

    //    public void Add(EntityVirtualObjectsManager manager, K key, V value)
    //    {
    //        // TODO:
    //        int sortedKeyIndex = GetInternalSortedKeyIndex(manager, key);
    //        if (sortedKeyIndex >= 0)
    //        {

    //        }
    //        else
    //        {

    //        }
    //    }

    //    public bool HasKey(EntityVirtualObjectsManager manager, K key)
    //    {
    //        return GetInternalSortedKeyIndex(manager, key) >= 0;
    //    }

    //    public bool TryGet(EntityVirtualObjectsManager manager, K key, out V value)
    //    {
    //        int sortedKeyIndex = GetInternalSortedKeyIndex(manager, key);
    //        if (sortedKeyIndex >= 0)
    //        {
    //            value = _sortedHashesAndValues.ElementAt(manager, sortedKeyIndex).Value;
    //            return false;
    //        }
    //        value = default;
    //        return false;
    //    }

    //    private int GetInternalSortedKeyIndex(EntityVirtualObjectsManager manager, K key)
    //    {
    //        // TODO:
    //        return -1;
    //    }

    //    public void OnCreate(EntityVirtualObjectsManager manager, ref ObjectHandle<HashMap<K, V>> handle)
    //    {
    //        _selfHandle = handle;

    //        // TODO: allocate data memory
    //    }

    //    public void OnDestroy(EntityVirtualObjectsManager manager, ref ObjectHandle<HashMap<K, V>> handle)
    //    {
    //        // TODO: free data memory
    //    }
    //}
}