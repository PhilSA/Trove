using Trove.Statemachines;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

class CubeStateMachineAuthoring : MonoBehaviour
{
    public float PosOffset;
    public float RotSpeed;
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
            out DynamicBuffer<StateVersion> stateVersionsBuffer, 
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

        StateMachineUtilities.TrySetState<PolyCubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref stateVersionsBuffer,
            ref statesBuffer,
            stateAHandle,
            new StateA
            {
                NextState = stateBHandle,
                TransitionTimer = new StateTransitionTimer(1f),
                PositionOffset = authoring.PosOffset,
            });

        StateMachineUtilities.TrySetState<PolyCubeState, CubeGlobalStateUpdateData, CubeEntityStateUpdateData>(
            ref stateVersionsBuffer,
            ref statesBuffer,
            stateBHandle,
            new StateB
            {
                NextState = stateAHandle,
                TransitionTimer = new StateTransitionTimer(0.7f),
                RotationSpeed = authoring.RotSpeed,
            });
        
        SetComponent(entity, stateMachine);
    }
}
