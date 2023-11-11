using Trove.PolymorphicElements;
using Unity.Burst;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

[BurstCompile]
[UpdateBefore(typeof(EndFrameSystem))]
public partial struct StateMachineSystem : ISystem
{
    private bool HasInitialized;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<StateMachineTests>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        StateMachineTests singleton = SystemAPI.GetSingleton<StateMachineTests>();

        // Create state machines
        if(!HasInitialized)
        {
            const float spacing = 2f;
            Random random = Random.CreateFromIndex(1);
            int resolution = (int)math.ceil(math.sqrt(singleton.StateMachinesCount));

            for (int i = 0; i < singleton.StateMachinesCount; i++)
            {
                Entity entity = state.EntityManager.Instantiate(singleton.StateMachinePrefab);

                // Transform
                int row = i / resolution;
                int column = i % resolution;
                state.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(new float3(column * spacing, row * spacing, 0f)));

                // Initialize State Machine
                {
                    ref MyStateMachine stateMachine = ref SystemAPI.GetComponentLookup<MyStateMachine>(false).GetRefRW(entity).ValueRW;

                    // Randomize state machine
                    stateMachine.Speed = random.NextFloat(0.5f, 3f);

                    StateMachineData data = new StateMachineData
                    {
                        Time = SystemAPI.Time,
                        LocalTransform = SystemAPI.GetComponentLookup<LocalTransform>(false).GetRefRW(entity),
                        EmissionColor = SystemAPI.GetComponentLookup<URPMaterialPropertyEmissionColor>(false).GetRefRW(entity),
                        StateElementBuffer = SystemAPI.GetBuffer<StateElement>(entity).Reinterpret<byte>(),
                        StateMetaDataBuffer = SystemAPI.GetBuffer<StateMetaData>(entity),
                    };
                    for (int s = 0; s < data.StateMetaDataBuffer.Length; s++)
                    {
                        IStateManager.Execute_OnStateMachineInitialize(ref data.StateElementBuffer, data.StateMetaDataBuffer[s].Value.StartByteIndex, out _, ref random, ref stateMachine, ref data);
                    }
                    MyStateMachine.TransitionToState(stateMachine.StartStateIndex, ref stateMachine, ref data);
                }

            }

            HasInitialized = true;
        }

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

        public void Execute(
            RefRW<MyStateMachine> sm, 
            RefRW<LocalTransform> localTransform, 
            RefRW<URPMaterialPropertyEmissionColor> emissionColor,
            DynamicBuffer<StateElement> stateElementBuffer,
            DynamicBuffer<StateMetaData> stateMetaDataBuffer)
        {
            // Build data
            StateMachineData data = new StateMachineData
            {
                Time = TimeData,
                LocalTransform = localTransform,
                EmissionColor = emissionColor,
                StateElementBuffer = stateElementBuffer.Reinterpret<byte>(),
                StateMetaDataBuffer = stateMetaDataBuffer,
            };

            // Update current state
            IStateManager.Execute_OnUpdate(ref data.StateElementBuffer, sm.ValueRW.CurrentStateByteStartIndex, out _, sm.ValueRO.Speed, ref sm.ValueRW, ref data);
        }
    }
}