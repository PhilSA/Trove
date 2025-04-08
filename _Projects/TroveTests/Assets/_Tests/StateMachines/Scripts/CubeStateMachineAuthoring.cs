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
        
        StateMachineUtilities.BakeStateMachineComponents<PolyCubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            this, 
            entity, 
            out StateMachine stateMachine, 
            out DynamicBuffer<StateData> stateVersionsBuffer, 
            out DynamicBuffer<PolyCubeState> statesBuffer);
        
        StateMachineUtilities.InitStateMachine<PolyCubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref stateMachine,
            ref stateVersionsBuffer,
            ref statesBuffer,
            10);
        
        StateMachineUtilities.CreateState<PolyCubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref stateVersionsBuffer, 
            ref statesBuffer,
            default,
            out StateHandle stateAHandle);
        
        StateMachineUtilities.CreateState<PolyCubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref stateVersionsBuffer, 
            ref statesBuffer,
            default,
            out StateHandle stateBHandle);
        
        StateMachineUtilities.CreateState<PolyCubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref stateVersionsBuffer, 
            ref statesBuffer,
            default,
            out StateHandle stateC1Handle);
        
        StateMachineUtilities.CreateState<PolyCubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref stateVersionsBuffer, 
            ref statesBuffer,
            default,
            out StateHandle stateC2Handle);
        
        StateMachineUtilities.CreateState<PolyCubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref stateVersionsBuffer, 
            ref statesBuffer,
            default,
            out StateHandle stateC3Handle);

        StateMachineUtilities.TrySetState<PolyCubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref stateVersionsBuffer,
            ref statesBuffer,
            stateAHandle,
            new CubeStateA
            {
                NextState = stateBHandle,
                TransitionTimer = new StateTransitionTimer(1f),
                PositionOffset = authoring.PosOffset,
            });

        StateMachineUtilities.TrySetState<PolyCubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref stateVersionsBuffer,
            ref statesBuffer,
            stateBHandle,
            new CubeStateB
            {
                NextState = stateAHandle,
                TransitionTimer = new StateTransitionTimer(0.7f),
                RotationSpeed = authoring.RotSpeed,
                StateMachine = new StateMachine
                {
                    InitialState = stateC1Handle,
                },
            });

        StateMachineUtilities.TrySetState<PolyCubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref stateVersionsBuffer,
            ref statesBuffer,
            stateC1Handle,
            new CubeStateC
            {
                NextState = stateC2Handle,
                TransitionTimer = new StateTransitionTimer(0.3f),
                Scale = authoring.Scale1,
            });

        StateMachineUtilities.TrySetState<PolyCubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref stateVersionsBuffer,
            ref statesBuffer,
            stateC2Handle,
            new CubeStateC
            {
                NextState = stateC3Handle,
                TransitionTimer = new StateTransitionTimer(0.3f),
                Scale = authoring.Scale2,
            });

        StateMachineUtilities.TrySetState<PolyCubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref stateVersionsBuffer,
            ref statesBuffer,
            stateC3Handle,
            new CubeStateC
            {
                NextState = stateC1Handle,
                TransitionTimer = new StateTransitionTimer(0.3f),
                Scale = authoring.Scale3,
            });
        
        stateMachine.InitialState = stateAHandle;
        
        SetComponent(entity, stateMachine);
    }
}
