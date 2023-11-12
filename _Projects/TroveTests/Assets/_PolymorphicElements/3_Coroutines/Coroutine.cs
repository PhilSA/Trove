using Unity.Entities;
using Unity.Mathematics;
using System;
using Trove.PolymorphicElements;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Core;

[Serializable]
public struct Coroutine : IComponentData
{
    public bool AutoDestroy;

    public int CurrentStateIndex;
    public bool Next;
}

[Serializable]
public struct CoroutineState : IBufferElementData
{
    public byte Value;
}

[Serializable]
public struct CoroutineMetaData : IBufferElementData
{
    public PolymorphicElementMetaData Value;
}

public struct CoroutineUpdateData
{
    public TimeData Time;
    public EntityCommandBuffer ECB;
    public RefRW<Coroutine> Coroutine;
    public ComponentLookup<LocalTransform> LocalTransformLookup;
    public ComponentLookup<URPMaterialPropertyEmissionColor> EmissionColorLookup;
}

[PolymorphicElementsGroup]
public interface ICoroutineState
{
    [AllowElementModification]
    void Begin(ref CoroutineUpdateData data);
    [AllowElementModification]
    bool Update(ref CoroutineUpdateData data);
}

[PolymorphicElement]
public struct Coroutine_MoveTo : ICoroutineState
{
    public Entity Entity;
    public float Speed;
    public float3 Target;

    private const float ToleranceDistanceSq = 0.01f * 0.01f;

    public void Begin(ref CoroutineUpdateData data)
    { }

    public bool Update(ref CoroutineUpdateData data)
    {
        if(data.LocalTransformLookup.TryGetComponent(Entity, out LocalTransform localTransform))
        {
            float3 vectorToTarget = Target - localTransform.Position;
            float distanceToTarget = math.length(vectorToTarget);
            float3 directionToTarget = math.normalizesafe(vectorToTarget);

            float distanceFromSpeed = math.min(data.Time.DeltaTime * Speed, distanceToTarget);
            localTransform.Position += directionToTarget * distanceFromSpeed;

            data.LocalTransformLookup[Entity] = localTransform;

            // Detect reached target
            if(math.distancesq(localTransform.Position, Target) < ToleranceDistanceSq)
            {
                data.Coroutine.ValueRW.Next = true;
            }
        }
        else
        {
            data.Coroutine.ValueRW.Next = true;
        }
        return false;
    }
}

[PolymorphicElement]
public struct Coroutine_SetColor : ICoroutineState
{
    public Entity Entity;
    public float4 Target;

    public void Begin(ref CoroutineUpdateData data)
    { }

    public bool Update(ref CoroutineUpdateData data)
    {
        if (data.EmissionColorLookup.TryGetComponent(Entity, out URPMaterialPropertyEmissionColor emissionColor))
        {
            emissionColor.Value = Target;
            data.EmissionColorLookup[Entity] = emissionColor;
        }
        data.Coroutine.ValueRW.Next = true;
        return false;
    }
}

[PolymorphicElement]
public struct Coroutine_Wait : ICoroutineState
{
    public float Time;
    public float StartTime;

    public void Begin(ref CoroutineUpdateData data)
    {
        StartTime = (float)data.Time.ElapsedTime;
    }

    public bool Update(ref CoroutineUpdateData data)
    {
        if((float)data.Time.ElapsedTime > StartTime + Time)
        {
            data.Coroutine.ValueRW.Next = true;
        }
        return false;
    }
}