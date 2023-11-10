
using Trove.PolymorphicElements;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public struct StateMachineData
{
    public TimeData Time;
    public RefRW<LocalTransform> LocalTransform;
    public RefRW<MyStateMachine> MyStateMachine;
    public DynamicBuffer<byte> StateElementBuffer;
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

    public PolymorphicElementMetaData MoveStateData;
    public PolymorphicElementMetaData RotateStateData;
    public PolymorphicElementMetaData ScaleStateData;

    public static bool TransitionToState(int newStateStartIndex, ref StateMachineData data)
    {
        ref MyStateMachine sm = ref data.MyStateMachine.ValueRW;

        // If both previous and next states are valid
        if (newStateStartIndex != sm.CurrentStateIndex)
        {
            // Call state exit on current state
            IStateManager.Execute_OnStateExit(ref data.StateElementBuffer, sm.CurrentStateIndex, out _, ref data);

            // Change current state
            sm.PreviousStateIndex = sm.CurrentStateIndex;
            sm.CurrentStateIndex = newStateStartIndex;

            // Call state enter on new current state
            IStateManager.Execute_OnStateEnter(ref data.StateElementBuffer, sm.CurrentStateIndex, out _, ref data);

            return true;
        }

        return false;
    }

    public static void Update(ref StateMachineData data)
    {
        MyStateMachine sm = data.MyStateMachine.ValueRO;

        // Transition to initial state
        if (sm.CurrentStateIndex < 0)
        {
            MyStateMachine.TransitionToState(sm.MoveStateData.StartByteIndex, ref data);
        }

        // Update current state
        IStateManager.Execute_OnUpdate(ref data.StateElementBuffer, sm.CurrentStateIndex, out _, ref data);
    }
}

public struct StateElement : IBufferElementData
{
    public byte Value;
}