using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Trove
{
    /*
    public interface IMultiLinkedListPoolObject
    {
        public int Version { get; set; }
        public MultiLinkedListPool.ObjectHandle PrevObjectHandle { get; set; }
    }

    /// <summary>
    /// Allows storing multiple independent growable lists in a single buffer.
    /// - Guarantees unchanging indexes for all objects added to lists.
    /// - Object allocation has to search through the indexes in ascending order to find the first free slot.
    /// - Each object can only be part of one -and only one- linked list (the API enforces it).
    ///
    /// ----
    /// 
    /// The main use case for this is to provide a solution to the lack of nested collections on entities. Imagine you
    /// have a `DynamicBuffer<Item>`, and each `Item` needs a list of `Effect`s, and `Item`s will gain and lose
    /// `Effect`s during play. You could choose to give each `Item` an Entity that stores a `DynamicBuffer<Effect>`,
    /// but then you have to pay the price of a buffer lookup for each item when accessing `Effect`s. You could choose
    /// to store `Effect`s in a `FixedList` in `Item`, but the storage size of that `FixedList` would be limited, and
    /// it would no doubt make iterating your `Item`s less efficient if you pick a worst-case-scenario `FixedList` size.
    ///
    /// `MultiLinkedListPool` is an alternative
    /// </summary>
    public struct MultiLinkedListPool
    {
        public ObjectHandle LastObjectHandle;

        
        #region DynamicBuffer

        #endregion
    }
    */
}