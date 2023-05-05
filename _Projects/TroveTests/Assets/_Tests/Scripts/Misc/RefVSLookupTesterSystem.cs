using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Trove.Tweens;
using Unity.Collections;
using Trove.UtilityAI;

public struct ConsiderationPayload
{
    public Consideration A;
}

public struct AMyTestReasoner : IComponentData
{
    public float Score;

    public Entity OneActionRef;
}

public struct AMyTestReasonerActionRef : IBufferElementData
{
    public Entity ActionEntity;
}

public struct AMyTestAction : IComponentData
{
    public Entity Reasoner;
    public float Score;
}

[InternalBufferCapacity(10)]
public struct AMyTestConsideration : IBufferElementData
{
    public float Input;

    public ConsiderationPayload Payload;
}

public struct BMyTestReasoner : IComponentData
{
    public float Score;
}

[InternalBufferCapacity(0)]
public struct BMyTestAction : IBufferElementData
{
    public float Score;
}

[InternalBufferCapacity(0)]
public struct BMyTestConsideration : IBufferElementData
{
    public float Input;
    public ConsiderationPayload Payload;
}

public struct CMyTestReasoner : IComponentData
{
    public float Score;
    public Entity OneConsiderationRef;
}

public struct CMyTestAction : IComponentData
{
    public float Score;
    public Entity TargetReasoner;
}

public struct CMyTestConsideration : IComponentData
{
    public float Input;
    public Entity TargetAction;
    public ConsiderationPayload Payload;
}

[BurstCompile]
public partial struct RefVSLookupTesterSystem : ISystem
{
    [BurstCompile]
    void OnCreate(ref SystemState state)
    {
    }

    [BurstCompile]
    void OnDestroy(ref SystemState state)
    {
    }

    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        int reasoners = 20000;
        int actionsPerReasoner = 10;
        int considerationsPerAction = 10;

        EntityCommandBuffer ecb = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>().ValueRW.CreateCommandBuffer(state.WorldUnmanaged);
        foreach (var (tester, entity) in SystemAPI.Query<RefVSLookupTester>().WithEntityAccess())
        {
            // A
            {
                for (int r = 0; r < reasoners; r++)
                {
                    Entity reasoner = ecb.CreateEntity();
                    var actionRefsBuffer = ecb.AddBuffer<AMyTestReasonerActionRef>(reasoner);

                    Entity exampleAction = default;
                    for (int a = 0; a < actionsPerReasoner; a++)
                    {
                        Entity action = ecb.CreateEntity();
                        ecb.AddComponent(action, new AMyTestAction { Reasoner = reasoner });
                        actionRefsBuffer.Add(new AMyTestReasonerActionRef { ActionEntity = action });

                        if (a == 0)
                        {
                            exampleAction = action;
                        }

                        var considerations = ecb.AddBuffer<AMyTestConsideration>(action);
                        for (int c = 0; c < considerationsPerAction; c++)
                        {
                            considerations.Add(new AMyTestConsideration());
                        }
                    }

                    ecb.AddComponent(reasoner, new AMyTestReasoner { OneActionRef = exampleAction });
                }
            }

            // B
            {
                for (int r = 0; r < reasoners; r++)
                {
                    Entity reasoner = ecb.CreateEntity();
                    ecb.AddComponent(reasoner, new BMyTestReasoner { Score = 1f });

                    var actions = ecb.AddBuffer<BMyTestAction>(reasoner);
                    for (int a = 0; a < actionsPerReasoner; a++)
                    {
                        actions.Add(new BMyTestAction { Score = 1f });
                    }

                    var considerations = ecb.AddBuffer<BMyTestConsideration>(reasoner);
                    for (int c = 0; c < actionsPerReasoner * considerationsPerAction; c++)
                    {
                        considerations.Add(new BMyTestConsideration());
                    }
                }
            }

            // C
            {
                for (int r = 0; r < reasoners; r++)
                {
                    Entity reasoner = ecb.CreateEntity();

                    Entity oneCons = default;

                    for (int a = 0; a < actionsPerReasoner; a++)
                    {
                        Entity action = ecb.CreateEntity();
                        ecb.AddComponent(action, new CMyTestAction { TargetReasoner = reasoner });

                        for (int c = 0; c < considerationsPerAction; c++)
                        {
                            Entity consideration = ecb.CreateEntity();
                            ecb.AddComponent(consideration, new CMyTestConsideration { TargetAction = action });

                            oneCons = consideration;
                        }
                    }

                    ecb.AddComponent(reasoner, new CMyTestReasoner { OneConsiderationRef = oneCons });
                }
            }

            ecb.DestroyEntity(entity);
        }

        // A
        {
            JobA_Inputs JobA_Inputs = new JobA_Inputs
            {
                ActionsPerReasoner = actionsPerReasoner,
                ConsPerAction = considerationsPerAction,
                CLookup = SystemAPI.GetBufferLookup<AMyTestConsideration>(false),
            };
            state.Dependency = JobA_Inputs.ScheduleParallel(state.Dependency);

            JobA_ConsToAction JobA_ConsToAction = new JobA_ConsToAction
            {
            };
            state.Dependency = JobA_ConsToAction.ScheduleParallel(state.Dependency);

            JobA_ActionToReasoner JobA_ActionToReasoner = new JobA_ActionToReasoner
            {
                ALookup = SystemAPI.GetComponentLookup<AMyTestAction>(true),
            };
            state.Dependency = JobA_ActionToReasoner.ScheduleParallel(state.Dependency);
        }

