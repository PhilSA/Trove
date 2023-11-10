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
            const float spacing = 3f;
            Random random = Random.CreateFromIndex(1);
            int resolution = (int)math.ceil(math.sqrt(singleton.StateMachinesCount));

            for (int i = 0; i < singleton.StateMachinesCount; i++)
            {
                Entity entity = state.EntityManager.Instantiate(singleton.StateMachinePrefab);

                // Transform
                int row = i / resolution;
                int column = i % resolution;
                state.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(new float3(column * spacing, row * spacing, 0f)));

                // Randomize state machine
                MyStateMachine sm = state.EntityManager.GetComponentData<MyStateMachine>(entity);
                sm.Speed = random.NextFloat(0.5f, 3f);
                state.EntityManager.SetComponentData(entity, sm);

                // Randomize states
                {
                    var statesBuffer = SystemAPI.GetBuffer<StateElement>(entity).Reinterpret<byte>();

                    ref MoveState moveState = ref PolymorphicElementsUtility.ReadElementAsRef<MoveState>(ref statesBuffer, sm.MoveStateData.StartByteIndex, out _, out bool success);
                    if (success)
                    {
                        moveState.Movement = random.NextFloat3(new float3(3f));
                    }

                    ref RotateState rotateState = ref PolymorphicElementsUtility.ReadElementAsRef<RotateState>(ref statesBuffer, sm.RotateStateData.StartByteIndex, out _, out success);
                    if (success)
                    {
                        rotateState.RotationSpeed = random.NextFloat3(new float3(1f));
                    }

                    ref ScaleState scaleState = ref PolymorphicElementsUtility.ReadElementAsRef<ScaleState>(ref statesBuffer, sm.ScaleStateData.StartByteIndex, out _, out success);
                    if (success)
                    {
                        scaleState.AddedScale = random.NextFloat(3f);
                    }
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
            DynamicBuffer<StateElement> stateElementBuffer)
        {
            // Build data
            StateMachineData data = new StateMachineData
            {
                Time = TimeData,
                LocalTransform = localTransform,
                MyStateMachine = sm,
                StateElementBuffer = stateElementBuffer.Reinterpret<byte>(),
            };

            // Update current state
            MyStateMachine.Update(ref data);
        }
    }
}