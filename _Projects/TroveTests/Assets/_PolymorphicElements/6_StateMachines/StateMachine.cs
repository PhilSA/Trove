
using Trove.PolymorphicElements;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct StateMachineData
{
    public TimeData Time;
    [NativeDisableUnsafePtrRestriction]
    public RefRW<LocalTransform> LocalTransform;
    [NativeDisableUnsafePtrRestriction]
    public RefRW<MyStateMachine> MyStateMachine;
    [ReadOnly]
    public DynamicBuffer<byte> StateElementBuffer;
    [ReadOnly]
    public DynamicBuffer<StateMetaData> StateMetadataBuffer;
}

[PolymorphicElementsGroup]
public interface IState
{
    void OnStateEnter(ref StateMachineData data);
    void OnStateExit(ref StateMachineData data);
    void OnUpdate(ref StateMachineData data);
}

public struct MyStateMachine : IComponentData
{
    public float Speed;
    public int CurrentStateIndex;
    public int PreviousStateIndex;

    public static void Create(EntityCommandBuffer ecb, Entity onEntity, ref Random random)
    {
        // State machine
        float randomSpeed = random.NextFloat(0.5f, 3f);
        MyStateMachine sm = new MyStateMachine
        {
            CurrentStateIndex = -1,
            PreviousStateIndex = -1,
            Speed = randomSpeed,
        };
        ecb.AddComponent(onEntity, sm);
        DynamicBuffer<byte> stateElements = ecb.AddBuffer<StateElement>(onEntity).Reinterpret<byte>();
        DynamicBuffer<StateMetaData> stateMetaDatas = ecb.AddBuffer<StateMetaData>(onEntity);

        // Write states
        {
            int stateInstanceIdCounter = 1;
            IStateManager.AddElement(ref stateElements, new MoveState
            {
                Duration = 2f,
                Movement = random.NextFloat3Direction() * 2f,
            }, stateInstanceIdCounter++, out PolymorphicElementMetaData metaData);
            stateMetaDatas.Add(new StateMetaData { Value = metaData });

            IStateManager.AddElement(ref stateElements, new RotateState
            {
                Duration = 2f,
                RotationSpeed = random.NextFloat3(new float3(10f)),
            }, stateInstanceIdCounter++, out metaData);
            stateMetaDatas.Add(new StateMetaData { Value = metaData });

            IStateManager.AddElement(ref stateElements, new ScaleState
            {
                Duration = 2f,
                AddedScale = random.NextFloat(3f),
            }, stateInstanceIdCounter++, out metaData);
            stateMetaDatas.Add(new StateMetaData { Value = metaData });
        }
    }

    public static bool TransitionToState(int toState, ref StateMachineData data)
    {
        // If both previous and next states are valid
        if (toState != data.MyStateMachine.ValueRW.CurrentStateIndex &&
            GetStateByteIndex(toState, data.StateElementBuffer, data.StateMetadataBuffer, out int nextStateByteIndex))
        {
            // Call state exit on current state
            if (GetStateByteIndex(data.MyStateMachine.ValueRW.CurrentStateIndex, data.StateElementBuffer, data.StateMetadataBuffer, out int currentStateByteIndex))
            {
                IStateManager.Execute_OnStateExit(ref data.StateElementBuffer, ref currentStateByteIndex, ref data);
            }

            // Change current state
            data.MyStateMachine.ValueRW.PreviousStateIndex = data.MyStateMachine.ValueRW.CurrentStateIndex;
            data.MyStateMachine.ValueRW.CurrentStateIndex = toState;

            // Call state enter on new current state
            IStateManager.Execute_OnStateEnter(ref data.StateElementBuffer, ref nextStateByteIndex, ref data);

            return true;
        }

        return false;
    }

    public static bool GetStateByteIndex(int stateIndex, DynamicBuffer<byte> stateElementBuffer, DynamicBuffer<StateMetaData> stateMetadataBuffer, out int stateByteIndex)
    {
        if (stateIndex >= 0 && stateMetadataBuffer.Length > stateIndex)
        {
            stateByteIndex = stateMetadataBuffer[stateIndex].Value.StartByteIndex;
            return true;
        }

        stateByteIndex = 0;
        return false;
    }
}

public struct StateElement : IBufferElementData
{
    public byte Value;
}

public struct StateMetaData : IBufferElementData
{
    public PolymorphicElementMetaData Value;
}