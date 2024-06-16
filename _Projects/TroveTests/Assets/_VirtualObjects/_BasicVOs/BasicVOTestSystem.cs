using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Logging;
using Unity.Collections;
using System;
using Trove.VirtualObjects;
using Unity.Collections.LowLevel.Unsafe;


public struct BasicVOTests : IComponentData
{
    public bool UseVirtualObjects;
    public int EntitiesCount;
    public int ElementsCount;

    public bool _hasInitialized;
}

public struct BasicVOComponent : IComponentData
{
    public int Sum;
    public ObjectHandle<List<BasicVOTestState>> StatesListHandle;
}

public struct BasicVOBufferElement : IBufferElementData
{
    public byte Byte;
}

public struct BasicRegularComponent : IComponentData
{
    public int Sum;
}

[InternalBufferCapacity(0)]
public struct BasicRegularBufferElement : IBufferElementData
{
    public BasicVOTestState State;
}

public struct BasicVOTestState : IVirtualObject
{
    public int DebugValue;

    public void DisplayValue(Entity entity)
    {
        //Log.Debug($"State display: entity {entity.Index} value {DebugValue}");
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
        ref BasicVOTests tester = ref SystemAPI.GetSingletonRW<BasicVOTests>().ValueRW;

        if (!tester._hasInitialized)
        {
            for (int i = 0; i < tester.EntitiesCount; i++)
            {
                Entity entity = state.EntityManager.CreateEntity();
                if (tester.UseVirtualObjects)
                {
                    state.EntityManager.AddComponentData(entity, new BasicVOComponent());
                    DynamicBuffer<byte> bytesBuffer = state.EntityManager.AddBuffer<BasicVOBufferElement>(entity).Reinterpret<byte>();

                    BasicVOComponent voComp = state.EntityManager.GetComponentData<BasicVOComponent>(entity);
                    List<BasicVOTestState> newStatesList = new List<BasicVOTestState>(tester.ElementsCount);
                    voComp.StatesListHandle = VirtualObjects.CreateObject(ref bytesBuffer, ref newStatesList);
                    for (int e = 0; e < tester.ElementsCount; e++)
                    {
                        newStatesList.Add(ref bytesBuffer, new BasicVOTestState { DebugValue = e });
                    }
                    VirtualObjects.SetObject(ref bytesBuffer, voComp.StatesListHandle, newStatesList);
                    state.EntityManager.SetComponentData(entity, voComp);
                }
                else
                {
                    state.EntityManager.AddComponentData(entity, new BasicRegularComponent());
                    DynamicBuffer<BasicVOTestState> statesBuffer = state.EntityManager.AddBuffer<BasicRegularBufferElement>(entity).Reinterpret<BasicVOTestState>();

                    for (int e = 0; e < tester.ElementsCount; e++)
                    {
                        statesBuffer.Add(new BasicVOTestState
                        {
                            DebugValue = e,
                        });
                    }
                }
            }

            tester._hasInitialized = true;
        }

        BasicVOTestSystemJob jobVO = new BasicVOTestSystemJob
        {
        };
        state.Dependency = jobVO.Schedule(state.Dependency);

        BasicRegularTestSystemJob jobReg = new BasicRegularTestSystemJob
        {
        };
        state.Dependency = jobReg.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct BasicVOTestSystemJob : IJobEntity
    {
        void Execute(Entity entity, ref BasicVOComponent voComp, ref DynamicBuffer<BasicVOBufferElement> voBuffer)
        {
            voComp.Sum = 0;
            DynamicBuffer<byte> bytesBuffer = voBuffer.Reinterpret<byte>();
            if (VirtualObjects.GetObjectCopy(ref bytesBuffer, voComp.StatesListHandle, out List<BasicVOTestState> statesList))
            {
                UnsafeList<BasicVOTestState> readOnlyList = statesList.AsReadOnlyUnsafeList(ref bytesBuffer);
                for (int i = 0; i < readOnlyList.Length; i++)
                {
                    voComp.Sum += readOnlyList[i].DebugValue;
                    //voComp.Sum += statesList.GetElementAt(ref bytesBuffer, i).DebugValue;
                }
            }
        }
    }

    [BurstCompile]
    public partial struct BasicRegularTestSystemJob : IJobEntity
    {
        void Execute(Entity entity, ref BasicRegularComponent regComp, ref DynamicBuffer<BasicRegularBufferElement> regBuffer)
        {
            regComp.Sum = 0;
            for (int i = 0; i < regBuffer.Length; i++)
            {
                regComp.Sum += regBuffer[i].State.DebugValue;
            }
        }
    }
}