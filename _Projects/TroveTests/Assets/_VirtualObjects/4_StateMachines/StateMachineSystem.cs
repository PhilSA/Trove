using Trove.PolymorphicElements;
using Unity.Burst;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Logging;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

[BurstCompile]
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
        if (!HasInitialized)
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

                    DynamicBuffer<byte> stateElementBuffer = SystemAPI.GetBuffer<StateElement>(entity).Reinterpret<byte>();
                    StateMachineData data = new StateMachineData
                    {
                        Time = SystemAPI.Time,
                        LocalTransform = SystemAPI.GetComponentLookup<LocalTransform>(false).GetRefRW(entity),
                        EmissionColor = SystemAPI.GetComponentLookup<URPMaterialPropertyEmissionColor>(false).GetRefRW(entity),
                        StateElementsBuffer = stateElementBuffer,
                        StateMetaDataBuffer = SystemAPI.GetBuffer<StateMetaData>(entity),
                    };

                    // Initialize all states
                    int readIndex = 0;
                    bool hasFinished = false;
                    while (!hasFinished)
                    {
                        IStateManager.OnStateMachineInitialize(stateElementBuffer, readIndex, out int readSize, out hasFinished, ref random, ref stateMachine, ref data);
                        readIndex += readSize;
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
                StateElementsBuffer = stateElementBuffer.Reinterpret<byte>(),
                StateMetaDataBuffer = stateMetaDataBuffer,
            };

            // Update current state
            IStateManager.OnUpdate(data.StateElementsBuffer, sm.ValueRW.CurrentStateByteStartIndex, out _, out _, sm.ValueRO.Speed, ref sm.ValueRW, ref data);
        }
    }
} 