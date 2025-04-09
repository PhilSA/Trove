using Trove.Statemachines;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class CubeStateMachineAuthoring : MonoBehaviour
{
    public float PosOffset;
    public float RotSpeed;
    public float Scale1;
    public float Scale2;
    public float Scale3;
}

class CubeStateMachineAuthoringBaker : Baker<CubeStateMachineAuthoring>
{
    public override void Bake(CubeStateMachineAuthoring authoring)
    {
        Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
        
        StateMachineUtilities.BakeStateMachineComponents<CubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            this, 
            entity, 
            out StateMachine stateMachine, 
            out DynamicBuffer<CubeState> statesBuffer);
        
        StateMachineUtilities.InitStateMachine<CubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref stateMachine,
            ref statesBuffer,
            10);
        
        StateMachineUtilities.CreateState<CubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref statesBuffer,
            default,
            out StateHandle stateAHandle);
        
        StateMachineUtilities.CreateState<CubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref statesBuffer,
            default,
            out StateHandle stateBHandle);
        
        StateMachineUtilities.CreateState<CubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref statesBuffer,
            default,
            out StateHandle stateC1Handle);
        
        StateMachineUtilities.CreateState<CubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref statesBuffer,
            default,
            out StateHandle stateC2Handle);
        
        StateMachineUtilities.CreateState<CubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref statesBuffer,
            default,
            out StateHandle stateC3Handle);

        StateMachineUtilities.TrySetState<CubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref statesBuffer,
            stateAHandle,
            new CubeState
            {
                State = new CubeStatePosition
                {
                    NextState = stateBHandle,
                    TransitionTimer = new StateTransitionTimer(1f),
                    PositionOffset = authoring.PosOffset,
                }
            });

        StateMachineUtilities.TrySetState<CubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref statesBuffer,
            stateBHandle,
            new CubeState
            {
                State = new CubeStateRotation
                {
                    NextState = stateAHandle,
                    TransitionTimer = new StateTransitionTimer(0.7f),
                    RotationSpeed = authoring.RotSpeed,
                },
                SubStateMachine = new StateMachine
                {
                    InitialState = stateC1Handle,
                },
            });

        StateMachineUtilities.TrySetState<CubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref statesBuffer,
            stateC1Handle,
            new CubeState
            {
                State = new CubeStateScale
                {
                    NextState = stateC2Handle,
                    TransitionTimer = new StateTransitionTimer(0.3f),
                    Scale = authoring.Scale1,
                }
            });

        StateMachineUtilities.TrySetState<CubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref statesBuffer,
            stateC2Handle,
            new CubeState
            {
                State = new CubeStateScale
                {
                    NextState = stateC3Handle,
                    TransitionTimer = new StateTransitionTimer(0.3f),
                    Scale = authoring.Scale2,
                }
            });

        StateMachineUtilities.TrySetState<CubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref statesBuffer,
            stateC3Handle,
            new CubeState
            {
                State = new CubeStateScale
                {
                    NextState = stateC1Handle,
                    TransitionTimer = new StateTransitionTimer(0.3f),
                    Scale = authoring.Scale3,
                }
            });
        
        stateMachine.InitialState = stateAHandle;
        
        SetComponent(entity, stateMachine);
    }
}
