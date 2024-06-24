using Trove.Attributes;
using Trove.ObjectHandles;
using Unity.Burst;
using Unity.Entities;
using Unity.Logging;

public struct StatVOBuffer : IBufferElementData
{
    public byte Data;
}

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

        statVOBufferLookup = SystemAPI.GetBufferLookup<StatVOBuffer>(false);
        state.Dependency = new ChangingStatsJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            StatVOBufferLookup = statVOBufferLookup,
        }.Schedule(state.Dependency);

        state.Dependency = new UpdateStatROValuesJob
        { }.ScheduleParallel(state.Dependency);

    }

    private Entity CreateStatsEntity(EntityManager entityManager, out ExampleStatOwner statOwner)
    {
        statOwner = default;
        Entity entity = entityManager.CreateEntity();

        DynamicBuffer<StatValueRO> statValueBuffer = entityManager.AddBuffer<StatValueRO>(entity);
        statValueBuffer.Resize(10, Unity.Collections.NativeArrayOptions.ClearMemory);

        DynamicBuffer<StatVOBuffer> statVOBuffer = entityManager.AddBuffer<StatVOBuffer>(entity);
        VirtualObjectManager.Initialize(ref statVOBuffer, 100, 10000);

        VirtualListHandle<DirtyStat> dirtyStatsList = VirtualList<DirtyStat>.Allocate(ref statVOBuffer, 10);
        statOwner = new ExampleStatOwner
        {
            Strength = VirtualObjectManager.CreateObject(ref statVOBuffer, Stat.Create(0, 10f, dirtyStatsList, ref statVOBuffer)),
            Dexterity = VirtualObjectManager.CreateObject(ref statVOBuffer, Stat.Create(1, 10f, dirtyStatsList, ref statVOBuffer)),
            Intelligence = VirtualObjectManager.CreateObject(ref statVOBuffer, Stat.Create(2, 10f, dirtyStatsList, ref statVOBuffer)),

            DirtyStatsList = dirtyStatsList,
        };
        entityManager.AddComponentData(entity, statOwner);

        return entity;
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

    [BurstCompile]
    public partial struct UpdateStatROValuesJob : IJobEntity
    {
        void Execute(in ExampleStatOwner statOwner, ref DynamicBuffer<StatVOBuffer> statVOBuffer, ref DynamicBuffer<StatValueRO> statValueROBuffer)
        {
            StatUtility.TransferDirtyStatsToStatValues(statOwner.DirtyStatsList, ref statVOBuffer, ref statValueROBuffer);
        }
    }
}
