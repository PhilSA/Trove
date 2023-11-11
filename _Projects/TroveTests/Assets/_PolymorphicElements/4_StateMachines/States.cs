
using System.Runtime.InteropServices;
using Trove.PolymorphicElements;
using Unity.Core;
using Unity.Mathematics;

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

    public void OnStateEnter(TimeData time)
    {
        Timer = 0f;
    }

    public void OnStateUpdate(TimeData time, float speed)
    {
        Timer += time.DeltaTime * speed;
    }
}

[PolymorphicElement]
public struct MoveState : IState
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
        TimedState.OnStateEnter(data.Time);
        StartPosition = data.LocalTransform.ValueRW.Position;
    }

    public void OnStateExit(ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        data.LocalTransform.ValueRW.Position = StartPosition;
    }

    public void OnUpdate(ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        TimedState.OnStateUpdate(data.Time, parentStateMachine.Speed);
        data.LocalTransform.ValueRW.Position = StartPosition + (math.sin(TimedState.NormalizedTime * math.PI) * Movement);

        if (TimedState.MustExit)
        {
            MyStateMachine.TransitionToState(NextStateIndex, ref parentStateMachine, ref data);
        }
    }
}

[PolymorphicElement]
public struct RotateState : IState
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
        TimedState.OnStateEnter(data.Time);
    }

    public void OnStateExit(ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
    }

    public void OnUpdate(ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        TimedState.OnStateUpdate(data.Time, parentStateMachine.Speed);
        data.LocalTransform.ValueRW.Rotation = math.mul(quaternion.Euler(RotationSpeed * data.Time.DeltaTime * parentStateMachine.Speed), data.LocalTransform.ValueRW.Rotation);

        if (TimedState.MustExit)
        {
            MyStateMachine.TransitionToState(NextStateIndex, ref parentStateMachine, ref data);
        }
    }
}

[PolymorphicElement]
public struct ScaleState : IState
{
    public int NextStateIndex;
    public TimedState TimedState;
    public float StartScale;
    public float AddedScale;
    public MyStateMachine SubStateMachine;

    public void OnStateMachineInitialize(ref Random random, ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        AddedScale = random.NextFloat(3f);
    }

    public void OnStateEnter(ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        TimedState.OnStateEnter(data.Time);
        StartScale = data.LocalTransform.ValueRW.Scale;

        if (SubStateMachine.CurrentStateIndex < 0)
        {
            MyStateMachine.TransitionToState(SubStateMachine.StartStateIndex, ref SubStateMachine, ref data);
        }
        else
        {
            IStateManager.Execute_OnStateEnter(ref data.StateElementBuffer, SubStateMachine.CurrentStateByteStartIndex, out _, ref SubStateMachine, ref data);
        }
    }

    public void OnStateExit(ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        data.LocalTransform.ValueRW.Scale = StartScale;

        IStateManager.Execute_OnStateExit(ref data.StateElementBuffer, SubStateMachine.CurrentStateByteStartIndex, out _, ref SubStateMachine, ref data);
    }

    public void OnUpdate(ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        TimedState.OnStateUpdate(data.Time, parentStateMachine.Speed);
        data.LocalTransform.ValueRW.Scale = StartScale * (1f + (math.sin(TimedState.NormalizedTime * math.PI) * AddedScale));

        IStateManager.Execute_OnUpdate(ref data.StateElementBuffer, SubStateMachine.CurrentStateByteStartIndex, out _, ref SubStateMachine, ref data);

        if (TimedState.MustExit)
        {
            MyStateMachine.TransitionToState(NextStateIndex, ref parentStateMachine, ref data);
        }
    }
}

[PolymorphicElement]
public struct ColorState : IState
{
    public int NextStateIndex;
    public TimedState TimedState;
    public float4 Color;

    public void OnStateMachineInitialize(ref Random random, ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
    }

    public void OnStateEnter(ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        TimedState.OnStateEnter(data.Time);
        data.EmissionColor.ValueRW.Value = Color;
    }

    public void OnStateExit(ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        data.EmissionColor.ValueRW.Value = new float4(0f, 0f, 0f, 1f);
    }

    public void OnUpdate(ref MyStateMachine parentStateMachine, ref StateMachineData data)
    {
        TimedState.OnStateUpdate(data.Time, parentStateMachine.Speed);
        if (TimedState.MustExit)
        {
            MyStateMachine.TransitionToState(NextStateIndex, ref parentStateMachine, ref data);
        }
    }
}