        // B
        {
            JobB JobB = new JobB
            {
            };
            state.Dependency = JobB.ScheduleParallel(state.Dependency);
        }

        // C
        {
            JobC_Inputs JobC_Inputs = new JobC_Inputs
            {
                TotalConsiderations = actionsPerReasoner * considerationsPerAction,
                CLookup = SystemAPI.GetComponentLookup<CMyTestConsideration>(false),
            };
            state.Dependency = JobC_Inputs.ScheduleParallel(state.Dependency);

            JobC_ConsToAction JobC_ConsToAction = new JobC_ConsToAction
            {
                ALookup = SystemAPI.GetComponentLookup<CMyTestAction>(false),
            };
            state.Dependency = JobC_ConsToAction.ScheduleParallel(state.Dependency);

            JobC_ActionToReasoner JobC_ActionToReasoner = new JobC_ActionToReasoner
            {
                RLookup = SystemAPI.GetComponentLookup<CMyTestReasoner>(false),
            };
            state.Dependency = JobC_ActionToReasoner.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct JobA_Inputs : IJobEntity
    {
        public int ActionsPerReasoner;
        public int ConsPerAction;
        [NativeDisableParallelForRestriction]
        public BufferLookup<AMyTestConsideration> CLookup;

        void Execute(in AMyTestReasoner reasoner)
        {
            for (int i = 0; i < ActionsPerReasoner; i++)
            {
                if (CLookup.TryGetBuffer(reasoner.OneActionRef, out DynamicBuffer<AMyTestConsideration> consBuffer))
                {
                    for (int c = 0; c < ConsPerAction; c++)
                    {
                        var cons = consBuffer[c];
                        cons.Input = 0.5f;
                        consBuffer[c] = cons;
                    }
                }
            }
        }
    }

    [BurstCompile]
    public partial struct JobA_ConsToAction : IJobEntity
    {
        void Execute(ref AMyTestAction action, ref DynamicBuffer<AMyTestConsideration> consBuffer)
        {
            action.Score = 1f;
            for (int i = 0; i < consBuffer.Length; i++)
            {
                action.Score *= consBuffer[i].Input;
            }
        }
    }

    [BurstCompile]
    public partial struct JobA_ActionToReasoner : IJobEntity
    {
        [ReadOnly]
        public ComponentLookup<AMyTestAction> ALookup;

        void Execute(ref AMyTestReasoner reasoner, in DynamicBuffer<AMyTestReasonerActionRef> actionRefs)
        {
            reasoner.Score = 1f;
            for (int i = 0; i < actionRefs.Length; i++)
            {
                if(ALookup.TryGetComponent(actionRefs[i].ActionEntity, out AMyTestAction action))
                {
                    reasoner.Score *= action.Score;
                }
            }
        }
    }

    [BurstCompile]
    public partial struct JobB : IJobEntity
    {
        void Execute(ref BMyTestReasoner reasoner, ref DynamicBuffer<BMyTestAction> actionsBuffer, ref DynamicBuffer<BMyTestConsideration> consBuffer)
        {
            // Inputs
            for (int i = 0; i < consBuffer.Length; i++)
            {
                var con = consBuffer[i];
                con.Input = 0.6f;
                consBuffer[i] = con;
            }

            // Reset Actions
            for (int i = 0; i < actionsBuffer.Length; i++)
            {
                var action = actionsBuffer[0];
                action.Score = 1f;
                actionsBuffer[0] = action;
            }

            // Cons to Actions
            for (int i = 0; i < consBuffer.Length; i++)
            {
                var action = actionsBuffer[0];
                action.Score *= consBuffer[i].Input;
                actionsBuffer[0] = action;
            }

            // Actions to Reasoner
            reasoner.Score = 1f;
            for (int i = 0; i < actionsBuffer.Length; i++)
            {
                reasoner.Score *= actionsBuffer[i].Score;
            }
        }
    }

    [BurstCompile]
    public partial struct JobC_Inputs : IJobEntity
    {
        public int TotalConsiderations;
        [NativeDisableParallelForRestriction]
        public ComponentLookup<CMyTestConsideration> CLookup;

        void Execute(in CMyTestReasoner reasoner)
        {
            for (int i = 0; i < TotalConsiderations; i++)
            {
                if (CLookup.TryGetComponent(reasoner.OneConsiderationRef, out CMyTestConsideration cons))
                {
                    cons.Input = 0.5f;
                    CLookup[reasoner.OneConsiderationRef] = cons;
                }
            }
        }
    }

    [BurstCompile]
    public partial struct JobC_ConsToAction : IJobEntity
    {
        [NativeDisableParallelForRestriction]
        public ComponentLookup<CMyTestAction> ALookup;

        void Execute(in CMyTestConsideration cons)
        {
            if (ALookup.TryGetComponent(cons.TargetAction, out CMyTestAction action))
            {
                action.Score *= cons.Input;
                ALookup[cons.TargetAction] = action;
            }
        }
    }

    [BurstCompile]
    public partial struct JobC_ActionToReasoner : IJobEntity
    {
        [NativeDisableParallelForRestriction]
        public ComponentLookup<CMyTestReasoner> RLookup;

        void Execute(in CMyTestAction action)
        {
            if (RLookup.TryGetComponent(action.TargetReasoner, out CMyTestReasoner reasoner))
            {
                reasoner.Score *= action.Score;
                RLookup[action.TargetReasoner] = reasoner;
            }
        }
    }
}