using System.Collections;
using System.Collections.Generic;
using Trove.UtilityAI;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public enum StressTestAIAction
{
    A0,
    A1,
    A2,
    A3,
    A4,
    A5,
    A6,
    A7,
    A8,
    A9,
}

[BurstCompile]
public partial struct StressTestAISystem : ISystem
{
    private int FrameCounter;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    { }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        FrameCounter++; 

        // Test Setup
        TestSetupJob setupJob = new TestSetupJob
        {
            ECB = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged),
        };
        state.Dependency = setupJob.Schedule(state.Dependency);

        // Update inputs
        AIInputsJob inputsJob = new AIInputsJob
        {
            ElapsedTime = (float)SystemAPI.Time.ElapsedTime,
            Input = math.saturate((math.sin((float)SystemAPI.Time.ElapsedTime) + 1f) * 0.5f),
        };
        state.Dependency = inputsJob.ScheduleParallel(state.Dependency);

        // Update the AI
        AIUpdateJob updateJob = new AIUpdateJob
        {
        };
        state.Dependency = updateJob.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public partial struct TestSetupJob : IJobEntity
    {
        public EntityCommandBuffer ECB;

        void Execute(Entity entity, ref StressTestAIConfig test)
        {
            for (int i = 0; i < test.SpawnCount; i++)
            {
                ECB.Instantiate(test.Prefab);
            }
            ECB.DestroyEntity(entity);
        }
    }

    [BurstCompile]
    public partial struct AIInputsJob : IJobEntity
    {
        public float ElapsedTime;
        public float Input;

        void Execute(Entity entity, [EntityIndexInQuery] int entityIndexInQuery, ref StressTestAI test, ref Reasoner reasoner, ref DynamicBuffer<Action> actions, ref DynamicBuffer<Consideration> considerations, ref DynamicBuffer<ConsiderationInput> considerationInputs)
        {
            //if (entityIndexInQuery % test.UpdateEveryXTick == 0)
            {
                ReasonerUtilities.SetConsiderationInput(ref test.A0C0Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A0C1Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A0C2Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A0C3Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A0C4Ref, Input, in reasoner, considerations, considerationInputs);

                ReasonerUtilities.SetConsiderationInput(ref test.A1C0Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A1C1Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A1C2Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A1C3Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A1C4Ref, Input, in reasoner, considerations, considerationInputs);

                ReasonerUtilities.SetConsiderationInput(ref test.A2C0Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A2C1Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A2C2Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A2C3Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A2C4Ref, Input, in reasoner, considerations, considerationInputs);

                ReasonerUtilities.SetConsiderationInput(ref test.A3C0Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A3C1Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A3C2Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A3C3Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A3C4Ref, Input, in reasoner, considerations, considerationInputs);

                ReasonerUtilities.SetConsiderationInput(ref test.A4C0Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A4C1Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A4C2Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A4C3Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A4C4Ref, Input, in reasoner, considerations, considerationInputs);

                ReasonerUtilities.SetConsiderationInput(ref test.A5C0Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A5C1Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A5C2Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A5C3Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A5C4Ref, Input, in reasoner, considerations, considerationInputs);

                ReasonerUtilities.SetConsiderationInput(ref test.A6C0Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A6C1Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A6C2Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A6C3Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A6C4Ref, Input, in reasoner, considerations, considerationInputs);

                ReasonerUtilities.SetConsiderationInput(ref test.A7C0Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A7C1Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A7C2Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A7C3Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A7C4Ref, Input, in reasoner, considerations, considerationInputs);

                ReasonerUtilities.SetConsiderationInput(ref test.A8C0Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A8C1Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A8C2Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A8C3Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A8C4Ref, Input, in reasoner, considerations, considerationInputs);

                ReasonerUtilities.SetConsiderationInput(ref test.A9C0Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A9C1Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A9C2Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A9C3Ref, Input, in reasoner, considerations, considerationInputs);
                ReasonerUtilities.SetConsiderationInput(ref test.A9C4Ref, Input, in reasoner, considerations, considerationInputs);
            }
        }
    }

    [BurstCompile]
    public partial struct AIUpdateJob : IJobEntity
    {
        void Execute(Entity entity, [EntityIndexInQuery] int entityIndexInQuery, ref StressTestAI test, ref Reasoner reasoner, ref DynamicBuffer<Action> actions, ref DynamicBuffer<Consideration> considerations, ref DynamicBuffer<ConsiderationInput> considerationInputs)
        {
            //if (entityIndexInQuery % test.UpdateEveryXTick == 0)
            {
                // Update reasoner
                ActionSelectors.HighestScoring actionSelector = new ActionSelectors.HighestScoring();
                ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelector, ref reasoner, actions, considerations, considerationInputs, out Action selectedAction);
            }
        }
    }
}
