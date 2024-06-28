//using Trove;
//using Trove.Attributes;
//using Trove.ObjectHandles;
//using Unity.Burst;
//using Unity.Burst.Intrinsics;
//using Unity.Collections;
//using Unity.Collections.LowLevel.Unsafe;
//using Unity.Entities;
//using Unity.Logging;
//using static UnityEngine.Rendering.DebugUI;

////public struct StatVOBuffer : IBufferElementData
////{
////    public byte Data;
////}

//public struct ChangingStat : IComponentData
//{ }

//public struct ExampleStatOwner : IComponentData
//{
//    public float Test;

//    public VirtualObjectHandle<Stat> Strength;
//    public VirtualObjectHandle<Stat> Dexterity;
//    public VirtualObjectHandle<Stat> Intelligence;

//    public VirtualListHandle<DirtyStat> DirtyStatsList;
//}

//partial struct VOStressTestSystem : ISystem
//{
//    private NativeList<byte> _statsBuffer;

//    [BurstCompile]
//    public void OnCreate(ref SystemState state)
//    {
//        state.RequireForUpdate<VOStressTest>();
//        _statsBuffer = new NativeList<byte>(100000, Allocator.Persistent);
//        NativeListVirtualObjectView voView = new NativeListVirtualObjectView(ref _statsBuffer);
//        VirtualObjectManager.Initialize(ref voView, 10000000);
//    }

//    [BurstCompile]
//    public void OnDestroy(ref SystemState state)
//    {
//        if (_statsBuffer.IsCreated)
//        {
//            _statsBuffer.Dispose();
//        }
//    }

//    [BurstCompile]
//    public void OnUpdate(ref SystemState state)
//    {
//        ref VOStressTest voTest = ref SystemAPI.GetSingletonRW<VOStressTest>().ValueRW;
//        //BufferLookup<StatVOBuffer> statVOBufferLookup = SystemAPI.GetBufferLookup<StatVOBuffer>(false);

//        if (!voTest.HasInitialized)
//        {
//            state.EntityManager.CompleteAllTrackedJobs();
            
//            NativeListVirtualObjectView voView = new NativeListVirtualObjectView(ref _statsBuffer);

//            for (int i = 0; i < voTest.UnchangingAttributesCount; i++)
//            {
//                CreateStatsEntity(state.EntityManager, ref voView, out _);
//            }

//            for (int i = 0; i < voTest.ChangingAttributesCount; i++)
//            {
//                Entity observedEntity = CreateStatsEntity(state.EntityManager, ref voView, out ExampleStatOwner observedStatOwner);
//                state.EntityManager.AddComponentData(observedEntity, new ChangingStat());

//                StatUtility.TryAddModifier(
//                    ref voView,
//                    new StatReference(observedEntity, observedStatOwner.Strength),
//                    new StatModifier
//                    {
//                        Type = StatModifier.ModifierType.Add,
//                        ValueA = 1f,
//                    });

//                for (int j = 0; j < voTest.ChangingAttributesChildDepth; j++)
//                {
//                    Entity observerEntity = CreateStatsEntity(state.EntityManager, ref voView, out ExampleStatOwner observerStatOwner);

//                    //statVOBufferLookup = SystemAPI.GetBufferLookup<StatVOBuffer>(false);

//                    StatUtility.TryAddModifier(
//                        ref voView,
//                        new StatReference(observerEntity, observerStatOwner.Strength),
//                        new StatModifier
//                        {
//                            Type = StatModifier.ModifierType.AddFromStat,
//                            StatA = new StatReference(observedEntity, observedStatOwner.Strength),
//                        });

//                    observedEntity = observerEntity;
//                    observedStatOwner = observerStatOwner;
//                }
//            }
            
//            voTest.HasInitialized = true;
//        }

//        //statVOBufferLookup = SystemAPI.GetBufferLookup<StatVOBuffer>(false);
//        state.Dependency = new ChangingStatsJob
//        {
//            DeltaTime = SystemAPI.Time.DeltaTime,
//            //StatVOBufferLookup = statVOBufferLookup,
//            StatsBuffer = _statsBuffer,
//        }.Schedule(state.Dependency);

