using Trove;
using Trove.DebugDraw;
using Trove.SpatialQueries;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using AABB = Trove.AABB;

[assembly: RegisterGenericJobType(typeof(BVH<TestNodeData>.BVHClearJob))]
    
public struct TestNodeData
{
    public Entity Entity;
}

partial struct SpatialQueryTesterSystem : ISystem
{
    private DebugDrawGroup _debugDrawGroup;
    
    private BVH<TestNodeData> _bvh;
    
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _bvh = BVH<TestNodeData>.Create(Allocator.Domain, 100000);
        
        state.RequireForUpdate<SpatialQueryTester>();
        state.RequireForUpdate<DebugDrawSingleton>();
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        _bvh.Dispose(default);
    }

    [BurstCompile]
    public unsafe void OnUpdate(ref SystemState state)
    {
        // Allocate the DebugDrawGroup if not created
        if (!_debugDrawGroup.IsCreated)
        {
            ref DebugDrawSingleton debugDrawSingleton = ref SystemAPI.GetSingletonRW<DebugDrawSingleton>().ValueRW;
            _debugDrawGroup = debugDrawSingleton.AllocateDebugDrawGroup();
        }
        
        if (SystemAPI.HasSingleton<SpatialQueryTester>())
        {
            ref SpatialQueryTester tester = ref SystemAPI.GetSingletonRW<SpatialQueryTester>().ValueRW;
            
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.Temp);

            if (!tester.IsInitialized)
            {
                Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(0);

                Entity spawnPrefab = tester.BVHCubePrefab;
                if (tester.UsePhysicsTest)
                {
                    spawnPrefab = tester.PhysicsCubePrefab;
                }
                
                for (int i = 0; i < tester.SpawnCount; i++)
                {
                    Entity newInstance = ecb.Instantiate(spawnPrefab);
                    ecb.SetComponent(newInstance, LocalTransform.FromPositionRotationScale(
                        random.NextFloat3(tester.SpawnArea.Min, tester.SpawnArea.Max),
                        random.NextQuaternionRotation(),
                        tester.SpawnScale));
                }

                tester.IsInitialized = true;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            
            // ---------------------------------------------------------------

            state.Dependency = _bvh.ScheduleClearJob(state.Dependency);

            if (tester.UseParallelAdd)
            {
                EntityQuery addEntitiesQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, BVHTestObject>().Build();
                NativeReference<int> addIndex = new NativeReference<int>(Allocator.Domain);
                
                state.Dependency = new ReserveBVHForAddJob
                {
                    ReserveCount = addEntitiesQuery.CalculateEntityCount(),
                    AddStartIndex = addIndex,
                    Bvh = _bvh,
                }.Schedule(state.Dependency);
                
                state.Dependency = new AddToBVHParallelJob()
                {
                    AddStartIndex = addIndex,
                    Bvh = _bvh,
                }.ScheduleParallel(state.Dependency);

                state.Dependency = _bvh.SchedulePostAddNodeUnsafeJobs(tester.UseParallelBuild, state.Dependency);
                
                addIndex.Dispose(state.Dependency);
            }
            else
            {
                state.Dependency = new AddToBVHJob
                {
                    Bvh = _bvh,
                }.Schedule(state.Dependency);
            }

            state.Dependency = _bvh.ScheduleBuildJobs(tester.UseParallelBuild, state.Dependency);

            // ---------------------------------------------------------------
            
            state.Dependency = new QueryBVHJob()
            {
                QueryScale = tester.QueryScale,
                BVH = _bvh,
            }.ScheduleParallel(state.Dependency);

            // ---------------------------------------------------------------
            
            // Debug
            if (SystemAPI.HasSingleton<BVHDebugger>())
            {
                BVHDebugger debugger = SystemAPI.GetSingleton<BVHDebugger>();
                
                _debugDrawGroup.Clear();
                state.Dependency.Complete();

                if (debugger.DebugBoundingBoxes)
                {
                    _bvh.GetNodes(out UnsafeList<BVHLeafNode> leafNodes, out UnsafeList<BVHHierarchyNode> hierarchyNodes, out AABB sceneAABB);

                    Stack nodesStack = new Stack(256);
                    int2* nodesStackPtr = stackalloc int2[nodesStack.Capacity];
                    
                    nodesStack.PushLast(nodesStackPtr, new int2(0, 0));  
                    while (nodesStack.PopLast(nodesStackPtr, out int2 nodeIndexAndLevel))
                    {
                        BVHHierarchyNode node = hierarchyNodes[nodeIndexAndLevel.x];
                        int nextLevel = nodeIndexAndLevel.y + 1;
                        
                        if (nodeIndexAndLevel.y == debugger.BoundingBoxDebugLevel)
                        {
                            _debugDrawGroup.DrawWireBox(
                                node.AABB.GetCenter(),
                                quaternion.identity,
                                node.AABB.GetExtents(),
                                UnityEngine.Color.green);
                        }
                        else if (node.ContainsLeafNodes == 0)
                        {
                            nodesStack.PushLast(nodesStackPtr, new int2(node.LeftIndex, nextLevel));
                            nodesStack.PushLast(nodesStackPtr, new int2(node.RightIndex, nextLevel));
                        }
                    }
                
                    _debugDrawGroup.DrawWireBox(
                        sceneAABB.GetCenter(),
                        quaternion.identity,
                        sceneAABB.GetExtents(),
                        UnityEngine.Color.white);
                }

                if (debugger.QueryEnabled)
                {
                    ComponentLookup<LocalTransform> localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
                    ComponentLookup<BVHTestObject> bvhTestObjectLookup = SystemAPI.GetComponentLookup<BVHTestObject>(true);

                    AABB aabb = AABB.FromCenterExtents(debugger.QueryPosition,
                        debugger.QueryExtents);

                   DefaultQueryCollector<TestNodeData> collector = new DefaultQueryCollector<TestNodeData>(32, Allocator.Temp);
                    UnsafeList<TestNodeData> allQueryResults = new UnsafeList<TestNodeData>(128, Allocator.Temp);

                    float3 spherePos = debugger.QueryPosition + (debugger.QueryDirection * -200f);
                    float sphereRadius = debugger.QueryExtents.x;
                    
                    _bvh.QueryAABB(aabb, ref collector);
                    allQueryResults.AddRange(collector.Results);
                    // _bvh.QuerySphere(spherePos, sphereRadius, ref collector);
                    // allQueryResults.AddRange(collector.Results);
                    // _bvh.QueryRay(debugger.QueryPosition, debugger.QueryDirection, debugger.QueryLength, ref collector);
                    // allQueryResults.AddRange(collector.Results);

                    // Draw query ray
                    _debugDrawGroup.DrawRay(
                        debugger.QueryPosition, 
                        debugger.QueryDirection, 
                        debugger.QueryLength,
                        UnityEngine.Color.blue);
                        
                    // Draw query bounds
                    _debugDrawGroup.DrawWireBox(
                        debugger.QueryPosition, 
                        quaternion.identity,
                        debugger.QueryExtents, 
                        UnityEngine.Color.blue);
                    _debugDrawGroup.DrawWireSphere(
                        spherePos, 
                        quaternion.identity,
                        sphereRadius, 
                        8,
                        8,
                        UnityEngine.Color.blue);
                    
                    // Draw query results
                    for (int i = 0; i < allQueryResults.Length; i++)
                    {
                        Entity resultEntity = allQueryResults[i].Entity;
                        if (localTransformLookup.TryGetComponent(resultEntity, out LocalTransform resultTransform) &&
                            bvhTestObjectLookup.TryGetComponent(resultEntity, out BVHTestObject resultBVHTestObject))
                        {
                            _debugDrawGroup.DrawWireBox(
                                resultTransform.Position, 
                                quaternion.identity,
                                resultTransform.Scale * resultBVHTestObject.AABBExtents, 
                                UnityEngine.Color.red);
                        }
                    }
                }
                
                // if (debugger.DebugNearestNeighbours)
                // {
                //     ComponentLookup<LocalTransform> localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
                //     ComponentLookup<BVHTestObject> bvhTestObjectLookup = SystemAPI.GetComponentLookup<BVHTestObject>(true);
                //
                //     if (_bvh.CreateNearestNeighborsQuerier(debugger.QueryPosition,
                //             out NearestNeighborsQuerier<TestNodeData> nearestNeighborsQuerier))
                //     {
                //         NearestNeighborResultCollector<TestNodeData> collector = new NearestNeighborResultCollector<TestNodeData>(32, Allocator.Temp);
                //         
                //         int counter = 0;
                //         while (counter <= debugger.NearestNeighboursDebugLevel)
                //         {
                //             nearestNeighborsQuerier.NextResultsBatch(in _bvh, ref collector, true);
                //             counter++;
                //
                //         }
                //     
                //         // Draw query results
                //         for (int i = 0; i < collector.Results.Length; i++)
                //         {
                //             Entity resultEntity = collector.Results[i].Data.Entity;
                //             if (localTransformLookup.TryGetComponent(resultEntity, out LocalTransform resultTransform) &&
                //                 bvhTestObjectLookup.TryGetComponent(resultEntity, out BVHTestObject resultBVHTestObject))
                //             {
                //                 _debugDrawGroup.DrawWireBox(
                //                     resultTransform.Position, 
                //                     quaternion.identity,
                //                     resultTransform.Scale * resultBVHTestObject.AABBExtents, 
                //                     UnityEngine.Color.red);
                //             }
                //         }
                //         
                //         // Draw actual closest
                //         if (collector.Results.Length > 0 &&
                //             localTransformLookup.TryGetComponent(collector.Results[0].Data.Entity, out LocalTransform closestTransform) &&
                //             bvhTestObjectLookup.TryGetComponent(collector.Results[0].Data.Entity, out BVHTestObject closestBVHTestObject))
                //         {
                //             _debugDrawGroup.DrawWireBox(
                //                 closestTransform.Position, 
                //                 quaternion.identity,
                //                 closestTransform.Scale * closestBVHTestObject.AABBExtents, 
                //                 UnityEngine.Color.yellow);
                //         }
                //     }
                // }
            }
        }
    }
    
    [BurstCompile]
    public partial struct AddToBVHJob : IJobEntity
    {
        public BVH<TestNodeData> Bvh;
         
        public void Execute(Entity entity, in LocalTransform transform, in BVHTestObject test)
        {
            AABB aabb = AABB.FromCenterExtents(transform.Position, test.AABBExtents * transform.Scale);
            Bvh.AddNode(new TestNodeData { Entity = entity }, aabb);
        }
    }
    
    [BurstCompile]
    public struct ReserveBVHForAddJob : IJob
    {
        public int ReserveCount;
        public NativeReference<int> AddStartIndex;
        public BVH<TestNodeData> Bvh;
         
        public void Execute()
        {
            Bvh.ReserveAddNodesUnsafe(ReserveCount, out int startIndexOfReservedRange);
            AddStartIndex.Value = startIndexOfReservedRange;
        }
    }
    
    [BurstCompile]
    public partial struct AddToBVHParallelJob : IJobEntity
    {
        [ReadOnly]
        public NativeReference<int> AddStartIndex;
        [NativeDisableParallelForRestriction]
        public BVH<TestNodeData> Bvh;
         
        public void Execute(Entity entity, [EntityIndexInQuery] int indexInQuery, in LocalTransform transform, in BVHTestObject test)
        {
            AABB aabb = AABB.FromCenterExtents(transform.Position, test.AABBExtents * transform.Scale);
            Bvh.AddNodeUnsafe(new TestNodeData { Entity = entity }, aabb, AddStartIndex.Value + indexInQuery);
        }
    }

    [BurstCompile]
    public partial struct QueryBVHJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public float QueryScale;
        [ReadOnly]
        public BVH<TestNodeData> BVH;

        private DefaultQueryCollector<TestNodeData> collector;
        
        public void Execute(in LocalTransform transform, ref BVHTestObject test)
        {
            AABB aabb = AABB.FromCenterExtents(transform.Position, test.AABBExtents * transform.Scale * QueryScale);
            BVH.QueryAABB(aabb, ref collector);
            test.QueryResults = collector.Results.Length;
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (!collector.IsCreated)
            {
                collector = new DefaultQueryCollector<TestNodeData>(32, Allocator.Temp);
            }
            
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask,
            bool chunkWasExecuted)
        {
        }
    }
}
