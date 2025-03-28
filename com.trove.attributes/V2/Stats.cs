using System.Runtime.CompilerServices;
using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;

namespace Trove.Stats
{
    public interface IStatsModifierStack
    {
        public void Reset();
        public void Apply(ref float statBaseValue, ref float statValue);
    }

    public interface IStatsModifier<TStatModifierStack>
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public uint ID { get; set; } // TODO: is it ok for set to be public
        public void AddObservedStatsToList(ref NativeList<int> observedStatIndexes);
        public void Apply(
            in NativeList<Stat> stats,
            ref TStatModifierStack stack);
    }

    public struct StatsWorld<TStatModifier, TStatModifierStack, TStatCustomData>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
        where TStatModifierStack : unmanaged, IStatsModifierStack
        where TStatCustomData : unmanaged
    {
        private NativeList<Stat> _stats;   
        private NativeList<StatData<TStatModifier, TStatModifierStack, TStatCustomData>> _statDatas;     
        private NativeList<IndexRange> _statFreeRanges;     
        private NativeList<TStatModifier> _statModifiers;
        private NativeList<IndexRange> _statModifierFreeRanges;
        private NativeList<int> _statObservers;
        private NativeList<IndexRange> _statObserverFreeRanges;

        private NativeReference<uint> _modifierIDCounter;
        private NativeList<int> _tmpStatsList;
        
        public NativeList<StatChangeEvent> StatChangeEvents;

        private float _growFactor;
        private int _modifiersCapacityPerStat;
        private int _observersCapacityPerStat;

        /// <summary>
        /// NOTE: All capacities will automatically grow past the initial capacity when needed
        /// </summary>
        public StatsWorld(int statsCapacity, int modifiersCapacityPerStat, int observersCapacityPerStat, float poolGrowFactor)
        {
            _growFactor = poolGrowFactor;
            _modifiersCapacityPerStat = modifiersCapacityPerStat;
            _observersCapacityPerStat = observersCapacityPerStat;
            
            _stats = new NativeList<Stat>(Allocator.Persistent);
            _statDatas = new NativeList<StatData<TStatModifier, TStatModifierStack, TStatCustomData>>(Allocator.Persistent);
            _statFreeRanges = new NativeList<IndexRange>(Allocator.Persistent);
            _statModifiers = new NativeList<TStatModifier>(Allocator.Persistent);
            _statModifierFreeRanges = new NativeList<IndexRange>(Allocator.Persistent);
            _statObservers = new NativeList<int>(Allocator.Persistent);
            _statObserverFreeRanges = new NativeList<IndexRange>(Allocator.Persistent);
            
            _modifierIDCounter = new NativeReference<uint>(Allocator.Persistent);
            _tmpStatsList = new NativeList<int>(Allocator.Persistent);
            
            StatChangeEvents = new NativeList<StatChangeEvent>(Allocator.Persistent);
            
            CollectionUtilities.PoolInit(ref _stats, ref _statFreeRanges, statsCapacity);
            _statDatas.Resize(_stats.Length, NativeArrayOptions.ClearMemory);
            CollectionUtilities.PoolInit(ref _statModifiers, ref _statModifierFreeRanges, _modifiersCapacityPerStat * statsCapacity);
            CollectionUtilities.PoolInit(ref _statObservers, ref _statObserverFreeRanges, _observersCapacityPerStat * statsCapacity);
        }

        public void Dispose(JobHandle jobHandle = default)
        {
            if (_stats.IsCreated)
            {
                _stats.Dispose(jobHandle);
            }
            if (_statDatas.IsCreated)
            {
                _statDatas.Dispose(jobHandle);
            }
            if (_statFreeRanges.IsCreated)
            {
                _statFreeRanges.Dispose(jobHandle);
            }
            if (_statModifiers.IsCreated)
            {
                _statModifiers.Dispose(jobHandle);
            }
            if (_statModifierFreeRanges.IsCreated)
            {
                _statModifierFreeRanges.Dispose(jobHandle);
            }
            if (_statObservers.IsCreated)
            {
                _statObservers.Dispose(jobHandle);
            }
            if (_statObserverFreeRanges.IsCreated)
            {
                _statObserverFreeRanges.Dispose(jobHandle);
            }
            if (_modifierIDCounter.IsCreated)
            {
                _modifierIDCounter.Dispose(jobHandle);
            }
            if (_tmpStatsList.IsCreated)
            {
                _tmpStatsList.Dispose(jobHandle);
            }
            if (StatChangeEvents.IsCreated)
            {
                StatChangeEvents.Dispose(jobHandle);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CheckStatIndexValid(int index)
        {
            return index >= 0 && index < _stats.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Stat GetStat(int statIndex)
        {
            return _stats[statIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TStatCustomData GetStatCustomData(int statIndex)
        {
            return _statDatas[statIndex].CustomData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStatCustomData(int statIndex, TStatCustomData customData)
        {
            StatData<TStatModifier, TStatModifierStack, TStatCustomData> statData = _statDatas[statIndex];
            statData.CustomData = customData;
            _statDatas[statIndex] = statData;
        }
        
        /// <summary>
        /// Private because we always have to manually call UpdateStat after modification
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe ref Stat GetStatRef(int statIndex)
        {
            return ref UnsafeUtility.ArrayElementAsRef<Stat>(_stats.GetUnsafePtr(), statIndex);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStatBaseValue(int statIndex, float baseValue)
        {
            ref Stat stat = ref GetStatRef(statIndex);
            if (stat.IsCreated)
            {
                stat.BaseValue = baseValue;
                UpdateStat(statIndex, ref stat);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddStatBaseValue(int statIndex, float baseValueAdd)
        {
            ref Stat stat = ref GetStatRef(statIndex);
            if (stat.IsCreated)
            {
                stat.BaseValue += baseValueAdd;
                UpdateStat(statIndex, ref stat);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MultiplyStatBaseValue(int statIndex, float baseValueMul)
        {
            ref Stat stat = ref GetStatRef(statIndex);
            if (stat.IsCreated)
            {
                stat.BaseValue *= baseValueMul;
                UpdateStat(statIndex, ref stat);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStatProduceChangeEvents(int statIndex, bool value)
        {
            ref Stat stat = ref GetStatRef(statIndex);
            if (stat.IsCreated)
            {
                stat._produceChangeEvents = value ? (byte)1 : (byte)0;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateStat(int statIndex)
        {
            ref Stat stat = ref GetStatRef(statIndex);
            if (stat.IsCreated)
            {
                UpdateStat(statIndex, ref stat);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateStat(int statIndex, ref Stat stat)
        {
            Stat initialStat = stat;
            StatData<TStatModifier, TStatModifierStack, TStatCustomData> statData = _statDatas[statIndex];

            // Apply Modifiers
            TStatModifierStack modifierStack = new TStatModifierStack();
            modifierStack.Reset();
            for (int m = 0; m < statData.Modifiers.Length; m++)
            {
                TStatModifier modifier =
                    PoolList<TStatModifier>.GetElement(in statData.Modifiers, ref _statModifiers, m);
                modifier.Apply(
                    in _stats,
                    ref modifierStack);
            }

            modifierStack.Apply(ref stat.BaseValue, ref stat.Value);

            // Stat change events
            if (stat._produceChangeEvents == 1)
            {
                StatChangeEvents.Add(new StatChangeEvent
                {
                    StatIndex = statIndex,
                    PrevValue = initialStat,
                    NewValue = stat,
                });
            }

            // Notify Observers
            _tmpStatsList.Clear();
            for (int o = 0; o < statData.Observers.Length; o++)
            {
                int observerStatIndex =
                    PoolList<int>.GetElement(in statData.Observers, ref _statObservers, o);
                _tmpStatsList.Add(observerStatIndex);
            }

            for (int d = 0; d < _tmpStatsList.Length; d++)
            {
                int observerStatIndex = _tmpStatsList[d];
                
                // Prevent self-observing stat
                // TODO: do this detection when adding modifiers, and also infinite loop detection
                if (observerStatIndex != statIndex) 
                {
                    UpdateStat(observerStatIndex);
                }
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CreateStat(float baseValue, bool produceChangeEvents, TStatCustomData customData, out int statIndex)
        {
            Stat newStat = new Stat
            {
                _isCreated = 1,
                _produceChangeEvents = produceChangeEvents ? (byte)1 : (byte)0,
                BaseValue = baseValue,
                Value = baseValue,
            };
            CollectionUtilities.PoolAdd(ref _stats, ref _statFreeRanges, in newStat, out statIndex, _growFactor);

            // Keep the stat arrays in sync
            if (_statDatas.Length != _stats.Length)
            {
                _statDatas.Resize(_stats.Length, NativeArrayOptions.ClearMemory);
            }
            
            // Alloc stat modifiers and observers lists
            _statDatas[statIndex] = new StatData<TStatModifier, TStatModifierStack, TStatCustomData>
            {
                CustomData = customData,
                Modifiers = PoolList<TStatModifier>.Create(ref _statModifiers, ref _statModifierFreeRanges, _modifiersCapacityPerStat, _growFactor, _growFactor),
                Observers = PoolList<int>.Create(ref _statObservers, ref _statObserverFreeRanges, _observersCapacityPerStat, _growFactor, _growFactor),
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveStat(int statIndex)
        {
            Stat stat = _stats[statIndex];
            if (stat.IsCreated)
            {
                StatData<TStatModifier, TStatModifierStack, TStatCustomData> statData = _statDatas[statIndex];
                
                // Do a stat update with a null stat
                _stats[statIndex] = default;
                UpdateStat(statIndex);
                
                // Free pool lists and clear stat data
                PoolList<TStatModifier>.Free(ref statData.Modifiers, ref _statModifiers, ref _statModifierFreeRanges);
                PoolList<int>.Free(ref statData.Observers, ref _statObservers, ref _statObserverFreeRanges);
                _statDatas[statIndex] = default;

                // Free stat
                CollectionUtilities.PoolRemove(ref _stats, ref _statFreeRanges, statIndex);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddStatModifier(int statIndex, TStatModifier modifier, out StatModifierHandle statModifierHandle)
        {
            Stat stat = _stats[statIndex];
            if (stat.IsCreated)
            {
                StatData<TStatModifier, TStatModifierStack, TStatCustomData> affectedStatData = _statDatas[statIndex];

                statModifierHandle = new StatModifierHandle
                {
                    StatIndex = statIndex,
                };
                
                // Add modifier to the affected stat
                uint modifierIdCounter = _modifierIDCounter.Value;
                modifier.ID = ++modifierIdCounter;
                _modifierIDCounter.Value = modifierIdCounter;
                statModifierHandle.ModifierID = modifier.ID;
                PoolList<TStatModifier>.Add(ref affectedStatData.Modifiers, ref _statModifiers, ref _statModifierFreeRanges, modifier);
                _statDatas[statIndex] = affectedStatData;
                
                // Register observers on observed stats
                _tmpStatsList.Clear();
                modifier.AddObservedStatsToList(ref _tmpStatsList);
                for (int i = 0; i < _tmpStatsList.Length; i++)
                {
                    int observedStatIndex = _tmpStatsList[i];
                    StatData<TStatModifier, TStatModifierStack, TStatCustomData> observedStatData = _statDatas[observedStatIndex];
                    PoolList<int>.Add(ref observedStatData.Observers, ref _statObservers, ref _statObserverFreeRanges, statIndex);
                    _statDatas[observedStatIndex] = observedStatData;
                }
                
                // Stat update
                UpdateStat(statIndex);
            }

            statModifierHandle = default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetStatModifier(StatModifierHandle modifierHandle, out TStatModifier statModifier)
        {
            Stat stat = _stats[modifierHandle.StatIndex];
            if (stat.IsCreated)
            {
                StatData<TStatModifier, TStatModifierStack, TStatCustomData> statData = _statDatas[modifierHandle.StatIndex];

                for (int i = 0; i < statData.Modifiers.Length; i++)
                {
                    TStatModifier modifier = PoolList<TStatModifier>.GetElement(in statData.Modifiers, ref _statModifiers, i);
                    if (modifier.ID == modifierHandle.ModifierID)
                    {
                        statModifier = modifier;
                        return true;
                    }
                }
            }

            statModifier = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RemoveStatModifier(StatModifierHandle modifierHandle)
        {
            Stat stat = _stats[modifierHandle.StatIndex];
            if (stat.IsCreated)
            {
                StatData<TStatModifier, TStatModifierStack, TStatCustomData> statData = _statDatas[modifierHandle.StatIndex];
                
                for (int i = 0; i < statData.Modifiers.Length; i++)
                {
                    TStatModifier modifier = PoolList<TStatModifier>.GetElement(in statData.Modifiers, ref _statModifiers, i);
                    if (modifier.ID == modifierHandle.ModifierID)
                    {
                        // Unregister observers
                        _tmpStatsList.Clear();
                        modifier.AddObservedStatsToList(ref _tmpStatsList);
                        for (int a = 0; a < _tmpStatsList.Length; a++)
                        {
                            int observerToRemove = _tmpStatsList[a];
                            for (int b = 0; b < statData.Observers.Length; b++)
                            {
                                int existingObserver = PoolList<int>.GetElement(in statData.Observers, ref _statObservers, b);
                                if (existingObserver == observerToRemove)
                                {
                                    PoolList<int>.RemoveAtSwapBack(ref statData.Observers, ref _statObservers, _tmpStatsList[b]);
                                    break;
                                }
                            }
                        }
                        
                        // Remove modifier
                        PoolList<TStatModifier>.RemoveAtSwapBack(ref statData.Modifiers, ref _statModifiers, i);
                
                        _statDatas[modifierHandle.StatIndex] = statData;
                
                        // Stat update
                        UpdateStat(modifierHandle.StatIndex);
                        
                        return true;
                    }
                }
            }

            return false;
        }
    }

    [InternalBufferCapacity(0)]
    public struct Stat : IBufferElementData
    {
        internal byte _isCreated;
        internal byte _produceChangeEvents;
        public float BaseValue;
        public float Value;
        
        public bool IsCreated => _isCreated != 0;
    }

    [InternalBufferCapacity(0)]
    public struct StatData<TStatModifier, TStatModifierStack, TStatCustomData> : IBufferElementData
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
        where TStatModifierStack : unmanaged, IStatsModifierStack
        where TStatCustomData : unmanaged
    {
        public TStatCustomData CustomData; 
        public PoolList<TStatModifier> Modifiers;
        public PoolList<int> Observers;
    }

    [InternalBufferCapacity(0)]
    public struct StatModifierFreeRange : IBufferElementData
    {
        public IndexRange Range;
    }

    [InternalBufferCapacity(0)]
    public struct StatObserverFreeRange : IBufferElementData
    {
        public IndexRange Range;
    }

    [InternalBufferCapacity(0)]
    public struct StatChangeEvent : IBufferElementData
    {
        public int StatIndex;
        public Stat PrevValue;
        public Stat NewValue;
    }
    
    public struct StatModifierHandle : IEquatable<StatModifierHandle>
    {
        public int StatIndex;
        public uint ModifierID;
        
        public bool Equals(StatModifierHandle other)
        {
            return StatIndex == other.StatIndex && ModifierID == other.ModifierID;
        }

        public override bool Equals(object obj)
        {
            return obj is StatModifierHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StatIndex, ModifierID);
        }
    }
}