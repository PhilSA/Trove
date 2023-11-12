
using Trove.PolymorphicElements;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Burst;
using Unity.Collections;

public struct EventExecutionData
{
    public RefRW<LocalTransform> LocalTransform;
}

[PolymorphicElementsGroup]
public interface IMyEvent
{
    void Process(ref EventExecutionData data);
}

[PolymorphicElement]
public struct SetPositionEvent : IMyEvent
{
    public float3 Position;

    public void Process(ref EventExecutionData data)
    {
        data.LocalTransform.ValueRW.Position = Position;
    }
}

[PolymorphicElement]
public struct SetRotationEvent : IMyEvent
{
    public quaternion Rotation;

    public void Process(ref EventExecutionData data)
    {
        data.LocalTransform.ValueRW.Rotation = Rotation;
    }
}

[PolymorphicElement]
public struct SetScaleEvent : IMyEvent
{
    public float Scale;

    public void Process(ref EventExecutionData data)
    {
        data.LocalTransform.ValueRW.Scale = Scale;
    }
}

public struct MyEventsBufferElement : IBufferElementData
{
    public byte Value;
}

[BurstCompile]
public partial struct EventTestSystem : ISystem
{
    [BurstCompile]
    void OnCreate(ref SystemState state)
    {
        // Create an Entity that our events can affect
        Entity testEntity = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(testEntity, LocalTransform.Identity);

        // Add a dynamic buffer of bytes to the targeted entity hold our events of various types
        state.EntityManager.AddBuffer<MyEventsBufferElement>(testEntity);

        // Add events to entities that have a transform and a MyEventsBufferElement buffer
        foreach (var (localTransform, eventsBuffer) in SystemAPI.Query<RefRW<LocalTransform>, DynamicBuffer<MyEventsBufferElement>>())
        {
            // Reinterpret our buffer as a bytes buffer, so our IMyEventManager know how to work with it
            DynamicBuffer<byte> eventsByteBuffer = eventsBuffer.Reinterpret<byte>();

            // Add events to buffer on that entity
            IMyEventManager.AddElement(ref eventsByteBuffer, new SetPositionEvent
            {
                Position = new float3(1f, 1f, 1f),
            });
            IMyEventManager.AddElement(ref eventsByteBuffer, new SetScaleEvent
            {
                Scale = 2f,
            });
            IMyEventManager.AddElement(ref eventsByteBuffer, new SetScaleEvent
            {
                Scale = 5f,
            });
            IMyEventManager.AddElement(ref eventsByteBuffer, new SetRotationEvent
            {
                Rotation = quaternion.Euler(1f, 0.5f, 0f),
            });
            IMyEventManager.AddElement(ref eventsByteBuffer, new SetPositionEvent
            {
                Position = new float3(5f, 2f, 3f),
            });
        }

        // Execute events on entities that have a transform and a MyEventsBufferElement buffer
        foreach (var (localTransform, eventsBuffer) in SystemAPI.Query<RefRW<LocalTransform>, DynamicBuffer<MyEventsBufferElement>>())
        {
            // Reinterpret our buffer as a bytes buffer, so our IMyEventManager know how to work with it
            DynamicBuffer<byte> eventsByteBuffer = eventsBuffer.Reinterpret<byte>();

            // Create the data struct used by our events for their processing
            EventExecutionData data = new EventExecutionData
            {
                LocalTransform = localTransform,
            };

            // Execute the Process() function of every element.
            // This loop will keep iteration as long as we haven't reached the end of the elements in the eventsBuffer.
            // IMyEventManager.Execute_Process returns true if it has found an element to read at the given elementStartByteIndex,
            // and it will then output the next element start byte index to elementStartByteIndex.
            int elementStartByteIndex = 0;
            while (IMyEventManager.Execute_Process(ref eventsByteBuffer, elementStartByteIndex, out elementStartByteIndex, ref data))
            { }
        }

    }
}

























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
            IStateManager.Execute_OnStateExit(ref data.StateElementBuffer, stateMachine.CurrentStateByteStartIndex, out _, ref stateMachine, ref data);

            // Change current state
            stateMachine.PreviousStateIndex = stateMachine.CurrentStateIndex;
            stateMachine.CurrentStateIndex = newStateIndex;
            stateMachine.CurrentStateByteStartIndex = newStateMetaData.StartByteIndex;

            // Call state enter on new current state
            IStateManager.Execute_OnStateEnter(ref data.StateElementBuffer, stateMachine.CurrentStateByteStartIndex, out _, ref stateMachine, ref data);

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