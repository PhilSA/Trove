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
        private ObjectHandle<List<T>> _selfHandle;
        internal MemoryRangeHandle _dataHandle;

        public int Length;
        public int Capacity;

        public int LengthyBytes => Length * sizeof(T);
        public int CapacityBytes => Capacity * sizeof(T);

        public List(int initialCapacity)
        {
            _selfHandle = default;

            Length = 0;
            Capacity = initialCapacity;

            _dataHandle = new MemoryRangeHandle();
        }

        public void Add(EntityVirtualObjectsManager manager, T element)
        {
            // TODO:

            // Write changes to memory
            manager.SetObject(_selfHandle, this);
        }

        public T ElementAt(EntityVirtualObjectsManager manager, int index)
        {
            return default;
        }

        public void SetElementAt(EntityVirtualObjectsManager manager, int index, T element)
        {
            // TODO:
        }

        public void RemoveAt(EntityVirtualObjectsManager manager, int index)
        {
            // TODO:
            // - should this handle object destruction?
        }

        private void WriteBackChanges(EntityVirtualObjectsManager manager)
        {
        }

        public void OnCreate(EntityVirtualObjectsManager manager, ref ObjectHandle<List<T>> handle)
        {
            _selfHandle = handle;

            // allocate list memory
            _dataHandle = new MemoryRangeHandle(manager.Allocate(CapacityBytes), CapacityBytes);
        }

        public void OnDestroy(EntityVirtualObjectsManager manager, ref ObjectHandle<List<T>> handle)
        {
            // free data memory
            manager.Free(_dataHandle);
        }
    }

    public unsafe struct PolymorphicList : IEntityVirtualObject<PolymorphicList>
    {
        public struct ElementMetaData
        {
            public int ByteIndexInListData;
            public int ByteSize;
        }

        private ObjectHandle<PolymorphicList> _selfHandle;
        private List<ElementMetaData> _metaDatas;
        private MemoryRangeHandle _dataHandle;

        public int LengthBytes;
        public int CapacityBytes;

        public PolymorphicList(int initialElementsCapacity, int initialDataBytesCapacity)
        {
            _selfHandle = default;
            _metaDatas = new List<ElementMetaData>(initialElementsCapacity);

            LengthBytes = 0;
            CapacityBytes = initialDataBytesCapacity;

            _dataHandle = default;
        }

        public void Add<T>(EntityVirtualObjectsManager manager, T data)
            where T : unmanaged
        {
            // TODO:
        }

        public void Add<T1, T2>(EntityVirtualObjectsManager manager, T1 data1, T1 data2)
            where T1 : unmanaged
            where T2 : unmanaged
        {
            // TODO:
        }

        public void Add<T1, T2, T3>(EntityVirtualObjectsManager manager, T1 data1, T2 data2, T3 data3)
            where T1 : unmanaged
            where T2 : unmanaged
            where T3 : unmanaged
        {
            // TODO:
        }

        public bool TryGetElementMetaData(EntityVirtualObjectsManager manager, int index, out ElementMetaData metaData)
        {
            // TODO: 

            metaData = default;
            return false;
        }

        public void RemoveAt(EntityVirtualObjectsManager manager, int index)
        {
            // TODO:
            // - should this handle object destruction?
        }

        public MemoryRangeHandle GetDataHandle()
        {
            return _dataHandle;
        }

        public int GetByteIndexInBufferMemory(ElementMetaData metaData)
        {
            if(_dataHandle.IsValid())
            {
                return _dataHandle.Index + metaData.ByteIndexInListData;
            }
            return -1;
        }

        public void OnCreate(EntityVirtualObjectsManager manager, ref ObjectHandle<PolymorphicList> handle)
        {
            _selfHandle = handle;

            // Allocate list data
            {
                _metaDatas._dataHandle = new MemoryRangeHandle(manager.Allocate(_metaDatas.CapacityBytes), _metaDatas.CapacityBytes);
                _dataHandle = new MemoryRangeHandle(manager.Allocate(CapacityBytes), CapacityBytes);
            }
        }

        public void OnDestroy(EntityVirtualObjectsManager manager, ref ObjectHandle<PolymorphicList> handle)
        {
            // free data memory
            manager.Free(_metaDatas._dataHandle);
            manager.Free(_dataHandle);
        }
    }

    public unsafe struct LinkedBucketsList<T> : IEntityVirtualObject<LinkedBucketsList<T>>
        where T : unmanaged
    {
        public struct Bucket : IEntityVirtualObject<Bucket>
        {
            private ObjectHandle<Bucket> _selfHandle;

            public int Length;
            public int Capacity;
            public MemoryRangeHandle DataHandle;
            public ObjectHandle<Bucket> NextBucketHandle;

            public int LengthyBytes => Length * sizeof(T);
            public int CapacityBytes => Capacity * sizeof(T);

            public void OnCreate(EntityVirtualObjectsManager manager, ref ObjectHandle<Bucket> objectHandle)
            {
                _selfHandle = objectHandle;
            }

            public void OnDestroy(EntityVirtualObjectsManager manager, ref ObjectHandle<Bucket> objectHandle)
            {
            }
        }

        public struct Iterator
        {
            public int CurrentIndex;

            public bool NextElement(out int index, out T element)
            {
                // TODO
                CurrentIndex++;

                index = default;
                element = default;
                return false;
            }
        }

        private ObjectHandle<LinkedBucketsList<T>> _selfHandle;

        public Bucket FirstBucket;

        //public LinkedBucketsList(int bucketCapacity)
        //{
        //    FirstBucket = new Bucket
        //    {
        //        SelfHandle = 

        //        Length = 0,
        //        Capacity = bucketCapacity,
        //        DataHandle = new MemoryRangeHandle
        //        {
        //            Index = manager.Allocate(bucketCapacity),
        //            Size = bucketCapacity,
        //        },
        //        NextBucketHandle = default,
        //    };

        //    _selfHandle = default;
        //}

        public void Add(T element)
        {
            // TODO:
        }

        public T ElementAt(EntityVirtualObjectsManager manager, int index)
        {
            // TODO:
            return default;
        }

        public void SetElementAt<T>(EntityVirtualObjectsManager manager, int index, T element)
            where T : unmanaged
        {
            // TODO:
        }

        public void RemoveAt(EntityVirtualObjectsManager manager, int index)
        {
            // TODO:
        }

        public void OnCreate(EntityVirtualObjectsManager manager, ref ObjectHandle<LinkedBucketsList<T>> handle)
        {
            _selfHandle = handle;
            
            // TODO: allocate data memory
        }

        public void OnDestroy(EntityVirtualObjectsManager manager, ref ObjectHandle<LinkedBucketsList<T>> handle)
        {
            // TODO: free data memory
        }
    }

    public unsafe struct HashMap<K, V> : IEntityVirtualObject<HashMap<K, V>>
        where K : unmanaged
        where V : unmanaged
    {
        public struct HashAndValue
        {
            public Hash128 Hash;
            public V Value;
        }

        private ObjectHandle<HashMap<K, V>> _selfHandle;
        private List<HashAndValue> _sortedHashesAndValues;

        //public HashMap(int initialCapacity)
        //{
        //    _selfHandle = default;
        //    _sortedHashesAndValues = new List<HashAndValue>(manager, initialCapacity);
        //}

        public void Add(EntityVirtualObjectsManager manager, K key, V value)
        {
            // TODO:
            int sortedKeyIndex = GetInternalSortedKeyIndex(manager, key);
            if (sortedKeyIndex >= 0)
            {

            }
            else
            {

            }
        }

        public bool HasKey(EntityVirtualObjectsManager manager, K key)
        {
            return GetInternalSortedKeyIndex(manager, key) >= 0;
        }

        public bool TryGet(EntityVirtualObjectsManager manager, K key, out V value)
        {
            int sortedKeyIndex = GetInternalSortedKeyIndex(manager, key);
            if (sortedKeyIndex >= 0)
            {
                value = _sortedHashesAndValues.ElementAt(manager, sortedKeyIndex).Value;
                return false;
            }
            value = default;
            return false;
        }

        private int GetInternalSortedKeyIndex(EntityVirtualObjectsManager manager, K key)
        {
            // TODO:
            return -1;
        }

        public void OnCreate(EntityVirtualObjectsManager manager, ref ObjectHandle<HashMap<K, V>> handle)
        {
            _selfHandle = handle;

            // TODO: allocate data memory
        }

        public void OnDestroy(EntityVirtualObjectsManager manager, ref ObjectHandle<HashMap<K, V>> handle)
        {
            // TODO: free data memory
        }
    }
}