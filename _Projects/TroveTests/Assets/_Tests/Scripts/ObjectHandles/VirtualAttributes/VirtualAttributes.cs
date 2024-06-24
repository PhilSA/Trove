using Trove.ObjectHandles;
using Unity.Burst;
using Unity.Entities;

public struct DirtyStat : IBufferElementData
{
    public int ValueIndex;
    public StatValueRO Value;

    public DirtyStat(Stat stat)
    {
        ValueIndex = stat.ValueIndex;
        Value = new StatValueRO(stat);
    }
}

public struct StatValueRO : IBufferElementData
{
    public float BaseValue;
    public float Value;

    public StatValueRO(Stat stat)
    {
        BaseValue = stat.BaseValue;
        Value = stat.Value;
    }
}

public struct Stat
{
    public float BaseValue;
    public float Value;
    public int ValueIndex;
    public VirtualListHandle<StatModifier> Modifiers;
    public VirtualListHandle<StatObserver> Observers;
    public VirtualListHandle<DirtyStat> DirtyStatsList;

    public static Stat Create<B>(int valueIndex, float baseValue, VirtualListHandle<DirtyStat> dirtyStatsList, ref DynamicBuffer<B> statBuffer)
        where B : unmanaged, IBufferElementData
    {
        Stat newStat = new Stat
        {
            BaseValue = baseValue,
            Value = baseValue,
            ValueIndex = valueIndex,
            Modifiers = VirtualList<StatModifier>.Allocate(ref statBuffer, 10),
            Observers = VirtualList<StatObserver>.Allocate(ref statBuffer, 10),
            DirtyStatsList = dirtyStatsList,
        };

        // Add to dirty stats list
        dirtyStatsList.TryAdd(ref statBuffer, new DirtyStat(newStat));

        return newStat;
    }
}

public struct StatReference
{
    public Entity BufferEntity;
    public VirtualObjectHandle<Stat> Handle;

    public StatReference(Entity bufferEntity, VirtualObjectHandle<Stat> handle)
    {
        BufferEntity = bufferEntity;
        Handle = handle;
    }
}

public struct StatObserver
{
    public StatReference StatReference;

    public StatObserver(StatReference statReference)
    {
        StatReference = statReference;
    }
}

public struct StatModifier
{
    public enum ModifierType
    {
        Add,
        AddFromStat,
        AddMultiply,
        AddMultiplyFromStat,
    }

    public struct Stack
    {
        public float Add;
        public float AddMultiply;

        public static Stack New()
        {
            return new Stack
            {
                Add = 0f,
                AddMultiply = 1f,
            };
        }

        public void Apply(ref Stat stat)
        {
            stat.Value = stat.BaseValue;
            stat.Value += Add;
            stat.Value += (AddMultiply * stat.Value);
        }
    }

    public ModifierType Type;
    public StatReference StatA;
    public StatReference StatB;
    public float ValueA;
    public float ValueB;

    public bool OnAdded<B>(StatReference prevStatReference, ref DynamicBuffer<B> prevStatBuffer, ref BufferLookup<B> voBufferLookup)
        where B : unmanaged, IBufferElementData
    {
        switch (Type)
        {
            case (ModifierType.AddFromStat):
            case (ModifierType.AddMultiplyFromStat):
                AddAsObserverOf(StatA, prevStatReference, ref prevStatBuffer, ref voBufferLookup);
                return true;
        }

        return false;
    }

    public void Apply<B>(Entity prevBufferEntity, ref DynamicBuffer<B> prevStatBuffer, ref BufferLookup<B> voBufferLookup, ref Stack stack)
        where B : unmanaged, IBufferElementData
    {
        switch(Type)
        {
            case (ModifierType.Add):
                {
                    stack.Add += ValueA;
                    break;
                }
            case (ModifierType.AddFromStat):
                {
                    if(StatUtility.TryResolveStat(StatA, ref voBufferLookup, prevBufferEntity, ref prevStatBuffer, out Stat otherStat, out _))
                    {
                        stack.Add += otherStat.Value;
                    }
                    break;
                }
            case (ModifierType.AddMultiply):
                {
                    stack.AddMultiply += ValueA;
                    break;
                }
            case (ModifierType.AddMultiplyFromStat):
                {
                    if (StatUtility.TryResolveStat(StatA, ref voBufferLookup, prevBufferEntity, ref prevStatBuffer, out Stat otherStat, out _))
                    {
                        stack.AddMultiply += otherStat.Value;
                    }
                    break;
                }
        }
    }

