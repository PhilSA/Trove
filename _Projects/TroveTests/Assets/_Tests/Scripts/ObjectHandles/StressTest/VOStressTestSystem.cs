using Trove.Attributes;
using Trove.ObjectHandles;
using Unity.Burst;
using Unity.Entities;

public struct ChangingStat : IComponentData
{ }

public struct ExampleStatOwner : IComponentData
{
    public VirtualObjectHandle<Stat> Strength;
    public VirtualObjectHandle<Stat> Dexterity;
    public VirtualObjectHandle<Stat> Intelligence;

    public VirtualListHandle<DirtyStat> DirtyStatsList;
}

partial struct VOStressTestSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<VOStressTest>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // TEST
        //Entity voTestEntity = SystemAPI.GetSingletonEntity<VOStressTest>();
        //DynamicBuffer<byte> testStatBuffer = state.EntityManager.AddBuffer<StatVOBuffer>(voTestEntity).Reinterpret<byte>();
        //VirtualObjectManager.Initialize(ref testStatBuffer, 16, 256);
        //VirtualListHandle<StatModifier> statModifiersHandle = VirtualList<StatModifier>.Allocate(ref testStatBuffer, 10);

        
        ref VOStressTest voTest = ref SystemAPI.GetSingletonRW<VOStressTest>().ValueRW;
        BufferLookup<StatVOBuffer> statVOBufferLookup = SystemAPI.GetBufferLookup<StatVOBuffer>(false);

        if (!voTest.HasInitialized)
        {
            for (int i = 0; i < voTest.UnchangingAttributesCount; i++)
            {
                CreateStatsEntity(state.EntityManager, out _);
            }

            for (int i = 0; i < voTest.ChangingAttributesCount; i++)
            {
                Entity observedEntity = CreateStatsEntity(state.EntityManager, out ExampleStatOwner observedStatOwner);
                state.EntityManager.AddComponentData(observedEntity, new ChangingStat());

                for (int j = 0; j < voTest.ChangingAttributesChildDepth; j++)
                {
                    Entity observerEntity = CreateStatsEntity(state.EntityManager, out ExampleStatOwner observerStatOwner);

                    statVOBufferLookup = SystemAPI.GetBufferLookup<StatVOBuffer>(false);

                    StatUtility.TryAddModifier(
                        new StatReference(observerEntity, observerStatOwner.Strength),
                        new StatModifier
                        {
                            Type = StatModifier.ModifierType.AddFromStat,
                            StatA = new StatReference(observedEntity, observedStatOwner.Strength),
                        },
                        ref statVOBufferLookup);

                    observedEntity = observerEntity;
                    observedStatOwner = observerStatOwner;
                }
            }

            voTest.HasInitialized = true;
        }

        float deltaTime = SystemAPI.Time.DeltaTime;
        statVOBufferLookup = SystemAPI.GetBufferLookup<StatVOBuffer>(false);
        foreach (var (statOwner, entity) in SystemAPI.Query<ExampleStatOwner>().WithAll<ChangingStat>().WithEntityAccess())
        {
            StatUtility.TryAddBaseValue(
                new StatReference(entity, statOwner.Strength),
                deltaTime,
                ref statVOBufferLookup);
        }

        //state.Dependency = new ChangingStatsJob
        //{
        //    DeltaTime = SystemAPI.Time.DeltaTime,
        //    StatVOBufferLookup = ,
        //}.Schedule(state.Dependency);

        //state.Dependency = new UpdateStatROValuesJob
        //{ }.ScheduleParallel(state.Dependency);

    }

    private Entity CreateStatsEntity(EntityManager entityManager, out ExampleStatOwner statOwner)
    {
        statOwner = default;
        Entity entity = entityManager.CreateEntity();
        entityManager.AddBuffer<StatValueRO>(entity);
        DynamicBuffer<byte> statVOBuffer = entityManager.AddBuffer<StatVOBuffer>(entity).Reinterpret<byte>();
        VirtualObjectManager.Initialize(ref statVOBuffer, 16, 256);

        VirtualListHandle<DirtyStat> dirtyStatsList = VirtualList<DirtyStat>.Allocate(ref statVOBuffer, 10);
        statOwner = new ExampleStatOwner
        {
            Strength = VirtualObjectManager.CreateObject(ref statVOBuffer, new Stat(0, 10f, dirtyStatsList, ref statVOBuffer)),
            Dexterity = VirtualObjectManager.CreateObject(ref statVOBuffer, new Stat(1, 10f, dirtyStatsList, ref statVOBuffer)),
            Intelligence = VirtualObjectManager.CreateObject(ref statVOBuffer, new Stat(2, 10f, dirtyStatsList, ref statVOBuffer)),

            DirtyStatsList = dirtyStatsList,
        };
        entityManager.AddComponentData(entity, statOwner);

        return entity;
    }

    [BurstCompile]
    public partial struct UpdateStatROValuesJob : IJobEntity
    {
        void Execute(in ExampleStatOwner statOwner, ref DynamicBuffer<StatVOBuffer> statVOBuffer, ref DynamicBuffer<StatValueRO> statValueROBuffer)
        {
            DynamicBuffer<byte> statBuffer = statVOBuffer.Reinterpret<byte>();
            StatUtility.TransferDirtyStatsToStatValues(statOwner.DirtyStatsList, ref statBuffer, ref statValueROBuffer);
        }
    }

    [BurstCompile]
    public partial struct ChangingStatsJob : IJobEntity
    {
        public float DeltaTime;
        public BufferLookup<StatVOBuffer> StatVOBufferLookup;

        void Execute(Entity entity, in ExampleStatOwner statOwner, in ChangingStat changingStat)
        {
            StatUtility.TryAddBaseValue(
                new StatReference(entity, statOwner.Strength),
                DeltaTime,
                ref StatVOBufferLookup);
        }
    }
}
