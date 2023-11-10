using System;
using System.Runtime.InteropServices;
using Trove.PolymorphicElements;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[Serializable]
public struct PolymorphicElementsTests : IComponentData
{
    public int StresTestBatches;

}

public struct TestPolyGroupAData
{
    public int A;
}

[PolymorphicElementsGroup]
public interface ITestPolyGroupA
{
    void Init();
    void Execute(ref TestPolyGroupAData data);
    void Execute2(ref TestPolyGroupAData data, int i);
    void Execute3(ref TestPolyGroupAData data, int i, in NativeList<byte> elems);
}

[PolymorphicElement]
public struct TestElementA : ITestPolyGroupA
{
    public void Init()
    {

    }

    public void Execute(ref TestPolyGroupAData data)
    {
        data.A++;
    }

    public void Execute2(ref TestPolyGroupAData data, int i)
    {

    }

    public void Execute3(ref TestPolyGroupAData data, int i, in NativeList<byte> elems)
    {

    }
}

[PolymorphicElement]
public struct TestElementB : ITestPolyGroupA
{
    public Entity Entity;

    public void Init()
    {

    }

    public void Execute(ref TestPolyGroupAData data)
    {
    }

    public void Execute2(ref TestPolyGroupAData data, int i)
    {

    }

    public void Execute3(ref TestPolyGroupAData data, int i, in NativeList<byte> elems)
    {

    }
}

[PolymorphicElement]
public struct TestElementC : ITestPolyGroupA
{
    public float3 Vec1;
    public quaternion Quat1;

    public void Init()
    {

    }

    public void Execute(ref TestPolyGroupAData data)
    {
    }

    public void Execute2(ref TestPolyGroupAData data, int i)
    {

    }

    public void Execute3(ref TestPolyGroupAData data, int i, in NativeList<byte> elems)
    {

    }
}


[BurstCompile]
[UpdateBefore(typeof(EndFrameSystem))]
public partial struct TestSystem : ISystem
{
    private NativeList<byte> _elementsList;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PolymorphicElementsTests>();
        _elementsList = new NativeList<byte>(1000000, Allocator.Persistent);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (_elementsList.IsCreated) _elementsList.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        PolymorphicElementsTests singleton = SystemAPI.GetSingleton<PolymorphicElementsTests>();
        ITestPolyGroupA_Handler handler = new ITestPolyGroupA_Handler();
        TestPolyGroupAData data = new TestPolyGroupAData
        {
            A = 0,
        };

        var writeJob = new TestWriteJob
        {
            Singleton = singleton,
            Handler = handler,
            ElementsList = _elementsList,
        };
        state.Dependency = writeJob.Schedule(state.Dependency);

        var readJob = new TesReadJob
        {
            Handler = handler,
            Data = data,
            ElementsList = _elementsList,
        };
        state.Dependency = readJob.Schedule(state.Dependency);
    }

    [BurstCompile]
    public struct TestWriteJob : IJob
    {
        public PolymorphicElementsTests Singleton;
        public ITestPolyGroupA_Handler Handler;
        public NativeList<byte> ElementsList;

        public void Execute()
        {
            for (int i = 0; i < Singleton.StresTestBatches; i++)
            {
                Handler.AddElement(ref ElementsList, new TestElementA { });
                Handler.AddElement(ref ElementsList, new TestElementB { });
                Handler.AddElement(ref ElementsList, new TestElementC { });
            }
        }
    }

    [BurstCompile]
    public struct TesReadJob : IJob
    {
        public ITestPolyGroupA_Handler Handler;
        public TestPolyGroupAData Data;
        public NativeList<byte> ElementsList;

        public void Execute()
        {
            int index = 0;
            while (Handler.ExecuteElement_Execute(ref ElementsList, ref index, ref Data))
            { }
            ElementsList.Clear();
        }
    }
}

[BurstCompile]
[UpdateBefore(typeof(EndFrameSystem))]
public partial struct TestFixedSystem : ISystem
{
    private NativeList<ITestPolyGroupA_Handler.UnionElement> _fixedElementsList;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PolymorphicElementsTests>();
        _fixedElementsList = new NativeList<ITestPolyGroupA_Handler.UnionElement>(1000000, Allocator.Persistent);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (_fixedElementsList.IsCreated) _fixedElementsList.Dispose();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        PolymorphicElementsTests singleton = SystemAPI.GetSingleton<PolymorphicElementsTests>();
        ITestPolyGroupA_Handler handler = new ITestPolyGroupA_Handler();
        TestPolyGroupAData data = new TestPolyGroupAData
        {
            A = 0,
        };

        var writeJob = new TestWriteFixedJob
        {
            Singleton = singleton,
            ElementsList = _fixedElementsList,
        };
        state.Dependency = writeJob.Schedule(state.Dependency);

        var readJob = new TesReadFixedJob
        {
            Data = data,
            ElementsList = _fixedElementsList,
        };
        state.Dependency = readJob.Schedule(state.Dependency);
    }

    [BurstCompile]
    public struct TestWriteFixedJob : IJob
    {
        public PolymorphicElementsTests Singleton;
        public NativeList<ITestPolyGroupA_Handler.UnionElement> ElementsList;

        public void Execute()
        {
            for (int i = 0; i < Singleton.StresTestBatches; i++)
            {
                ElementsList.Add(new ITestPolyGroupA_Handler.UnionElement(new TestElementA { }));
                ElementsList.Add(new ITestPolyGroupA_Handler.UnionElement(new TestElementB { }));
                ElementsList.Add(new ITestPolyGroupA_Handler.UnionElement(new TestElementC { }));
            }
        }
    }

    [BurstCompile]
    public struct TesReadFixedJob : IJob
    {
        public TestPolyGroupAData Data;
        public NativeList<ITestPolyGroupA_Handler.UnionElement> ElementsList;

        public void Execute()
        {
            for (int i = 0; i < ElementsList.Length; i++)
            {
                ElementsList[i].Execute_Execute(ref Data);
            }
            ElementsList.Clear();
        }
    }
}


[BurstCompile]
public partial struct EndFrameSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.EntityManager.CompleteAllTrackedJobs();
    }
}