    private static bool AddAsObserverOf<B>(
        StatReference otherStatReference, 
        StatReference prevStatReference, 
        ref DynamicBuffer<B> prevStatBuffer,
        ref BufferLookup<B> voBufferLookup)
        where B : unmanaged, IBufferElementData
    {
        ref Stat otherStat = ref StatUtility.TryResolveStatRef(
            otherStatReference, 
            ref voBufferLookup, 
            prevStatReference.BufferEntity,
            ref prevStatBuffer,
            out DynamicBuffer<B> otherStatBuffer, 
            out bool success);
        if (success)
        {
            if (otherStat.Observers.TryAdd(ref otherStatBuffer, new StatObserver(prevStatReference)))
            {
                return true;
            }
        }
        return false;
    }
}

public static class StatUtility
{
    public static bool TryAddModifier<B>(
        StatReference statReference,
        StatModifier modifier,
        ref BufferLookup<B> voBufferLookup,
        bool autoRecompute = true)
        where B : unmanaged, IBufferElementData
    {
        ref Stat stat = ref TryResolveStatRef(statReference, ref voBufferLookup, out DynamicBuffer<B> statBuffer, out bool success);
        if (success)
        {
            if (stat.Modifiers.TryAdd(ref statBuffer, modifier))
            {
                if(modifier.OnAdded(statReference, ref statBuffer, ref voBufferLookup))
                {
                    if (autoRecompute)
                    {
                        RecomputeStatAndDependencies(
                            ref stat,
                            statReference.BufferEntity,
                            ref statBuffer,
                            ref voBufferLookup);
                    }
                    return true;
                }
            }
        }
        return false;
    }

    public static bool TryAddBaseValue<B>(
        StatReference statReference,
        float value,
        ref BufferLookup<B> voBufferLookup,
        bool autoRecompute = true)
        where B : unmanaged, IBufferElementData
    {
        ref Stat stat = ref TryResolveStatRef(statReference, ref voBufferLookup, out DynamicBuffer<B> statBuffer, out bool success);
        if(success)
        {
            stat.BaseValue += value;
            if (autoRecompute)
            {
                RecomputeStatAndDependencies(
                    ref stat,
                    statReference.BufferEntity,
                    ref statBuffer,
                    ref voBufferLookup);
            }
            return true;
        }
        return false;
    }

    public static void RecomputeStatAndDependencies<B>(
        ref Stat stat,
        Entity statBufferEntity,
        ref DynamicBuffer<B> statBuffer,
        ref BufferLookup<B> voBufferLookup)
        where B : unmanaged, IBufferElementData
    {
        if(stat.Modifiers.TryAsUnsafeVirtualArray(ref statBuffer, out UnsafeVirtualArray<StatModifier> statModifiers) &&
            stat.Observers.TryAsUnsafeVirtualArray(ref statBuffer, out UnsafeVirtualArray<StatObserver> statObservers))
        {
            // Modifiers
            {
                StatModifier.Stack modifierStack = StatModifier.Stack.New();
                for (int i = 0; i < statModifiers.Length; i++)
                {
                    StatModifier modifier = statModifiers[i];
                    modifier.Apply(statBufferEntity, ref statBuffer, ref voBufferLookup, ref modifierStack);
                }
                modifierStack.Apply(ref stat);
            }

            // Add to dirty stats list
            stat.DirtyStatsList.TryAdd(ref statBuffer, new DirtyStat(stat));

            // Observers
            for (int i = statObservers.Length - 1; i >= 0; i--)
            {
                StatObserver observer = statObservers[i];
                ref Stat observerStat = ref TryResolveStatRef(
                    observer.StatReference,
                    ref voBufferLookup,
                    statBufferEntity,
                    ref statBuffer,
                    out DynamicBuffer<B> observerStatBuffer,
                    out bool success);
                if (success)
                {
                    RecomputeStatAndDependencies(
                        ref observerStat,
                        observer.StatReference.BufferEntity,
                        ref observerStatBuffer,
                        ref voBufferLookup);
                }
                else
                {
                    // If could not resolve, remove from observers
                    stat.Observers.TryRemoveAt(ref statBuffer, i);
                }
            }
        }
    }

