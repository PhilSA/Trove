
using Trove.PolymorphicElements;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Logging;
using Unity.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

public struct StateMachineData
{
    public TimeData Time;
    public RefRW<LocalTransform> LocalTransform;
    public RefRW<URPMaterialPropertyEmissionColor> EmissionColor;
    public DynamicBuffer<byte> StateElementBuffer;
    public DynamicBuffer<StateMetaData> StateMetaDataBuffer;

    public float ExtraTime;
}

[PolymorphicElementsGroup]
public interface IState
{
    [AllowElementModification]
    void OnStateMachineInitialize(ref Unity.Mathematics.Random random, ref MyStateMachine parentStateMachine, ref StateMachineData data);
    [AllowElementModification]
    void OnStateEnter(ref MyStateMachine parentStateMachine, ref StateMachineData data);
    [AllowElementModification]
    void OnStateExit(ref MyStateMachine parentStateMachine, ref StateMachineData data);
    [AllowElementModification]
    void OnUpdate(float cummulativeSpeed, ref MyStateMachine parentStateMachine, ref StateMachineData data);
}

public struct MyStateMachine : IComponentData
{
    public float Speed;
    public int StartStateIndex;

    public int CurrentStateIndex;
    public int CurrentStateByteStartIndex;
    public int PreviousStateIndex;

    public static bool TransitionToState(int newStateIndex, ref MyStateMachine stateMachine, ref StateMachineData data)
    {
        // If both previous and next states are valid
        if (newStateIndex != stateMachine.CurrentStateIndex &&
            GetStateMetaData(newStateIndex, out PolymorphicElementMetaData newStateMetaData, ref data.StateMetaDataBuffer))
        {
            // Call state exit on current state
            if (PolymorphicElementsUtility.GetPtrOfByteIndex(data.StateElementBuffer, stateMachine.CurrentStateByteStartIndex, out PolymorphicElementPtr ptr))
            {
                IStateManager.OnStateExit(ptr, out _, ref stateMachine, ref data);
            }

            // Change current state
            stateMachine.PreviousStateIndex = stateMachine.CurrentStateIndex;
            stateMachine.CurrentStateIndex = newStateIndex;
            stateMachine.CurrentStateByteStartIndex = newStateMetaData.StartByteIndex;

            // Call state enter on new current state
            if (PolymorphicElementsUtility.GetPtrOfByteIndex(data.StateElementBuffer, stateMachine.CurrentStateByteStartIndex, out ptr))
            {
                IStateManager.OnStateEnter(ptr, out _, ref stateMachine, ref data);
            }

            return true;
        }

        return false;
    }

    public static bool GetStateMetaData(int stateIndex, out PolymorphicElementMetaData metaData, ref DynamicBuffer<StateMetaData> metaDatasBuffer)
    {
        if(stateIndex >= 0 && stateIndex < metaDatasBuffer.Length)
        {
            metaData = metaDatasBuffer[stateIndex].Value;
            return true;
        }

        metaData = default;
        return false;
    }
}

[InternalBufferCapacity(0)]
public struct StateElement : IBufferElementData
{
    public byte Value;
}

[InternalBufferCapacity(0)]
public struct StateMetaData : IBufferElementData
{
    public PolymorphicElementMetaData Value;
}