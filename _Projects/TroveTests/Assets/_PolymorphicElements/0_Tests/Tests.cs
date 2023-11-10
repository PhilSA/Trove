using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using PolymorphicElements;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;


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
    public quaternion Quat2;
    public quaternion Quat3;
    public quaternion Quat4;
    public quaternion Quat5;
    public quaternion Quat6;
    public quaternion Quat7;
    public quaternion Quat8;
    public quaternion Quat9;
    
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

public static class TestParams
{
    public const int EventBatches = 333333;
}

[BurstCompile]
[UpdateBefore(typeof(EndFrameSystem))]
public partial struct TestSystem : ISystem
{
    private NativeList<byte> _elementsList;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
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
        ITestPolyGroupA_Handler handler = new ITestPolyGroupA_Handler();
        TestPolyGroupAData data = new TestPolyGroupAData
        {
            A = 0,
        };
        
        var writeJob = new TestWriteJob
        {
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
        public ITestPolyGroupA_Handler Handler;
        public NativeList<byte> ElementsList;
        
        public void Execute()
        {
            for (int i = 0; i < TestParams.EventBatches; i++)
            {
                Handler.WriteElement(ref ElementsList, new TestElementA { });
                Handler.WriteElement(ref ElementsList, new TestElementB { });
                Handler.WriteElement(ref ElementsList, new TestElementC { });
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
            while (Handler.ReadAndExecuteElementAt_Execute(ref ElementsList, ref index, ref Data))
            { }
            ElementsList.Clear();
        }
    }
}

[BurstCompile]
[UpdateBefore(typeof(EndFrameSystem))]
public partial struct TestFixedSystem : ISystem
{
    private NativeList<ITestPolyGroupA_Handler.FixedElement> _fixedElementsList;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _fixedElementsList = new NativeList<ITestPolyGroupA_Handler.FixedElement>(1000000, Allocator.Persistent);
    }
    
    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (_fixedElementsList.IsCreated) _fixedElementsList.Dispose();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        ITestPolyGroupA_Handler handler = new ITestPolyGroupA_Handler();
        TestPolyGroupAData data = new TestPolyGroupAData
        {
            A = 0,
        };
        
        var writeJob = new TestWriteFixedJob
        {
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
        public NativeList<ITestPolyGroupA_Handler.FixedElement> ElementsList;
        
        public void Execute()
        {
            for (int i = 0; i < TestParams.EventBatches; i++)
            {
                ElementsList.Add(new ITestPolyGroupA_Handler.FixedElement(new TestElementA { }));
                ElementsList.Add(new ITestPolyGroupA_Handler.FixedElement(new TestElementB { }));
                ElementsList.Add(new ITestPolyGroupA_Handler.FixedElement(new TestElementC { }));
            }
        }
    }
    
    [BurstCompile]
    public struct TesReadFixedJob : IJob
    {
        public TestPolyGroupAData Data;
        public NativeList<ITestPolyGroupA_Handler.FixedElement> ElementsList;
        
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

// [BurstCompile]
// [UpdateBefore(typeof(MainThreadEventData_System))]
// public partial struct OtherSystem : ISystem
// {
//     [BurstCompile]
//     public void OnCreate(ref SystemState state)
//     {
//         state.RequireForUpdate<MainThreadEventData_System.Singleton>();
//     }
//     
//     [BurstCompile]
//     public void OnUpdate(ref SystemState state)
//     {
//         int totalCount = 100000;
//         int threadCount = 10;
//         
//         var job = new TestWriteJob
//         {
//             CountPerThread = totalCount / threadCount,
//             Writer = SystemAPI.GetSingletonRW<MainThreadEventData_System.Singleton>().ValueRW.EventBuffer.CreateBufferWriter(threadCount),
//         };
//         state.Dependency = job.Schedule(threadCount, 1, state.Dependency);
//     }
//     
//     [BurstCompile]
//     public struct TestWriteJob : IJobParallelFor
//     {
//         public int CountPerThread;
//         public EventsHandler_MainThreadEventData.EventBuffersManager.EventBuffer.Writer Writer;
//         
//         public void Execute(int threadIndex)
//         {
//             Writer.BeginForEachIndex(threadIndex);
//             for (int i = 0; i < CountPerThread; i++)
//             {
//                 Writer.WriteEvent(new TestPolymorphicElement
//                 {
//                     Vec1 = default,
//                     Vec2 = default,
//                     Vec3 = default,
//                 });
//             }
//             Writer.EndForEachIndex();
//         }
//     }
// }
//
// [BurstCompile]
// public partial struct MainThreadEventData_System : ISystem
// {
//     private EventsHandler_MainThreadEventData.EventBuffersManager _eventBuffer;
//
//     public struct Singleton : IComponentData
//     {
//         public EventsHandler_MainThreadEventData.EventBuffersManager EventBuffer;
//     }
//     
//     [BurstCompile]
//     public void OnCreate(ref SystemState state)
//     {
//         _eventBuffer = new EventsHandler_MainThreadEventData.EventBuffersManager();
//         _eventBuffer.Allocate();
//         state.EntityManager.CreateSingleton(new Singleton { EventBuffer = _eventBuffer });
//         
//         state.RequireForUpdate<Singleton>();
//     }
//     
//     [BurstCompile]
//     public void OnDestroy(ref SystemState state)
//     {
//         _eventBuffer.Dispose();
//     }
//     
//     [BurstCompile]
//     public void OnUpdate(ref SystemState state)
//     {
//         state.Dependency.Complete();
//         
//         ref EventsHandler_MainThreadEventData.EventBuffersManager eventBuffer = ref SystemAPI.GetSingletonRW<Singleton>().ValueRW.EventBuffer;
//
//         MainThreadEventData data = new MainThreadEventData
//         { };
//         
//         eventBuffer.ProcessEvents(ref data);
//     }
//
//     [BurstCompile]
//     public struct TestReadJob : IJobParallelFor
//     {
//         public MainThreadEventData Data;
//         public EventsHandler_MainThreadEventData.EventBuffersManager.EventBuffer.Reader Reader;
//         
//         public void Execute(int threadIndex)
//         {
//             Reader.BeginForEachIndex(threadIndex);
//             while (Reader.ReadAndExecuteEvent(ref Data))
//             { }
//             Reader.EndForEachIndex();
//         }
//     }
// }