    public static bool TryResolveStat<B>(
        StatReference statReference,
        ref BufferLookup<B> voBufferLookup,
        out Stat stat,
        out DynamicBuffer<B> statBuffer)
        where B : unmanaged, IBufferElementData
    {
        if (voBufferLookup.TryGetBuffer(statReference.BufferEntity, out DynamicBuffer<B> voBuffer))
        {
            statBuffer = voBuffer;
            if (VirtualObjectManager.TryGetObjectValue(ref statBuffer, statReference.Handle, out stat))
            {
                return true;
            }
        }
        statBuffer = default;
        stat = default;
        return false;
    }

    public static bool TryResolveStat<B>(
        StatReference statReference,
        ref BufferLookup<B> voBufferLookup,
        Entity prevBufferEntity,
        ref DynamicBuffer<B> prevStatBuffer,
        out Stat stat,
        out DynamicBuffer<B> statBuffer)
        where B : unmanaged, IBufferElementData
    {
        statBuffer = prevStatBuffer;
        if (prevBufferEntity != statReference.BufferEntity)
        {
            if (voBufferLookup.TryGetBuffer(statReference.BufferEntity, out DynamicBuffer<B> voBuffer))
            {
                statBuffer = voBuffer;
            }
            else
            {
                statBuffer = default;
            }
        }
        if(VirtualObjectManager.TryGetObjectValue(ref statBuffer, statReference.Handle, out stat))
        {
            return true;
        }
        statBuffer = default;
        stat = default;
        return false;
    }

    public unsafe static ref Stat TryResolveStatRef<B>(
        StatReference statReference,
        ref BufferLookup<B> voBufferLookup,
        out DynamicBuffer<B> statBuffer,
        out bool success)
        where B : unmanaged, IBufferElementData
    {
        if (voBufferLookup.TryGetBuffer(statReference.BufferEntity, out DynamicBuffer<B> voBuffer))
        {
            statBuffer = voBuffer;
            return ref VirtualObjectManager.TryGetObjectValueRef(ref statBuffer, statReference.Handle, out success);
        }
        statBuffer = default;
        success = false;
        return ref *(Stat*)default; 
    }

    public unsafe static ref Stat TryResolveStatRef<B>(
        StatReference statReference,
        ref BufferLookup<B> voBufferLookup,
        Entity prevBufferEntity,
        ref DynamicBuffer<B> prevStatBuffer,
        out DynamicBuffer<B> statBuffer,
        out bool success)
        where B : unmanaged, IBufferElementData
    {
        statBuffer = prevStatBuffer;
        if (prevBufferEntity != statReference.BufferEntity)
        {
            if (voBufferLookup.TryGetBuffer(statReference.BufferEntity, out DynamicBuffer<B> voBuffer))
            {
                statBuffer = voBuffer;
                return ref VirtualObjectManager.TryGetObjectValueRef(ref statBuffer, statReference.Handle, out success);
            }
            else
            {
                statBuffer = default;
            }
        }
        statBuffer = default;
        success = false;
        return ref *(Stat*)prevStatBuffer.GetUnsafePtr();
    }

    public static bool TransferDirtyStatsToStatValues<B>(
        VirtualListHandle<DirtyStat> dirtyStatsList, 
        ref DynamicBuffer<B> statBuffer, 
        ref DynamicBuffer<StatValueRO> statValuesBuffer)
        where B : unmanaged, IBufferElementData
    {
        bool success = true;
        if (dirtyStatsList.TryAsUnsafeVirtualArray(ref statBuffer, out UnsafeVirtualArray<DirtyStat> dirtyStats))
        {
            for (int i = 0; i < dirtyStats.Length; i++)
            {
                DirtyStat dirtyStat = dirtyStats[i];
                if (dirtyStat.ValueIndex < statValuesBuffer.Length)
                {
                    statValuesBuffer[dirtyStat.ValueIndex] = dirtyStat.Value;
                }
                else
                {
                    success = false;
                    // TODO: error handling?
                }
            }

            if (dirtyStats.Length > 0)
            {
                dirtyStatsList.TryClear(ref statBuffer);
            }
        }
        else
        {
            success = false;
            // TODO: error handling?
        }
        return success;
    }
}