//        state.Dependency = new UpdateStatROValuesJob
//        {
//            StatsBuffer = _statsBuffer,
//        }.ScheduleParallel(state.Dependency);

//        state.Dependency = new ReadVeryDirectlyJob
//        {
//            StatsBuffer = _statsBuffer,
//        }.Schedule(state.Dependency);

//        state.Dependency = new ReadDirectlyJob
//        {
//            StatsBuffer = _statsBuffer,
//        }.Schedule(state.Dependency);

//        state.Dependency = new ReadValueFromHandleJob
//        {
//            StatsBuffer = _statsBuffer,
//        }.Schedule(state.Dependency);

//        state.Dependency = new ReadValueFromResolveStatJob
//        {
//            StatsBuffer = _statsBuffer,
//        }.Schedule(state.Dependency);

//        state.Dependency = new ReadRefFromHandleJob
//        {
//            StatsBuffer = _statsBuffer,
//        }.Schedule(state.Dependency);

//        state.Dependency = new GetComponentFromLookupJob
//        {
//            TestLookup = SystemAPI.GetComponentLookup<TestStatComponent>(true),
//        }.Schedule(state.Dependency);

//        state.Dependency.Complete();
//    }

//    private Entity CreateStatsEntity<V>(EntityManager entityManager, ref V voView, out ExampleStatOwner statOwner)
//        where V : unmanaged, IVirtualObjectView
//    {
//        statOwner = default;
//        Entity entity = entityManager.CreateEntity();

//        entityManager.AddComponentData(entity, new TestStatComponent { Value = 10f });

//        DynamicBuffer<StatValueRO> statValueBuffer = entityManager.AddBuffer<StatValueRO>(entity);
//        statValueBuffer.Resize(10, NativeArrayOptions.ClearMemory);

//        //DynamicBuffer<StatVOBuffer> statVOBuffer = entityManager.AddBuffer<StatVOBuffer>(entity);


//        VirtualListHandle<DirtyStat> dirtyStatsList = VirtualListHandle<DirtyStat>.Allocate(ref voView, 10);
//        statOwner = new ExampleStatOwner
//        {
//            Strength = VirtualObjectManager.CreateObject(ref voView, Stat.Create(ref voView, 0, 10f, 10, 10, dirtyStatsList)),
//            Dexterity = VirtualObjectManager.CreateObject(ref voView, Stat.Create(ref voView, 1, 10f, 10, 10, dirtyStatsList)),
//            Intelligence = VirtualObjectManager.CreateObject(ref voView, Stat.Create(ref voView, 2, 10f, 10, 10, dirtyStatsList)),

//            DirtyStatsList = dirtyStatsList,
//        };
//        entityManager.AddComponentData(entity, statOwner);

//        return entity;
//    }

//    [BurstCompile]
//    public unsafe partial struct ReadVeryDirectlyJob : IJobEntity
//    {
//        public NativeList<byte> StatsBuffer;

//        void Execute(Entity entity, ref ExampleStatOwner statOwner, in ChangingStat changingStat)
//        {
//            byte* bufferPtr = StatsBuffer.GetUnsafePtr();
//            byte* valPtr = bufferPtr + (long)statOwner.Strength.ByteIndex;
//            Stat stat1 = *(Stat*)valPtr;
//            valPtr = bufferPtr + (long)statOwner.Strength.ByteIndex;
//            Stat stat2 = *(Stat*)valPtr;
//            valPtr = bufferPtr + (long)statOwner.Strength.ByteIndex;
//            Stat stat3 = *(Stat*)valPtr;
//            valPtr = bufferPtr + (long)statOwner.Strength.ByteIndex;
//            Stat stat4 = *(Stat*)valPtr;
//            valPtr = bufferPtr + (long)statOwner.Strength.ByteIndex;
//            Stat stat5 = *(Stat*)valPtr;

