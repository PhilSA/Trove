
using Trove.Statemachines;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// This an example of an authoring component for this state machine
/// TODO: move this code out of this file, to a new file named "CubeSMAuthoring". MonoBehaviours need their file name to match.
/// </summary>
class CubeSMAuthoring : MonoBehaviour
{
    class Baker : Baker<CubeSMAuthoring>
    {
        public override void Bake(CubeSMAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

            // Add the state machine components
            StateMachineUtilities
                .BakeStateMachineComponents<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
                    this,
                    entity,
                    out StateMachine stateMachine,
                    out DynamicBuffer<CubeSMState> statesBuffer);

            // Initialize the state machine buffers with an initial capacity
            StateMachineUtilities
                .InitStateMachine<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
                    ref stateMachine,
                    ref statesBuffer,
                    8);

            // Create a few states and remember their StateHandles.
            StateMachineUtilities
                .CreateState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
                    ref statesBuffer,
                    default,
                    out StateHandle stateAHandle);
            StateMachineUtilities
                .CreateState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
                    ref statesBuffer,
                    default,
                    out StateHandle stateBHandle);
            StateMachineUtilities.CreateState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
                ref statesBuffer,
                default,
                out StateHandle stateC1Handle);
            StateMachineUtilities.CreateState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
                ref statesBuffer,
                default,
                out StateHandle stateC2Handle);
            StateMachineUtilities.CreateState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
                ref statesBuffer,
                default,
                out StateHandle stateC3Handle);

            // Set state data, now that we have all of our state handles created.
            // Note: it can be useful to set state data after creating all of our state handles, in cases where
            // Our states must store state handles to transition to. If not, we could've also set state data directly
            // in the "CreateState" function.
            StateMachineUtilities.TrySetState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
                ref statesBuffer,
                stateAHandle,
                new CubeSMState
                {
                    State = new CubeSMPositionState
                    {
                        TargetState = stateBHandle,
                        Duration = 1f,
                        PositionOffset = 2f,
                    },
                });
            StateMachineUtilities.TrySetState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
                ref statesBuffer,
                stateBHandle,
                new CubeSMState
                {
                    State = new CubeSMRotationState
                    {
                        TargetState = stateAHandle,
                        Duration = 2f,
                        RotationSpeed = 3f,
                        
                        StateMachine = new StateMachine(stateC1Handle),
                    },
                });
            StateMachineUtilities.TrySetState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
                ref statesBuffer,
                stateC1Handle,
                new CubeSMState
                {
                    State = new CubeSMScaleState()
                    {
                        TargetState = stateC2Handle,
                        Duration = 0.3f,
                        Scale = 0.3f,
                    },
                });
            StateMachineUtilities.TrySetState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
                ref statesBuffer,
                stateC2Handle,
                new CubeSMState
                {
                    State = new CubeSMScaleState()
                    {
                        TargetState = stateC3Handle,
                        Duration = 0.4f,
                        Scale = 0.75f,
                    },
                });
            StateMachineUtilities.TrySetState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
                ref statesBuffer,
                stateC3Handle,
                new CubeSMState
                {
                    State = new CubeSMScaleState()
                    {
                        TargetState = stateC1Handle,
                        Duration = 0.5f,
                        Scale = 1f,
                    },
                });

            // Set an initial state for our state machine. This is a state the state machine will automatically 
            // transition to the first time it updates.
            // Note: we don't want to set the "stateMachine.CurrentState" here, because if we do that here in baking,
            // the current state would not get an "OnStateEnter" at runtime.
            stateMachine.InitialState = stateAHandle;

            // Write back any changes to the stateMachine component
            SetComponent(entity, stateMachine);
        }
    }
}