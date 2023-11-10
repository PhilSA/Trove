using Trove.PolymorphicElements;
using Unity.Burst;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
[UpdateBefore(typeof(EndFrameSystem))]
public partial struct StateMachineSystem : ISystem
{
    private bool HasInitialized;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PolymorphicElementsTests>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        PolymorphicElementsTests singleton = SystemAPI.GetSingleton<PolymorphicElementsTests>();
        if (!singleton.EnableStateMachineTest)
            return;

        // Create state machines
        if(!HasInitialized)
        {
            Random random = Random.CreateFromIndex(1);
            EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged);
            for (int i = 0; i < singleton.StateMachinesCount; i++)
            {
                const float spacing = 3f;

                Entity entity = ecb.Instantiate(singleton.StateMachinePrefab);

                // Transform
                int resolution = (int)math.ceil(math.sqrt(singleton.StateMachinesCount));
                int row = i / resolution;
                int column = i % resolution;
                ecb.SetComponent(entity, LocalTransform.FromPosition(new float3(column * spacing, row * spacing, 0f)));

                MyStateMachine.Create(ecb, entity, ref random);
            }

            HasInitialized = true;
        }

        StateMachineData data = new StateMachineData
        {
            Time = SystemAPI.Time,
        };

        var job = new StateMachineJob
        {
            TimeData = SystemAPI.Time,
        };
        state.Dependency = job.ScheduleParallel(state.Dependency);
        state.Dependency.Complete();
    }

    [BurstCompile]
    public partial struct StateMachineJob : IJobEntity
    {
        public TimeData TimeData;
        public StateMachineData Data;

        public void Execute(
            RefRW<MyStateMachine> sm, 
            RefRW<LocalTransform> localTransform, 
            DynamicBuffer<StateElement> stateElementBuffer, 
            DynamicBuffer<StateMetaData> stateMetadataBuffer)
        {
            // Build data
            StateMachineData Data = new StateMachineData
            {
                Time = TimeData,
                LocalTransform = localTransform,
                MyStateMachine = sm,
                StateElementBuffer = stateElementBuffer.Reinterpret<byte>(),
                StateMetadataBuffer = stateMetadataBuffer,
            };

            // Initial state
            if(sm.ValueRO.CurrentStateIndex < 0)
            {
                MyStateMachine.TransitionToState(0, ref Data);
            }

            // Update current state
            if (MyStateMachine.GetStateByteIndex(sm.ValueRO.CurrentStateIndex, Data.StateElementBuffer, stateMetadataBuffer, out int stateByteIndex))
            {
                int tmpIndex = stateByteIndex;
                IStateManager.Execute_OnUpdate(ref Data.StateElementBuffer, ref tmpIndex, ref Data);
            }
        }
    }
}