//            statOwner.Test += stat1.Value;
//            statOwner.Test += stat2.Value;
//            statOwner.Test += stat3.Value;
//            statOwner.Test += stat4.Value;
//            statOwner.Test += stat5.Value;
//        }
//    }

//    [BurstCompile]
//    public unsafe partial struct ReadDirectlyJob : IJobEntity
//    {
//        public NativeList<byte> StatsBuffer;

//        void Execute(Entity entity, ref ExampleStatOwner statOwner, in ChangingStat changingStat)
//        {
//            ByteArrayUtilities.ReadValue(StatsBuffer.GetUnsafePtr(), statOwner.Strength.ByteIndex, out Stat stat1);
//            ByteArrayUtilities.ReadValue(StatsBuffer.GetUnsafePtr(), statOwner.Strength.ByteIndex, out Stat stat2);
//            ByteArrayUtilities.ReadValue(StatsBuffer.GetUnsafePtr(), statOwner.Strength.ByteIndex, out Stat stat3);
//            ByteArrayUtilities.ReadValue(StatsBuffer.GetUnsafePtr(), statOwner.Strength.ByteIndex, out Stat stat4);
//            ByteArrayUtilities.ReadValue(StatsBuffer.GetUnsafePtr(), statOwner.Strength.ByteIndex, out Stat stat5);

//            statOwner.Test += stat1.Value;
//            statOwner.Test += stat2.Value;
//            statOwner.Test += stat3.Value;
//            statOwner.Test += stat4.Value;
//            statOwner.Test += stat5.Value;
//        }
//    }

//    [BurstCompile]
//    public partial struct ReadValueFromHandleJob : IJobEntity
//    {
//        public NativeList<byte> StatsBuffer;

//        void Execute(Entity entity, ref ExampleStatOwner statOwner, in ChangingStat changingStat)
//        {
//            NativeListVirtualObjectView voView = new NativeListVirtualObjectView(ref StatsBuffer);
//            VirtualObjectManager.TryGetObjectValue(ref voView, statOwner.Strength, out Stat stat1);
//            VirtualObjectManager.TryGetObjectValue(ref voView, statOwner.Strength, out Stat stat2);
//            VirtualObjectManager.TryGetObjectValue(ref voView, statOwner.Strength, out Stat stat3);
//            VirtualObjectManager.TryGetObjectValue(ref voView, statOwner.Strength, out Stat stat4);
//            VirtualObjectManager.TryGetObjectValue(ref voView, statOwner.Strength, out Stat stat5);

//            statOwner.Test += stat1.Value;
//            statOwner.Test += stat2.Value;
//            statOwner.Test += stat3.Value;
//            statOwner.Test += stat4.Value;
//            statOwner.Test += stat5.Value;
//        }
//    }

//    [BurstCompile]
//    public partial struct ReadValueFromResolveStatJob : IJobEntity
//    {
//        public NativeList<byte> StatsBuffer;

//        void Execute(Entity entity, ref ExampleStatOwner statOwner, in ChangingStat changingStat)
//        {
//            NativeListVirtualObjectView voView = new NativeListVirtualObjectView(ref StatsBuffer);
//            StatUtility.TryResolveStat(ref voView, new StatReference(entity, statOwner.Strength), out Stat stat1);
//            StatUtility.TryResolveStat(ref voView, new StatReference(entity, statOwner.Strength), out Stat stat2);
//            StatUtility.TryResolveStat(ref voView, new StatReference(entity, statOwner.Strength), out Stat stat3);
//            StatUtility.TryResolveStat(ref voView, new StatReference(entity, statOwner.Strength), out Stat stat4);
//            StatUtility.TryResolveStat(ref voView, new StatReference(entity, statOwner.Strength), out Stat stat5);

//            statOwner.Test += stat1.Value;
//            statOwner.Test += stat2.Value;
//            statOwner.Test += stat3.Value;
//            statOwner.Test += stat4.Value;
//            statOwner.Test += stat5.Value;
//        }
//    }

