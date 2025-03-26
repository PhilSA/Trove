using System.Runtime.CompilerServices;
using Trove.EventSystems;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Trove.Stats
{
    public struct StatEvent<TStatModifier, TStatModifierStack> : IBufferElementData
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public enum StatEventType
        {
            Recompute,
            AddBaseValue,
            SetBaseValue,
            AddModifier,
            RemoveModifier,
        }

        internal StatEventType EventType;
        internal StatHandle StatHandle;
        internal ModifierHandle ModifierHandle;
        internal float Value;
        internal TStatModifier Modifier;
        internal Entity CallbackEntity;
        internal bool RecomputeImmediate;

        public static StatEvent<TStatModifier, TStatModifierStack> Recompute(StatHandle statHandle, bool recomputeImmediate)
        {
            return new StatEvent<TStatModifier, TStatModifierStack>
            {
                EventType = StatEventType.Recompute,
                StatHandle = statHandle,
                RecomputeImmediate = recomputeImmediate,
            };
        }

        public static StatEvent<TStatModifier, TStatModifierStack> AddBaseValue(StatHandle statHandle, float value, bool recomputeImmediate)
        {
            return new StatEvent<TStatModifier, TStatModifierStack>
            {
                EventType = StatEventType.AddBaseValue,
                StatHandle = statHandle,
                Value = value,
                RecomputeImmediate = recomputeImmediate,
            };
        }

        public static StatEvent<TStatModifier, TStatModifierStack> SetBaseValue(StatHandle statHandle, float value, bool recomputeImmediate)
        {
            return new StatEvent<TStatModifier, TStatModifierStack>
            {
                EventType = StatEventType.SetBaseValue,
                StatHandle = statHandle,
                Value = value,
                RecomputeImmediate = recomputeImmediate,
            };
        }

        public static StatEvent<TStatModifier, TStatModifierStack> AddModifier(StatHandle statHandle, TStatModifier modifier, bool recomputeImmediate, Entity callbackEntity = default)
        {
            return new StatEvent<TStatModifier, TStatModifierStack>
            {
                EventType = StatEventType.AddModifier,
                StatHandle = statHandle,
                Modifier = modifier,
                CallbackEntity = callbackEntity,
                RecomputeImmediate = recomputeImmediate,
            };
        }

        public static StatEvent<TStatModifier, TStatModifierStack> RemoveModifier(StatHandle statHandle, ModifierHandle modifierHandle, bool recomputeImmediate)
        {
            return new StatEvent<TStatModifier, TStatModifierStack>
            {
                EventType = StatEventType.RemoveModifier,
                StatHandle = statHandle,
                ModifierHandle = modifierHandle,
                RecomputeImmediate = recomputeImmediate,
            };
        }
    }

    public struct GlobalStatEventsSingleton<TStatModifier, TStatModifierStack> : IComponentData, IGlobalEventsSingleton<StatEvent<TStatModifier, TStatModifierStack>>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public QueueEventsManager<StatEvent<TStatModifier, TStatModifierStack>> QueueEventsManager { get; set; }
        public StreamEventsManager StreamEventsManager { get; set; }
        public NativeList<StatEvent<TStatModifier, TStatModifierStack>> EventsList { get; set; }
    }

    public struct AddModifierEventCallback : IBufferElementData
    {
        public ModifierHandle ModifierHandle;
    }
}