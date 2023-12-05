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

    public void OnCreate(ref VirtualObjectsManager manager)
    {
    }

    public void OnDestroy(ref VirtualObjectsManager manager)
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
            Entity entityB = state.EntityManager.CreateEntity();

            state.EntityManager.AddComponentData(entityA, new BasicVOComponent());
            state.EntityManager.AddComponentData(entityB, new BasicVOComponent());

            state.EntityManager.AddBuffer<BasicVOBufferElement>(entityA);
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
            VirtualObjectsManager voManager = VirtualObjectsManager.Get(ref bytesBuffer);

            if (!voComp.HasInitialized)
            {
                Log.Debug($"About to create entity {entity.Index} test object ....");
                List<BasicVOTestState> newStatesList = new List<BasicVOTestState>(10);
                voComp.StatesListHandle = voManager.CreateObject(ref newStatesList);
                newStatesList.Add(ref voManager, new BasicVOTestState { DebugValue = 3 });
                newStatesList.Add(ref voManager, new BasicVOTestState { DebugValue = 6 });
                newStatesList.Add(ref voManager, new BasicVOTestState { DebugValue = 9 });
                voManager.SetObject(voComp.StatesListHandle, newStatesList);

                Log.Debug($"Created entity {entity.Index} test object of id {voComp.StatesListHandle.ObjectID} at address {voComp.StatesListHandle.Address.StartByteIndex}");

                voComp.HasInitialized = true;
            }

            if (voManager.GetObjectCopy(voComp.StatesListHandle, out List<BasicVOTestState> statesList))
            {
                for (int i = 0; i < statesList.Length; i++)
                {
                    statesList.GetElementAt(ref voManager, i).DisplayValue(entity);
                }
            }
        }
    }
}