using System.Runtime.CompilerServices;
using Trove.ObjectHandles;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Logging;

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
    public UnsafeVirtualList<StatModifier> Modifiers;
    public UnsafeVirtualList<StatObserver> Observers;
    public VirtualListHandle<DirtyStat> DirtyStatsList;

    public static Stat Create<V>(
        ref V voView,
        int valueIndex, 
        float baseValue,
        int modifiersInitialCapacity,
        int observersInitialCapacity,
        VirtualListHandle<DirtyStat> dirtyStatsList)
        where V : unmanaged, IVirtualObjectView
    {
        Stat newStat = new Stat
        {
            BaseValue = baseValue,
            Value = baseValue,
            ValueIndex = valueIndex,
            Modifiers = UnsafeVirtualList<StatModifier>.Allocate(ref voView, modifiersInitialCapacity),
            Observers = UnsafeVirtualList<StatObserver>.Allocate(ref voView, observersInitialCapacity),
            DirtyStatsList = dirtyStatsList,
        };

        // Add to dirty stats list
        dirtyStatsList.TryAdd(ref voView, new DirtyStat(newStat));

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Apply(ref Stat stat)
        {
            stat.Value = stat.BaseValue;
            stat.Value += Add;
            stat.Value *= AddMultiply;
        }
    }

    public ModifierType Type;
    public StatReference StatA;
    public StatReference StatB;
    public float ValueA;
    public float ValueB;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool OnAdded<V>(
        ref V voView, 
        StatReference statReference)
        where V : unmanaged, IVirtualObjectView
    {
        switch (Type)
        {
            case (ModifierType.AddFromStat):
            case (ModifierType.AddMultiplyFromStat):
                AddAsObserverOf(ref voView, StatA, statReference);
                return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Apply<V>(
        ref V voView, 
        ref Stack stack)
        where V : unmanaged, IVirtualObjectView
    {
        switch (Type)
        {
            case (ModifierType.Add):
                {
                    stack.Add += ValueA;
                    break;
                }
            case (ModifierType.AddFromStat):
                {
                    if (StatUtility.TryResolveStat(ref voView, StatA, out Stat otherStat))
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
                    if (StatUtility.TryResolveStat(ref voView, StatA, out Stat otherStat))
                    {
                        stack.AddMultiply += otherStat.Value;
                    }
                    break;
                }
        }
    }

    private static bool AddAsObserverOf<V>(
        ref V voView,
        StatReference otherStatReference,
        StatReference selfStatReference)
        where V : unmanaged, IVirtualObjectView
    {
        ref Stat otherStat = ref StatUtility.TryResolveStatRef(ref voView, otherStatReference, out bool success);
        if (success)
        {
            if (otherStat.Observers.TryAdd(ref voView, new StatObserver(selfStatReference)))
            {
                return true;
            }
        }
        return false;
    }
}

public static class StatUtility
{
    public static bool TryAddModifier<V>(
        ref V voView,
        StatReference statReference,
        StatModifier modifier,
        bool autoRecompute = true)
        where V : unmanaged, IVirtualObjectView
    {
        ref Stat stat = ref TryResolveStatRef(ref voView, statReference, out bool success);
        if (success &&
            stat.Modifiers.TryAdd(ref voView, modifier) &&
            modifier.OnAdded(ref voView, statReference))
        {
            if (autoRecompute)
            {
                RecomputeStatAndDependencies(ref voView, ref stat);
            }
            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryAddBaseValue<V>(
        ref V voView,
        StatReference statReference,
        float value,
        bool autoRecompute = true)
        where V : unmanaged, IVirtualObjectView
    {
        ref Stat stat = ref TryResolveStatRef(ref voView, statReference, out bool success);
        if (success)
        {
            stat.BaseValue += value;
            if (autoRecompute)
            {
                RecomputeStatAndDependencies(ref voView, ref stat);
            }
            return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecomputeStatAndDependencies<V>(
        ref V voView,
        ref Stat stat)
        where V : unmanaged, IVirtualObjectView
    {
        if (stat.Modifiers.TryAsUnsafeArrayView(ref voView, out UnsafeArrayView<StatModifier> statModifiers) &&
            stat.Observers.TryAsUnsafeArrayView(ref voView, out UnsafeArrayView<StatObserver> statObservers))
        {
            // Modifiers
            {
                StatModifier.Stack modifierStack = StatModifier.Stack.New();
                for (int i = 0; i < statModifiers.Length; i++)
                {
                    StatModifier modifier = statModifiers[i];
                    modifier.Apply(ref voView, ref modifierStack);
                }
                modifierStack.Apply(ref stat);
            }

            // Add to dirty stats list
            stat.DirtyStatsList.TryAdd(ref voView, new DirtyStat(stat));

            // Observers
            for (int i = statObservers.Length - 1; i >= 0; i--)
            {
                StatObserver observer = statObservers[i];
                ref Stat observerStat = ref TryResolveStatRef(
                    ref voView,
                    observer.StatReference,
                    out bool success);
                if (success)
                {
                    RecomputeStatAndDependencies(
                        ref voView,
                        ref observerStat);
                }
                else
                {
                    // If could not resolve, remove from observers
                    stat.Observers.TryRemoveAt(ref voView, i);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryResolveStat<V>(
        ref V voView,
        StatReference statReference,
        out Stat stat)
        where V : unmanaged, IVirtualObjectView
    {
        if (VirtualObjectManager.TryGetObjectValue(ref voView, statReference.Handle, out stat))
        {
            return true;
        }
        stat = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe static ref Stat TryResolveStatRef<V>(
        ref V voView,
        StatReference statReference,
        out bool success)
        where V : unmanaged, IVirtualObjectView
    {
        return ref VirtualObjectManager.TryGetObjectValueRef(ref voView, statReference.Handle, out success);
    }

    public static bool TransferDirtyStatsToStatValues<V>(
        ref V voView,
        VirtualListHandle<DirtyStat> dirtyStatsList,
        ref DynamicBuffer<StatValueRO> statValuesBuffer)
        where V : unmanaged, IVirtualObjectView
    {
        bool success = true;
        if (dirtyStatsList.TryAsUnsafeArrayView(ref voView, out UnsafeArrayView<DirtyStat> dirtyStats))
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
                dirtyStatsList.TryClear(ref voView);
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
