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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExecuteGlobal(
            ref ComponentLookup<StatOwner> statOwnerLookup,
            ref ComponentLookup<DirtyStatsMask> dirtyStatsMaskLookup,
            ref BufferLookup<Stat> statsBufferLookup,
            ref BufferLookup<TStatModifier> statModifiersBufferLookup,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref BufferLookup<AddModifierEventCallback> AddModifierEventCallbackBufferLookup,
            ref NativeQueue<StatHandle> recomputeImmediateStatsQueue,
            ref UnsafeList<StatHandle> tmpObservedStatsList)
        {
            switch (EventType)
            {
                case StatEventType.Recompute:
                    {
                        HandleRecompute(
                            ref dirtyStatsMask,
                            dirtyStatsMaskEnabledRefRW,
                            ref statsBuffer,
                            ref statModifiersBuffer,
                            ref statObserversBuffer,
                            ref statsBufferLookup,
                            ref statModifiersBufferLookup,
                            ref statObserversBufferLookup,
                            ref recomputeImmediateStatsQueue);

                        break;
                    }
                case StatEventType.AddBaseValue:
                    {

                        HandleRecompute(
                            ref dirtyStatsMask,
                            dirtyStatsMaskEnabledRefRW,
                            ref statsBuffer,
                            ref statModifiersBuffer,
                            ref statObserversBuffer,
                            ref statsBufferLookup,
                            ref statModifiersBufferLookup,
                            ref statObserversBufferLookup,
                            ref recomputeImmediateStatsQueue);

                        break;
                    }
                case StatEventType.SetBaseValue:
                    {


                        HandleRecompute(
                            ref dirtyStatsMask,
                            dirtyStatsMaskEnabledRefRW,
                            ref statsBuffer,
                            ref statModifiersBuffer,
                            ref statObserversBuffer,
                            ref statsBufferLookup,
                            ref statModifiersBufferLookup,
                            ref statObserversBufferLookup,
                            ref recomputeImmediateStatsQueue);

                        break;
                    }
                case StatEventType.AddModifier:
                    {
                        ModifierHandle addedModifierHandle = StatUtilities.AddModifier<TStatModifier, TStatModifierStack>(
                            StatHandle,
                            Modifier,
                            ref statOwnerLookup,
                            ref statModifiersBufferLookup,
                            ref statObserversBufferLookup,
                            ref dirtyStatsMaskLookup,
                            ref tmpObservedStatsList);

                        HandleRecompute(
                            ref dirtyStatsMask,
                            dirtyStatsMaskEnabledRefRW,
                            ref statsBuffer,
                            ref statModifiersBuffer,
                            ref statObserversBuffer,
                            ref statsBufferLookup,
                            ref statModifiersBufferLookup,
                            ref statObserversBufferLookup,
                            ref recomputeImmediateStatsQueue);

                        if (CallbackEntity != Entity.Null &&
                            AddModifierEventCallbackBufferLookup.TryGetBuffer(CallbackEntity, out DynamicBuffer<AddModifierEventCallback> callbackBuffer))
                        {
                            callbackBuffer.Add(new AddModifierEventCallback
                            {
                                ModifierHandle = addedModifierHandle,
                            });
                        }

                        break;
                    }
                case StatEventType.RemoveModifier:
                    {
                        StatUtilities.RemoveModifier<TStatModifier, TStatModifierStack>(
                            StatHandle,
                            ModifierHandle,
                            ref statOwnerLookup,
                            ref statModifiersBufferLookup,
                            ref statObserversBufferLookup,
                            ref dirtyStatsMaskLookup,
                            ref tmpObservedStatsList);

                        HandleRecompute(
                            ref dirtyStatsMask,
                            dirtyStatsMaskEnabledRefRW,
                            ref statsBuffer,
                            ref statModifiersBuffer,
                            ref statObserversBuffer,
                            ref statsBufferLookup,
                            ref statModifiersBufferLookup,
                            ref statObserversBufferLookup,
                            ref recomputeImmediateStatsQueue);

                        break;
                    }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExecuteEntity(
            Entity entity,
            ref DirtyStatsMask dirtyStatsMask,
            EnabledRefRW<DirtyStatsMask> dirtyStatsMaskEnabledRefRW,
            ref DynamicBuffer<Stat> statsBuffer,
            ref DynamicBuffer<TStatModifier> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer,
            ref ComponentLookup<StatOwner> statOwnerLookup,
            ref ComponentLookup<DirtyStatsMask> dirtyStatsMaskLookup,
            ref BufferLookup<Stat> statsBufferLookup,
            ref BufferLookup<TStatModifier> statModifiersBufferLookup,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref BufferLookup<AddModifierEventCallback> AddModifierEventCallbackBufferLookup,
            ref NativeQueue<StatHandle> recomputeImmediateStatsQueue,
            ref UnsafeList<StatHandle> tmpObservedStatsList)
        {
            switch (EventType)
            {
                case StatEventType.Recompute:
                    {
                        HandleRecompute(
                            ref dirtyStatsMask,
                            dirtyStatsMaskEnabledRefRW,
                            ref statsBuffer,
                            ref statModifiersBuffer,
                            ref statObserversBuffer,
                            ref statsBufferLookup,
                            ref statModifiersBufferLookup,
                            ref statObserversBufferLookup,
                            ref recomputeImmediateStatsQueue);

                        break;
                    }
                case StatEventType.AddBaseValue:
                    {

                        HandleRecompute(
                            ref dirtyStatsMask,
                            dirtyStatsMaskEnabledRefRW,
                            ref statsBuffer,
                            ref statModifiersBuffer,
                            ref statObserversBuffer,
                            ref statsBufferLookup,
                            ref statModifiersBufferLookup,
                            ref statObserversBufferLookup,
                            ref recomputeImmediateStatsQueue);

                        break;
                    }
                case StatEventType.SetBaseValue:
                    {


                        HandleRecompute(
                            ref dirtyStatsMask,
                            dirtyStatsMaskEnabledRefRW,
                            ref statsBuffer,
                            ref statModifiersBuffer,
                            ref statObserversBuffer,
                            ref statsBufferLookup,
                            ref statModifiersBufferLookup,
                            ref statObserversBufferLookup,
                            ref recomputeImmediateStatsQueue);

                        break;
                    }
                case StatEventType.AddModifier:
                    {
                        ModifierHandle addedModifierHandle = StatUtilities.AddModifier<TStatModifier, TStatModifierStack>(
                            StatHandle,
                            Modifier,
                            ref statOwnerLookup,
                            ref statModifiersBufferLookup,
                            ref statObserversBufferLookup,
                            ref dirtyStatsMaskLookup,
                            ref tmpObservedStatsList);

                        HandleRecompute(
                            ref dirtyStatsMask,
                            dirtyStatsMaskEnabledRefRW,
                            ref statsBuffer,
                            ref statModifiersBuffer,
                            ref statObserversBuffer,
                            ref statsBufferLookup,
                            ref statModifiersBufferLookup,
                            ref statObserversBufferLookup,
                            ref recomputeImmediateStatsQueue);

                        if (CallbackEntity != Entity.Null &&
                            AddModifierEventCallbackBufferLookup.TryGetBuffer(CallbackEntity, out DynamicBuffer<AddModifierEventCallback> callbackBuffer))
                        {
                            callbackBuffer.Add(new AddModifierEventCallback
                            {
                                ModifierHandle = addedModifierHandle,
                            });
                        }

                        break;
                    }
                case StatEventType.RemoveModifier:
                    {
                        StatUtilities.RemoveModifier<TStatModifier, TStatModifierStack>(
                            StatHandle,
                            ModifierHandle,
                            ref statOwnerLookup,
                            ref statModifiersBufferLookup,
                            ref statObserversBufferLookup,
                            ref dirtyStatsMaskLookup,
                            ref tmpObservedStatsList);

                        HandleRecompute(
                            ref dirtyStatsMask,
                            dirtyStatsMaskEnabledRefRW,
                            ref statsBuffer,
                            ref statModifiersBuffer,
                            ref statObserversBuffer,
                            ref statsBufferLookup,
                            ref statModifiersBufferLookup,
                            ref statObserversBufferLookup,
                            ref recomputeImmediateStatsQueue);

                        break;
                    }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandleRecomputeGlobal(
            ref BufferLookup<Stat> statsBufferLookup,
            ref BufferLookup<TStatModifier> statModifiersBufferLookup,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref NativeQueue<StatHandle> recomputeImmediateStatsQueue)
        {
            if (RecomputeImmediate)
            {
                StatUtilities.RecomputeStatAndObserversImmediate<TStatModifier, TStatModifierStack>(
                    StatHandle,
                    ref statsBuffer,
                    ref statModifiersBuffer,
                    ref statObserversBuffer,
                    ref statsBufferLookup,
                    ref statModifiersBufferLookup,
                    ref statObserversBufferLookup,
                    ref recomputeImmediateStatsQueue);
            }
            else
            {
                StatUtilities.MarkStatForBatchRecompute(
                    StatHandle.Index,
                    ref dirtyStatsMask,
                    dirtyStatsMaskEnabledRefRW);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void HandleRecomputeEntity(
            Entity entity,
            ref DirtyStatsMask dirtyStatsMask,
            EnabledRefRW<DirtyStatsMask> dirtyStatsMaskEnabledRefRW,
            ref DynamicBuffer<Stat> statsBuffer,
            ref DynamicBuffer<TStatModifier> statModifiersBuffer,
            ref DynamicBuffer<StatObserver> statObserversBuffer,
            ref BufferLookup<Stat> statsBufferLookup,
            ref BufferLookup<TStatModifier> statModifiersBufferLookup,
            ref BufferLookup<StatObserver> statObserversBufferLookup,
            ref NativeQueue<StatHandle> recomputeImmediateStatsQueue)
        {
            if (RecomputeImmediate)
            {
                StatUtilities.RecomputeStatAndObserversImmediate<TStatModifier, TStatModifierStack>(
                    StatHandle,
                    ref statsBuffer,
                    ref statModifiersBuffer,
                    ref statObserversBuffer,
                    ref statsBufferLookup,
                    ref statModifiersBufferLookup,
                    ref statObserversBufferLookup,
                    ref recomputeImmediateStatsQueue);
            }
            else
            {
                StatUtilities.MarkStatForBatchRecompute(
                    StatHandle.Index,
                    ref dirtyStatsMask,
                    dirtyStatsMaskEnabledRefRW);
            }
        }
    }

    public struct StatEventsSingleton<TStatModifier, TStatModifierStack> : IComponentData, IGlobalEventsSingleton<StatEvent<TStatModifier, TStatModifierStack>>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public QueueEventsManager<StatEvent<TStatModifier, TStatModifierStack>> QueueEventsManager { get; set; }
        public StreamEventsManager StreamEventsManager { get; set; }
        public NativeList<StatEvent<TStatModifier, TStatModifierStack>> EventsList { get; set; }
    }

    public struct EntityStatEventsSingleton<TStatModifier, TStatModifierStack> : IComponentData, IEntityEventsSingleton<EntityStatEvent<TStatModifier, TStatModifierStack>>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public QueueEventsManager<EntityStatEvent<TStatModifier, TStatModifierStack>> QueueEventsManager { get; set; }
        public StreamEventsManager StreamEventsManager { get; set; }
    }

    public struct EntityStatEvent<TStatModifier, TStatModifierStack> : IEntityBufferEvent<StatEvent<TStatModifier, TStatModifierStack>>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>, IBufferElementData
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public Entity AffectedEntity { get; set; }
        public StatEvent<TStatModifier, TStatModifierStack> BufferElement { get; set; }
    }

    public struct HasEntityStatEvents : IComponentData, IEnableableComponent
    { }

    public struct AddModifierEventCallback : IBufferElementData
    {
        public ModifierHandle ModifierHandle;
    }
}