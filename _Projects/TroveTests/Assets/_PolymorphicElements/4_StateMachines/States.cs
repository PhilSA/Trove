
using System.Runtime.InteropServices;
using Trove.PolymorphicElements;
using Unity.Mathematics;

[PolymorphicElement]
public struct MoveState : IState
{
    public int NextStateStartIndex;
    public int UpdateCounter;
    public float StartTime;
    public float Duration;
    public float3 StartPosition;
    public float3 Movement;

    public void OnStateEnter(ref StateMachineData data)
    {
        StartTime = (float)data.Time.ElapsedTime;
        StartPosition = data.LocalTransform.ValueRW.Position;
    }

    public void OnStateExit(ref StateMachineData data)
    {
        data.LocalTransform.ValueRW.Position = StartPosition;
    }

    public void OnUpdate(ref StateMachineData data)
    {
        UpdateCounter++;
        float normTime = ((float)data.Time.ElapsedTime - StartTime) / (Duration / data.MyStateMachine.ValueRW.Speed);
        data.LocalTransform.ValueRW.Position = StartPosition + (math.sin(normTime * math.PI) * Movement);

        if (normTime >= 1f)
        {
            MyStateMachine.TransitionToState(NextStateStartIndex, ref data);
        }
    }
}

[PolymorphicElement]
public struct RotateState : IState
{
    public int NextStateStartIndex;
    public int UpdateCounter;
    public float Duration;
    public float Timer;
    public float3 RotationSpeed;

    public void OnStateEnter(ref StateMachineData data)
    {
        Timer = 0f;
    }

    public void OnStateExit(ref StateMachineData data)
    {
    }

    public void OnUpdate(ref StateMachineData data)
    {
        UpdateCounter++;
        Timer += data.Time.DeltaTime * data.MyStateMachine.ValueRW.Speed;
        data.LocalTransform.ValueRW.Rotation = math.mul(quaternion.Euler(RotationSpeed * data.Time.DeltaTime * data.MyStateMachine.ValueRW.Speed), data.LocalTransform.ValueRW.Rotation);

        if (Timer >= Duration)
        {
            MyStateMachine.TransitionToState(NextStateStartIndex, ref data);
        }
    }
}

[PolymorphicElement]
public struct ScaleState : IState
{
    public int NextStateStartIndex;
    public int UpdateCounter;
    public float StartTime;
    public float Duration;
    public float StartScale;
    public float AddedScale;

    public void OnStateEnter(ref StateMachineData data)
    {
        StartTime = (float)data.Time.ElapsedTime;
        StartScale = data.LocalTransform.ValueRW.Scale;
    }

    public void OnStateExit(ref StateMachineData data)
    {
        data.LocalTransform.ValueRW.Scale = StartScale;
    }

    public void OnUpdate(ref StateMachineData data)
    {
        UpdateCounter++;
        float normTime = ((float)data.Time.ElapsedTime - StartTime) / (Duration / data.MyStateMachine.ValueRW.Speed);
        data.LocalTransform.ValueRW.Scale = StartScale * (1f + (math.sin(normTime * math.PI) * AddedScale));

        if (normTime >= 1f)
        {
            MyStateMachine.TransitionToState(NextStateStartIndex, ref data);
        }
    }
}