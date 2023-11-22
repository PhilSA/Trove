
using System.Runtime.InteropServices;
using Trove.PolymorphicElements;
using Unity.Core;
using Unity.Mathematics;
using Unity.Logging;

public struct TimedState
{
    public float Duration;
    public float Timer;
    public float NormalizedTime => Timer / Duration;
    public bool MustExit => Timer >= Duration;

    public TimedState(float duration)
    {
        Duration = duration;
        Timer = default;
    }

    public void OnStateEnter(TimeData time, ref StateMachineData data)
    {
        Timer = 0f + data.ExtraTime;
    }

    public void OnStateUpdate(TimeData time, float speed)
    {
        Timer += time.DeltaTime * speed;
    }

    public void TransitionToStateIfEnded(int stateIndex, ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        float extraTime = Timer - Duration;
        if(extraTime >= 0f)
        {
            data.ExtraTime = extraTime;
            MyStateMachine.TransitionToState(stateIndex, ref parentStateMachine, ref data);
        }
    }
}

[PolymorphicElement]
public partial struct MoveState : IState
{
    public int NextStateIndex;
    public TimedState TimedState;
    public float3 StartPosition;
    public float3 Movement;

    public void OnStateMachineInitialize(ref Random random, ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        Movement = random.NextFloat3(new float3(3f));
    }

    public void OnStateEnter(ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        TimedState.OnStateEnter(data.Time, ref data);
        StartPosition = data.LocalTransform.ValueRW.Position;
    }

    public void OnStateExit(ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        data.LocalTransform.ValueRW.Position = StartPosition;
    }

    public void OnUpdate(float cummulativeSpeed, ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        TimedState.OnStateUpdate(data.Time, parentStateMachine.Speed);
        data.LocalTransform.ValueRW.Position = StartPosition + (math.sin(TimedState.NormalizedTime * math.PI) * Movement);

        TimedState.TransitionToStateIfEnded(NextStateIndex, ref parentStateMachine, ref data);
    }
}

[PolymorphicElement]
public partial struct RotateState : IState
{
    public int NextStateIndex;
    public TimedState TimedState;
    public float3 RotationSpeed;

    public void OnStateMachineInitialize(ref Random random, ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        RotationSpeed = random.NextFloat3(new float3(1f));
    }

    public void OnStateEnter(ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        TimedState.OnStateEnter(data.Time, ref data);
    }

    public void OnStateExit(ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
    }

    public void OnUpdate(float cummulativeSpeed, ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        TimedState.OnStateUpdate(data.Time, parentStateMachine.Speed);
        data.LocalTransform.ValueRW.Rotation = math.mul(quaternion.Euler(RotationSpeed * data.Time.DeltaTime * parentStateMachine.Speed), data.LocalTransform.ValueRW.Rotation);

        TimedState.TransitionToStateIfEnded(NextStateIndex, ref parentStateMachine, ref data);
    }
}

[PolymorphicElement]
public partial struct ScaleState : IState
{
    public int NextStateIndex;
    public TimedState TimedState;
    public float StartScale;
    public float AddedScale;
    public MyStateMachine SubStateMachine;

    public void OnStateMachineInitialize(ref Random random, ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        AddedScale = random.NextFloat(2f);
    }

    public void OnStateEnter(ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        TimedState.OnStateEnter(data.Time, ref data);
        StartScale = data.LocalTransform.ValueRW.Scale;

        if (SubStateMachine.CurrentStateIndex < 0)
        {
            MyStateMachine.TransitionToState(SubStateMachine.StartStateIndex, ref SubStateMachine, ref data);
        }
        else
        {
            IStateManager.OnStateEnter(data.StateElementsBuffer, SubStateMachine.CurrentStateByteStartIndex, out _, out _, ref SubStateMachine, ref data);
        }
    }

    public void OnStateExit(ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        data.LocalTransform.ValueRW.Scale = StartScale;

        IStateManager.OnStateExit(data.StateElementsBuffer, SubStateMachine.CurrentStateByteStartIndex, out _, out _, ref SubStateMachine, ref data);
    }

    public void OnUpdate(float cummulativeSpeed, ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        TimedState.OnStateUpdate(data.Time, parentStateMachine.Speed);
        data.LocalTransform.ValueRW.Scale = StartScale * (1f + (math.sin(TimedState.NormalizedTime * math.PI) * AddedScale));

        IStateManager.OnUpdate(data.StateElementsBuffer, SubStateMachine.CurrentStateByteStartIndex, out _, out _, cummulativeSpeed * SubStateMachine.Speed, ref SubStateMachine, ref data);

        TimedState.TransitionToStateIfEnded(NextStateIndex, ref parentStateMachine, ref data);
    }
}

[PolymorphicElement]
public partial struct ColorState : IState
{
    public int NextStateIndex;
    public TimedState TimedState;
    public float4 Color;

    public void OnStateMachineInitialize(ref Random random, ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
    }

    public void OnStateEnter(ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        TimedState.OnStateEnter(data.Time, ref data);
        data.EmissionColor.ValueRW.Value = Color;
    }

    public void OnStateExit(ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        data.EmissionColor.ValueRW.Value = new float4(0f, 0f, 0f, 1f);
    }

    public void OnUpdate(float cummulativeSpeed, ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        TimedState.OnStateUpdate(data.Time, parentStateMachine.Speed);
        TimedState.TransitionToStateIfEnded(NextStateIndex, ref parentStateMachine, ref data);
    }
}