//    [BurstCompile]
//    public partial struct ReadRefFromHandleJob : IJobEntity
//    {
//        public NativeList<byte> StatsBuffer;

//        void Execute(Entity entity, ref ExampleStatOwner statOwner, in ChangingStat changingStat)
//        {
//            NativeListVirtualObjectView voView = new NativeListVirtualObjectView(ref StatsBuffer);
//            ref Stat stat1 = ref StatUtility.TryResolveStatRef(ref voView, new StatReference(entity, statOwner.Strength), out bool success);
//            ref Stat stat2 = ref StatUtility.TryResolveStatRef(ref voView, new StatReference(entity, statOwner.Strength), out success);
//            ref Stat stat3 = ref StatUtility.TryResolveStatRef(ref voView, new StatReference(entity, statOwner.Strength), out success);
//            ref Stat stat4 = ref StatUtility.TryResolveStatRef(ref voView, new StatReference(entity, statOwner.Strength), out success);
//            ref Stat stat5 = ref StatUtility.TryResolveStatRef(ref voView, new StatReference(entity, statOwner.Strength), out success);

//            statOwner.Test += stat1.Value;
//            statOwner.Test += stat2.Value;
//            statOwner.Test += stat3.Value;
//            statOwner.Test += stat4.Value;
//            statOwner.Test += stat5.Value;
//        }
//    }

//    public struct TestStatComponent : IComponentData
//    {
//        public float Value;
//    }

//    [BurstCompile]
//    public partial struct GetComponentFromLookupJob : IJobEntity
//    {
//        [ReadOnly]
//        public ComponentLookup<TestStatComponent> TestLookup;

//        void Execute(Entity entity, ref ExampleStatOwner statOwner, in ChangingStat changingStat)
//        {
//            if(TestLookup.TryGetComponent(entity, out TestStatComponent stat1) &&
//                TestLookup.TryGetComponent(entity, out TestStatComponent stat2) &&
//                TestLookup.TryGetComponent(entity, out TestStatComponent stat3) &&
//                TestLookup.TryGetComponent(entity, out TestStatComponent stat4) &&
//                TestLookup.TryGetComponent(entity, out TestStatComponent stat5))
//            {
//                statOwner.Test += stat1.Value;
//                statOwner.Test += stat2.Value;
//                statOwner.Test += stat3.Value;
//                statOwner.Test += stat4.Value;
//                statOwner.Test += stat5.Value;
//            }
//        }
//    }

//    [BurstCompile]
//    public partial struct ChangingStatsJob : IJobEntity, IJobEntityChunkBeginEnd
//    {
//        public float DeltaTime;
//        //public BufferLookup<StatVOBuffer> StatVOBufferLookup;
//        public NativeList<byte> StatsBuffer;

//        private NativeListVirtualObjectView voView;

//        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
//        {
//            if (!voView.IsCreated)
//            {
//                voView = new NativeListVirtualObjectView(ref StatsBuffer);
//            }
//            return true;
//        }

//        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
//        {
//        }

//        void Execute(Entity entity, in ExampleStatOwner statOwner, in ChangingStat changingStat)
//        {
//            StatUtility.TryAddBaseValue(
//                ref voView,
//                new StatReference(entity, statOwner.Strength),
//                DeltaTime);
//        }
//    }

//    [BurstCompile]
//    public partial struct UpdateStatROValuesJob : IJobEntity
//    {
//        [NativeDisableParallelForRestriction]
//        public NativeList<byte> StatsBuffer;

//        void Execute(in ExampleStatOwner statOwner, ref DynamicBuffer<StatValueRO> statValueROBuffer)
//        {
//            NativeListVirtualObjectView voView = new NativeListVirtualObjectView(ref StatsBuffer);
//            StatUtility.TransferDirtyStatsToStatValues(ref voView, statOwner.DirtyStatsList, ref statValueROBuffer);
//        }
//    }
//}
