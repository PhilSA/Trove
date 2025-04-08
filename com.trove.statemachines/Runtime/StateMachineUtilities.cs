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
            out DynamicBuffer<StateVersion> stateVersionsBuffer,
            out DynamicBuffer<TState> statesBuffer)
            where TState : unmanaged, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            stateMachine = default;
            baker.AddComponent(entity, stateMachine);
            stateVersionsBuffer = baker.AddBuffer<StateVersion>(entity);
            statesBuffer = baker.AddBuffer<TState>(entity);
        }

        public static void CreateStateMachineComponents<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            EntityManager entityManager,
            Entity entity)
            where TState : unmanaged, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            entityManager.AddComponentData(entity, new StateMachine());
            entityManager.AddBuffer<StateVersion>(entity);
            entityManager.AddBuffer<TState>(entity);
        }

        public static void CreateStateMachineComponents<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            EntityCommandBuffer ecb,
            Entity entity)
            where TState : unmanaged, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            ecb.AddComponent(entity, new StateMachine());
            ecb.AddBuffer<StateVersion>(entity);
            ecb.AddBuffer<TState>(entity);
        }

        public static void CreateStateMachineComponents<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            EntityCommandBuffer.ParallelWriter ecb,
            int sortKey,
            Entity entity)
            where TState : unmanaged, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            ecb.AddComponent(sortKey, entity, new StateMachine());
            ecb.AddBuffer<StateVersion>(sortKey, entity);
            ecb.AddBuffer<TState>(sortKey, entity);
        }
        
        public static void InitStateMachine<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            ref StateMachine stateMachine,
            ref DynamicBuffer<StateVersion> stateVersionsBuffer,
            ref DynamicBuffer<TState> statesBuffer,
            int statesInitialCapacity)
            where TState : unmanaged, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            stateMachine.CurrentStateHandle = default;
            ResizeStatesBuffer<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(ref statesBuffer, ref stateVersionsBuffer, statesInitialCapacity);
        }

        /// <summary>
        /// Note: can only grow; not shrink
        /// </summary>
        public static void ResizeStatesBuffer<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            ref DynamicBuffer<TState> statesBuffer,
            ref DynamicBuffer<StateVersion> stateVersionsBuffer,
            int newSize)
            where TState : unmanaged, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            if (newSize > statesBuffer.Length)
            {
                statesBuffer.Resize(newSize, NativeArrayOptions.ClearMemory);
                stateVersionsBuffer.Resize(statesBuffer.Length, NativeArrayOptions.ClearMemory);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Update<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            ref StateMachine stateMachine,
            ref DynamicBuffer<StateVersion> stateVersionsBuffer,
            ref DynamicBuffer<TState> statesBuffer,
            ref TGlobalStateUpdateData globalStateUpdateData,
            ref TEntityStateUpdateData entityStateUpdateData)
            where TState : unmanaged, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            TState nullState = default;
            
            ref TState currentState = ref TryGetStateRef<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
                ref stateVersionsBuffer, ref statesBuffer, stateMachine.CurrentStateHandle, out bool success, ref nullState);
            if (success)
            {
                currentState.Update(ref globalStateUpdateData, ref entityStateUpdateData);
            }
        }
        
        public static bool TryStateTransition<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            ref StateMachine stateMachine, 
            ref DynamicBuffer<StateVersion> stateVersionsBuffer,
            ref DynamicBuffer<TState> statesBuffer,
            ref TGlobalStateUpdateData globalStateUpdateData,
            ref TEntityStateUpdateData entityStateUpdateData,
            StateHandle newStateHandle)
            where TState : unmanaged, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            TState nullState = default;
            
            ref TState newState = ref TryGetStateRef<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
                ref stateVersionsBuffer, ref statesBuffer, newStateHandle, out bool success, ref nullState);
            if (success)
            {
                StateHandle prevStateHandle = stateMachine.CurrentStateHandle;
                ref TState prevState = ref TryGetStateRef<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
                    ref stateVersionsBuffer, ref statesBuffer, prevStateHandle, out success, ref nullState);
                if (success)
                {
                    prevState.OnStateExit(ref globalStateUpdateData, ref entityStateUpdateData);
                }
                newState.OnStateEnter(ref globalStateUpdateData, ref entityStateUpdateData);
                stateMachine.CurrentStateHandle = newStateHandle;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetState<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            ref DynamicBuffer<StateVersion> stateVersionsBuffer,
            ref DynamicBuffer<TState> statesBuffer, 
            StateHandle stateHandle,
            out TState state)
            where TState : unmanaged, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            if (stateHandle.Exists() && stateHandle.Index < statesBuffer.Length)
            {
                StateVersion existingStateVersion = stateVersionsBuffer[stateHandle.Index];
                if (existingStateVersion.Version == stateHandle.Version)
                {
                    state = statesBuffer[stateHandle.Index];
                    return true;
                }
            }

            state = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ref TState TryGetStateRef<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            ref DynamicBuffer<StateVersion> stateVersionsBuffer,
            ref DynamicBuffer<TState> statesBuffer, 
            StateHandle stateHandle,
            out bool success,
            ref TState nullResult)
            where TState : unmanaged, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            if (stateHandle.Exists() && stateHandle.Index < statesBuffer.Length)
            {
                StateVersion existingStateVersion = stateVersionsBuffer[stateHandle.Index];
                if (existingStateVersion.Version == stateHandle.Version)
                {
                    ref TState state =
                        ref UnsafeUtility.ArrayElementAsRef<TState>(
                            statesBuffer.GetUnsafePtr(),
                            stateHandle.Index);
                    success = true;
                    return ref state;
                }
            }

            success = false;
            return ref nullResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrySetState<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            ref DynamicBuffer<StateVersion> stateVersionsBuffer,
            ref DynamicBuffer<TState> statesBuffer, 
            StateHandle stateHandle,
            TState state)
            where TState : unmanaged, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            if (stateHandle.Exists() && stateHandle.Index < statesBuffer.Length)
            {
                StateVersion existingStateVersion = stateVersionsBuffer[stateHandle.Index];
                if (existingStateVersion.Version == stateHandle.Version)
                {
                    existingStateVersion.Version = stateHandle.Version;
                    stateVersionsBuffer[stateHandle.Index] = existingStateVersion;
                    statesBuffer[stateHandle.Index] = state;
                    return true;
                }
            }

            return false;
        }

        public static void CreateState<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            ref DynamicBuffer<StateVersion> stateVersionsBuffer,
            ref DynamicBuffer<TState> statesBuffer, 
            TState state, 
            out StateHandle stateHandle)
            where TState : unmanaged, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            int addIndex = -1;
            for (int i = 0; i < statesBuffer.Length; i++)
            {
                StateVersion iteratedStateVersion = stateVersionsBuffer[i];
                if (!iteratedStateVersion.Exists())
                {
                    addIndex = i;
                    break;
                }
            }

            if (addIndex < 0)
            {
                addIndex = statesBuffer.Length;
                int newCapacity = math.max((int)math.ceil(statesBuffer.Length * StatesBufferGrowFactor),
                    statesBuffer.Length + 1);
                ResizeStatesBuffer<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(ref statesBuffer, ref stateVersionsBuffer, newCapacity);
            }

            StateVersion existingStateVersion = stateVersionsBuffer[addIndex];
            existingStateVersion.Version = -existingStateVersion.Version + 1; // flip version and increment
            stateVersionsBuffer[addIndex] = existingStateVersion;

            statesBuffer[addIndex] = state;
            
            stateHandle = new StateHandle
            {
                Index = addIndex,
                Version = existingStateVersion.Version,
            };
        }

        public static bool TryDestroyState<TState, TGlobalStateUpdateData, TEntityStateUpdateData>(
            ref DynamicBuffer<StateVersion> stateVersionsBuffer,
            StateHandle stateHandle)
            where TState : unmanaged, IState<TGlobalStateUpdateData, TEntityStateUpdateData>, IBufferElementData
            where TGlobalStateUpdateData : unmanaged 
            where TEntityStateUpdateData : unmanaged
        {
            if (stateHandle.Exists() && stateHandle.Index < stateVersionsBuffer.Length)
            {
                StateVersion existingStateVersion = stateVersionsBuffer[stateHandle.Index];
                if (existingStateVersion.Version == stateHandle.Version)
                {
                    existingStateVersion.Version = -existingStateVersion.Version; // flip version
                    stateVersionsBuffer[stateHandle.Index] = existingStateVersion;

                    return true;
                }
            }

            return false;
        }
    }
}