using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace Trove.Statemachines
{
    public static class StateMachineUtilities
    {
        private const float StatesBufferGrowFactor = 1.5f;
        
        public static void BakeStateMachineComponents<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            IBaker baker,
            Entity entity,
            out StateMachine stateMachine,
            out DynamicBuffer<TState> statesBuffer)
            where TState : unmanaged, IPoolElement, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            stateMachine = default;
            baker.AddComponent(entity, stateMachine);
            statesBuffer = baker.AddBuffer<TState>(entity);
        }

        public static void CreateStateMachineComponents<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            EntityManager entityManager,
            Entity entity)
            where TState : unmanaged, IPoolElement, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            entityManager.AddComponentData(entity, new StateMachine());
            entityManager.AddBuffer<TState>(entity);
        }

        public static void CreateStateMachineComponents<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            EntityCommandBuffer ecb,
            Entity entity)
            where TState : unmanaged, IPoolElement, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            ecb.AddComponent(entity, new StateMachine());
            ecb.AddBuffer<TState>(entity);
        }

        public static void CreateStateMachineComponents<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            Entity entity)
            where TState : unmanaged, IPoolElement, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            ecb.AddComponent(sortKey, entity, new StateMachine());
            ecb.AddBuffer<TState>(sortKey, entity);
        }
        
        public static void InitStateMachine<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            ref StateMachine stateMachine,
            ref DynamicBuffer<TState> statesBuffer,
            int statesInitialCapacity)
            where TState : unmanaged, IPoolElement, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            stateMachine.CurrentStateHandle = default;
            Pool.Init(ref statesBuffer, statesInitialCapacity);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Update<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            ref StateMachine stateMachine,
            ref DynamicBuffer<TState> statesBuffer,
            ref TGlobalStateUpdateData globalStateUpdateData,
            ref TEntityStateUpdateData entityStateUpdateData)
            where TState : unmanaged, IPoolElement, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            TState nullState = default;

            // State machine initialization
            if (stateMachine.HasInitialized == 0)
            {
                // Transition to initial state
                if (!stateMachine.CurrentStateHandle.Handle.Exists() &&
                    stateMachine.InitialState.Handle.Exists())
                {
                    TryStateTransition(ref stateMachine, ref statesBuffer,
                        ref globalStateUpdateData,
                        ref entityStateUpdateData, stateMachine.InitialState);
                }

                stateMachine.HasInitialized = 1;
            }

            ref TState currentState = ref Pool.TryGetObjectRef(ref statesBuffer,
                stateMachine.CurrentStateHandle.Handle, out bool success, ref nullState);
            if (success)
            {
                currentState.Update(ref stateMachine, ref globalStateUpdateData, ref entityStateUpdateData);
            }
        }
        
        public static bool TryStateTransition<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            ref StateMachine stateMachine, 
            ref DynamicBuffer<TState> statesBuffer,
            ref TGlobalStateUpdateData globalStateUpdateData,
            ref TEntityStateUpdateData entityStateUpdateData,
            StateHandle newStateHandle)
            where TState : unmanaged, IPoolElement, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            TState nullState = default;

            ref TState newState = ref Pool.TryGetObjectRef(ref statesBuffer,
                newStateHandle.Handle, out bool success, ref nullState);
            if (success)
            {
                StateHandle prevStateHandle = stateMachine.CurrentStateHandle;
                ref TState prevState = ref Pool.TryGetObjectRef(ref statesBuffer,
                    prevStateHandle.Handle, out success, ref nullState);
                if (success)
                {
                    prevState.OnStateExit(ref stateMachine, ref globalStateUpdateData, ref entityStateUpdateData);
                }
                newState.OnStateEnter(ref stateMachine, ref globalStateUpdateData, ref entityStateUpdateData);
                stateMachine.CurrentStateHandle = newStateHandle;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetState<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            ref DynamicBuffer<TState> statesBuffer, 
            StateHandle stateHandle,
            out TState state)
            where TState : unmanaged, IPoolElement, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            return Pool.TryGetObject(ref statesBuffer, stateHandle.Handle, out state);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref TState TryGetStateRef<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            ref DynamicBuffer<TState> statesBuffer, 
            StateHandle stateHandle,
            out bool success,
            ref TState nullResult)
            where TState : unmanaged, IPoolElement, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            return ref Pool.TryGetObjectRef(ref statesBuffer, stateHandle.Handle, out success, ref nullResult);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySetState<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            ref DynamicBuffer<TState> statesBuffer, 
            StateHandle stateHandle,
            TState state)
            where TState : unmanaged, IPoolElement, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            return Pool.TrySetObject(ref statesBuffer, stateHandle.Handle, state);
        }

        public static void CreateState<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            ref DynamicBuffer<TState> statesBuffer, 
            TState state, 
            out StateHandle stateHandle)
            where TState : unmanaged, IPoolElement, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            Pool.AddElement(ref statesBuffer, state, out PoolElementHandle poolObjectHandle);
            stateHandle = new StateHandle
            {
                Handle = poolObjectHandle,
            };
        }

        public static bool TryDestroyState<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            ref DynamicBuffer<TState> statesBuffer,
            StateHandle stateHandle)
            where TState : unmanaged, IPoolElement, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            return Pool.TryRemoveObject(ref statesBuffer, stateHandle.Handle);
        }
    }
}