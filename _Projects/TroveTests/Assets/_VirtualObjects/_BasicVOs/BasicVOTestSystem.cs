using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Logging;
using Unity.Collections;
using System;
using Trove.VirtualObjects;


public struct BasicVOTests : IComponentData
{

}

public struct BasicVOComponent : IComponentData
{
    public ObjectHandle<List<BasicVOTestState>> StatesListHandle;
    public bool HasInitialized;
}

public struct BasicVOBufferElement : IBufferElementData
{
    public byte Byte;
}

public struct BasicVOTestState : IVirtualObject
{
    public int DebugValue;

    public void DisplayValue(Entity entity)
    {
        Log.Debug($"State display: entity {entity.Index} value {DebugValue}");
    }

    public void OnCreate(ref DynamicBuffer<byte> buffer)
    {
    }

    public void OnDestroy(ref DynamicBuffer<byte> buffer)
    {
    }
}

[BurstCompile]
public partial struct BasicVOTestSystem : ISystem
{
    private bool _hasInitialized;

    [BurstCompile]
    void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BasicVOTests>();
    }

    [BurstCompile]
    void OnDestroy(ref SystemState state)
    {
    }

    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        if (!_hasInitialized)
        {
            Entity entityA = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entityA, new BasicVOComponent());
            state.EntityManager.AddBuffer<BasicVOBufferElement>(entityA);

            Entity entityB = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entityB, new BasicVOComponent());
            state.EntityManager.AddBuffer<BasicVOBufferElement>(entityB);

            _hasInitialized = true;
        }

        BasicVOTestSystemJob job = new BasicVOTestSystemJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct BasicVOTestSystemJob : IJobEntity
    {
        public float DeltaTime;

        void Execute(Entity entity, ref BasicVOComponent voComp, ref DynamicBuffer<BasicVOBufferElement> voBuffer)
        {
            DynamicBuffer<byte> bytesBuffer = voBuffer.Reinterpret<byte>();

            if (!voComp.HasInitialized)
            {
                List<BasicVOTestState> newStatesList = new List<BasicVOTestState>(10);
                voComp.StatesListHandle = VirtualObjects.CreateObject(ref bytesBuffer, ref newStatesList);
                newStatesList.Add(ref bytesBuffer, new BasicVOTestState { DebugValue = 3 });
                newStatesList.Add(ref bytesBuffer, new BasicVOTestState { DebugValue = 6 });
                newStatesList.Add(ref bytesBuffer, new BasicVOTestState { DebugValue = 9 });
                VirtualObjects.SetObject(ref bytesBuffer, voComp.StatesListHandle, newStatesList);

                voComp.HasInitialized = true;
            }

            if (VirtualObjects.GetObjectCopy(ref bytesBuffer, voComp.StatesListHandle, out List<BasicVOTestState> statesList))
            {
                for (int i = 0; i < statesList.Length; i++)
                {
                    statesList.GetElementAt(ref bytesBuffer, i).DisplayValue(entity);
                }
            }
        }
    }
}