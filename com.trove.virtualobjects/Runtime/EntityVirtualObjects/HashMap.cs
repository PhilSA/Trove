using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Logging;

namespace Trove.EntityVirtualObjects
{
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