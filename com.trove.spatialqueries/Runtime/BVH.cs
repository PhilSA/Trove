using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Trove.SpatialQueries
{
    [StructLayout(LayoutKind.Explicit)]
    public struct BVHNode : IComparable<BVHNode>
    {
        [FieldOffset(0)]
        public uint MortonCode;
        [FieldOffset(4)]
        public int DataIndex; // For leaf nodes this is index of their data, but for parent nodes this is index of their first child
        [FieldOffset(8)]
        public AABB AABB;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid()
        {
            return DataIndex >= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(BVHNode other)
        {
            return MortonCode.CompareTo(other.MortonCode);
        }
    }

    public struct NodeLevelData
    {
        public int StartIndex;
        public int Count;
    }

    public interface IBVHQueryCollector<TNodeData> where TNodeData : struct
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnBeginQuery();
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNode(in TNodeData node);
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFoundResults();
    }

    public struct DefaultQueryCollector<TNodeData> : IBVHQueryCollector<TNodeData>
        where TNodeData : unmanaged
    {
        public UnsafeList<TNodeData> Results;
        public bool IsCreated => Results.IsCreated;

        public DefaultQueryCollector(int capacity, Allocator allocator)
        {
            Results = new UnsafeList<TNodeData>(capacity, allocator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnBeginQuery()
        {
            Results.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNode(in TNodeData node)
        {
            Results.AddWithGrowFactor(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFoundResults()
        {
            return Results.Length > 0;
        }

        public void Dispose()
        {
            Results.Dispose();
        }
    }

    public struct NearestNeighborResultCollector<TNodeData> : IBVHQueryCollector<NearestNeighborResult<TNodeData>>
        where TNodeData : unmanaged
    {
        public UnsafeList<NearestNeighborResult<TNodeData>> Results;

        public bool IsCreated => Results.IsCreated;

        public NearestNeighborResultCollector(int capacity, Allocator allocator)
        {
            Results = new UnsafeList<NearestNeighborResult<TNodeData>>(capacity, allocator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnBeginQuery()
        {
            Results.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNode(in NearestNeighborResult<TNodeData> node)
        {
            Results.AddWithGrowFactor(node);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFoundResults()
        {
            return Results.Length > 0;
        }

        public void Dispose()
        {
            Results.Dispose();
        }
    }

    public struct NearestNeighborsQuerier<TNodeData> where TNodeData : unmanaged
    {
        internal float3 Position;
        internal int CurrentNodeIndexInLevel;
        internal int CurrentLevel;
        internal float MaxDistance;

        private bool InvalidatedForNextBatches;

        public bool NextResultsBatch(in BVH<TNodeData> bvh, ref NearestNeighborResultCollector<TNodeData> collector, bool sortResults = true)
        {
            collector.OnBeginQuery();
                
            if (CurrentLevel >= bvh.NodeLevelDatas.Length || InvalidatedForNextBatches)
                return false;
                
            NodeLevelData levelData = bvh.NodeLevelDatas[CurrentLevel];
                
            // Do a query at the current distance
            AABB currentNodeAABB = bvh.SortedNodes[levelData.StartIndex + CurrentNodeIndexInLevel].AABB;
            float queryDistance = math.distance(currentNodeAABB.FarthestPoint(Position), Position);

            if (queryDistance > MaxDistance)
            {
                InvalidatedForNextBatches = true;
                queryDistance = math.min(queryDistance, MaxDistance);
            }
                
            bvh.QueryNearestNeighborsInternal(Position, queryDistance, ref collector);

            if (collector.Results.Length == 0)
                return false;
                
            if (sortResults)
            {
                collector.Results.Sort();
            }
                
            CurrentLevel++;
            CurrentNodeIndexInLevel /= BVHUtils.NbLeavesPerNode; // parent node

            return true;
        }
    }
    
    public struct NearestNeighborResult<TNodeData> : IComparable<NearestNeighborResult<TNodeData>>
        where TNodeData : unmanaged
    {
        public TNodeData Data;
        public float DistanceSq;

        public int CompareTo(NearestNeighborResult<TNodeData> other)
        {
            return DistanceSq.CompareTo(other.DistanceSq);
        }
    }

    public struct BVH<TNodeData> where TNodeData : unmanaged
    {
        // Nodes A and B are used to ping pong between buffers during sorting.
        // After sorting, one of them becomes the "SortedNodes" and the other becomes the "ReorderedNodes"
        internal NativeList<BVHNode> NodesA;
        internal NativeList<BVHNode> NodesB;
        internal NativeList<TNodeData> LeafNodeDatas;
        internal NativeList<NodeLevelData> NodeLevelDatas;
        internal NativeReference<AABB> SceneAABB;
        internal NativeList<int> RadixSortHistograms;

        internal NativeList<BVHNode> SortedNodes => NodesA;

        public static BVH<TNodeData> Create(Allocator allocator, int initialElementsCapacity)
        {
            BVH<TNodeData> bvh = new BVH<TNodeData>();
            bvh.NodesA = new NativeList<BVHNode>(
                BVHUtils.ComputeTotalNodesCountForEntries(initialElementsCapacity),
                allocator);
            bvh.NodesB = new NativeList<BVHNode>(bvh.NodesA.Capacity, allocator);
            bvh.LeafNodeDatas = new NativeList<TNodeData>(initialElementsCapacity, allocator);
            bvh.NodeLevelDatas = new NativeList<NodeLevelData>(32, allocator);
            bvh.SceneAABB = new NativeReference<AABB>(allocator);
            bvh.RadixSortHistograms = new NativeList<int>(BVHUtils.RadixSortBucketCount, allocator);
            bvh.RadixSortHistograms.Resize(bvh.RadixSortHistograms.Capacity, NativeArrayOptions.ClearMemory);

            return bvh;
        }

        public void Dispose(JobHandle jobHandle)
        {
            if (NodesA.IsCreated)
            {
                NodesA.Dispose(jobHandle);
            }

            if (NodesB.IsCreated)
            {
                NodesB.Dispose(jobHandle);
            }

            if (LeafNodeDatas.IsCreated)
            {
                LeafNodeDatas.Dispose(jobHandle);
            }

            if (NodeLevelDatas.IsCreated)
            {
                NodeLevelDatas.Dispose(jobHandle);
            }

            if (SceneAABB.IsCreated)
            {
                SceneAABB.Dispose(jobHandle);
            }

            if (RadixSortHistograms.IsCreated)
            {
                RadixSortHistograms.Dispose(jobHandle);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddNode(in TNodeData nodeData, in AABB aabb)
        {
            ref AABB sceneAABBRef = ref *SceneAABB.GetUnsafePtr();
            sceneAABBRef.Include(aabb);

            NodesA.Add(new BVHNode
            {
                AABB = aabb,
            });
            LeafNodeDatas.Add(nodeData);
        }

        public void ReserveAddNodesUnsafe(int addNodesCount, out int startIndexOfReservedRange)
        {
            startIndexOfReservedRange = NodesA.Length;
            NodesA.Resize(NodesA.Length + addNodesCount, NativeArrayOptions.UninitializedMemory);
            LeafNodeDatas.Resize(LeafNodeDatas.Length + addNodesCount, NativeArrayOptions.UninitializedMemory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddNodeUnsafe(in TNodeData nodeData, in AABB aabb, int atIndex)
        {
            NodesA[atIndex] = new BVHNode
            {
                AABB = aabb,
            };
            LeafNodeDatas[atIndex] = nodeData;
        }

        public unsafe void AddNodesUnsafe(TNodeData* nodeDatas, AABB* aabbs, int count, int atIndex)
        {
            const int AABBsFieldOffset = 8;
            
            BVHNode* dstNodes = NodesA.GetUnsafePtr() + (long)atIndex;
            AABB* dstAABB = (AABB*)((byte*)dstNodes + (long)AABBsFieldOffset); // AABBs are at fieldOffset 8
            UnsafeUtility.MemCpyStride(
                dstAABB, AABBsFieldOffset, 
                aabbs, 0, 
                UnsafeUtility.SizeOf<AABB>(), count);
            
            TNodeData* dstNodeDatas = LeafNodeDatas.GetUnsafePtr() + (long)atIndex;
            UnsafeUtility.MemCpy(dstNodeDatas, nodeDatas, UnsafeUtility.SizeOf<TNodeData>() * count);
        }

        public unsafe bool QueryAABB<TCollector>(in AABB aabb, ref TCollector collector) 
            where TCollector : unmanaged, IBVHQueryCollector<TNodeData>
        {
            collector.OnBeginQuery();
        
            if (SortedNodes.Length < 1)
            {
                return false;
            }
        
            Stack nodesStack = new Stack(256);
            int* nodesStackPtr = stackalloc int[nodesStack.Capacity];
            BVHNode* nodesPtr = SortedNodes.GetUnsafeReadOnlyPtr();
            TNodeData* leafDataPtr = LeafNodeDatas.GetUnsafeReadOnlyPtr();
            int leafNodesCount = LeafNodeDatas.Length;

            nodesStack.PushLast(nodesStackPtr, SortedNodes.Length - 1);  // start at root node;
            while (nodesStack.PopLast(nodesStackPtr, out int nodeIndex))
            {
                BVHNode node = nodesPtr[nodeIndex];

                if (!aabb.OverlapsAABB(node.AABB) || !node.IsValid())
                    continue;

                if (nodeIndex < leafNodesCount)
                {
                    collector.AddNode(leafDataPtr[node.DataIndex]);
                }
                else
                {
                    for (int i = 0; i < BVHUtils.NbLeavesPerNode; i++)
                    {
                        nodesStack.PushLast(nodesStackPtr, node.DataIndex + i);
                    }
                }
            }

            return collector.HasFoundResults();
        }

        public unsafe bool QuerySphere<TCollector>(in float3 position, float radius, ref TCollector collector)
            where TCollector : unmanaged, IBVHQueryCollector<TNodeData>
        {
            collector.OnBeginQuery();
        
            if (SortedNodes.Length < 1)
            {
                return false;
            }
        
            Stack nodesStack = new Stack(256);
            int* nodesStackPtr = stackalloc int[nodesStack.Capacity];
            BVHNode* nodesPtr = SortedNodes.GetUnsafeReadOnlyPtr();
            TNodeData* leafDataPtr = LeafNodeDatas.GetUnsafeReadOnlyPtr();
            int leafNodesCount = LeafNodeDatas.Length;
            
            float radiusSq = radius * radius;

            nodesStack.PushLast(nodesStackPtr, SortedNodes.Length - 1);  // start at root node;
            while (nodesStack.PopLast(nodesStackPtr, out int nodeIndex))
            {
                BVHNode node = nodesPtr[nodeIndex];

                if (!node.AABB.OverlapsSphere(position, radiusSq) || !node.IsValid())
                    continue;

                if (nodeIndex < leafNodesCount)
                {
                    collector.AddNode(leafDataPtr[node.DataIndex]);
                }
                else
                {
                    for (int i = 0; i < BVHUtils.NbLeavesPerNode; i++)
                    {
                        nodesStack.PushLast(nodesStackPtr, node.DataIndex + i);
                    }
                }
            }

            return collector.HasFoundResults();
        }

        public unsafe bool QueryRay<TCollector>(float3 rayOrigin, float3 rayDirectionNormalized, float rayLength,
            ref TCollector collector)
            where TCollector : unmanaged, IBVHQueryCollector<TNodeData>
        {
            collector.OnBeginQuery();

            if (SortedNodes.Length < 1) {
                return false;
            }

            Stack nodesStack = new Stack(256);
            int* nodesStackPtr = stackalloc int[nodesStack.Capacity];
            BVHNode* nodesPtr = SortedNodes.GetUnsafeReadOnlyPtr();
            TNodeData* leafDataPtr = LeafNodeDatas.GetUnsafeReadOnlyPtr();
            int leafNodesCount = LeafNodeDatas.Length;

            nodesStack.PushLast(nodesStackPtr, SortedNodes.Length - 1);  // start at root node;
            while (nodesStack.PopLast(nodesStackPtr, out int nodeIndex)) {
                BVHNode node = nodesPtr[nodeIndex];

                if (!node.AABB.IntersectsRay(rayOrigin, rayDirectionNormalized, rayLength) || !node.IsValid())
                    continue;

                if (nodeIndex < leafNodesCount) {
                    collector.AddNode(leafDataPtr[node.DataIndex]);
                } else {
                    for (int i = 0; i < BVHUtils.NbLeavesPerNode; i++) {
                        nodesStack.PushLast(nodesStackPtr, node.DataIndex + i);
                    }
                }
            }

            return collector.HasFoundResults();
        }

        // Perform a raycast and fetch only the closest entity (no collector required)
        // This version also gives the closest distance.
        public unsafe bool QueryRayClosest(float3 rayOrigin, float3 rayDirectionNormalized, float rayLength,
            out TNodeData closestHit, out float closestDistance)
        {
            closestHit = default;
            closestDistance = float.MaxValue;

            if (SortedNodes.Length < 1) {
                return false;
            }

            Stack nodesStack = new Stack(256);
            int* nodesStackPtr = stackalloc int[nodesStack.Capacity];
            BVHNode* nodesPtr = SortedNodes.GetUnsafeReadOnlyPtr();
            TNodeData* leafDataPtr = LeafNodeDatas.GetUnsafeReadOnlyPtr();
            int leafNodesCount = LeafNodeDatas.Length;

            float currentMaxT = rayLength;
            bool found = false;

            nodesStack.PushLast(nodesStackPtr, SortedNodes.Length - 1);
            while (nodesStack.PopLast(nodesStackPtr, out int nodeIndex)) {
                BVHNode node = nodesPtr[nodeIndex];
                if (!node.IsValid())
                    continue;
                if (!node.AABB.IntersectsRay(rayOrigin, rayDirectionNormalized, currentMaxT, out float tEntry))
                    continue;
                if (tEntry >= currentMaxT)
                    continue;

                if (nodeIndex < leafNodesCount) {
                    currentMaxT = tEntry;
                    closestDistance = tEntry;
                    closestHit = leafDataPtr[node.DataIndex];
                    found = true;
                } else {
                    // Ordered child traversal: push children in descending tEntry order so closest pops first
                    int childBase = node.DataIndex;
                    float4 ts = new float4(float.MaxValue);
                    int4 idxs = new int4(-1);
                    for (int i = 0; i < BVHUtils.NbLeavesPerNode; i++)
                    {
                        int ci = childBase + i;
                        BVHNode child = nodesPtr[ci];
                        if (!child.IsValid())
                            continue;
                        if (child.AABB.IntersectsRay(rayOrigin, rayDirectionNormalized, currentMaxT, out float t))
                        {
                            ts[i] = t;
                            idxs[i] = ci;
                        }
                    }
                    // Insertion sort, descending by t — smallest t ends up on top of stack
                    for (int a = 0; a < 4; a++)
                    {
                        for (int b = a + 1; b < 4; b++)
                        {
                            if (ts[b] > ts[a])
                            {
                                (ts[a], ts[b]) = (ts[b], ts[a]);
                                (idxs[a], idxs[b]) = (idxs[b], idxs[a]);
                            }
                        }
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        if (idxs[i] >= 0) { nodesStack.PushLast(nodesStackPtr, idxs[i]); }
                    }
                }
            }

            return found;
        }

        // Returns true if any node was hit (useful for line-of-sight tests).
        public unsafe bool QueryRayAny(float3 rayOrigin, float3 rayDirectionNormalized, float rayLength)
        {
            if (SortedNodes.Length < 1)
                return false;

            Stack nodesStack = new Stack(256);
            int* nodesStackPtr = stackalloc int[nodesStack.Capacity];
            BVHNode* nodesPtr = SortedNodes.GetUnsafeReadOnlyPtr();
            int leafNodesCount = LeafNodeDatas.Length;

            nodesStack.PushLast(nodesStackPtr, SortedNodes.Length - 1);  // start at root node;
            while (nodesStack.PopLast(nodesStackPtr, out int nodeIndex))
            {
                BVHNode node = nodesPtr[nodeIndex];

                if (!node.AABB.IntersectsRay(rayOrigin, rayDirectionNormalized, rayLength) || !node.IsValid())
                    continue;

                if (nodeIndex < leafNodesCount)
                {
                    return true;
                }
                else
                {
                    for (int i = 0; i < BVHUtils.NbLeavesPerNode; i++)
                    {
                        nodesStack.PushLast(nodesStackPtr, node.DataIndex + i);
                    }
                }
            }

            return false;
        }


        // Perform a raycast and fetch only the closest entity (no collector required)
        // Use this version if you don't care about the distance.
        public bool QueryRayClosest(float3 rayOrigin, float3 rayDirectionNormalized, float rayLength, out TNodeData closestHit)
        {
            return QueryRayClosest(rayOrigin, rayDirectionNormalized, rayLength, out closestHit, out _);
        }

        public bool QueryNearestNeighbor(float3 position, ref NearestNeighborResultCollector<TNodeData> collector, 
            out NearestNeighborResult<TNodeData> nearestResult, float maxDistance = float.MaxValue)
        {
            if (CreateNearestNeighborsQuerier(position, out NearestNeighborsQuerier<TNodeData> querier, maxDistance))
            {
                if(querier.NextResultsBatch(in this, ref collector, false))
                {
                    UnsafeList<NearestNeighborResult<TNodeData>> results = collector.Results;
                    nearestResult = results[0];
                    for (int i = 1; i < results.Length; i++)
                    {
                        if (results[i].DistanceSq < nearestResult.DistanceSq)
                        {
                            nearestResult = results[i];
                        }
                    }
                    return true;
                }
            }

            nearestResult = default;
            return false;
        }

        public unsafe bool CreateNearestNeighborsQuerier(float3 position, out NearestNeighborsQuerier<TNodeData> querier, float maxDistance = float.MaxValue)
        {
            // Project position onto Scene AABB if not inside it
            if (!SceneAABB.Value.Contains(position))
            {
                float3 positionOnScene = SceneAABB.Value.ClosestPoint(position);
                float3 positionToSceneNorm = math.normalize(positionOnScene - position);
                position = positionOnScene + (positionToSceneNorm * 0.1f);
            }

            int deepestSmallestContainingNodeIndex = int.MaxValue;
            float deepestSmallestContainingNodeVolume = float.MaxValue;
            if (SortedNodes.Length >= 1)
            {
                // Calculate the morton code of the position
                float3 sceneDimensions = SceneAABB.Value.Max - SceneAABB.Value.Min;
                float3 normalizedPositionInScene = (position - SceneAABB.Value.Min) / sceneDimensions; 
                uint queriedMortonCode = BVHUtils.ComputeMortonCode(normalizedPositionInScene);
                
                // Approximate the index of this morton code in sorted leaf nodes
                float normMortonValue = (float)queriedMortonCode / (float)uint.MaxValue;
                int queriedNodeIndex = (int)math.round(LeafNodeDatas.Length * normMortonValue);
                
                // Search for closest morton from that index
                int indexOfClosestMorton = -1;
                uint iteratedMorton = SortedNodes[queriedNodeIndex].MortonCode;
                if (iteratedMorton == queriedMortonCode)
                {
                    indexOfClosestMorton = queriedNodeIndex;
                }
                else
                {
                    // binary search for match
                    bool iteratedMortonIsGreater = iteratedMorton > queriedMortonCode;
                    int minIndex = iteratedMortonIsGreater ? 0 : queriedNodeIndex;
                    int maxIndex = iteratedMortonIsGreater ? queriedNodeIndex : LeafNodeDatas.Length ;
                
                    while (maxIndex - minIndex > 1)
                    {
                        queriedNodeIndex = minIndex + ((maxIndex - minIndex) / 2);
                        iteratedMorton = SortedNodes[queriedNodeIndex].MortonCode;
                        if (iteratedMorton == queriedMortonCode)
                        {
                            break;
                        }
                        else
                        {
                            // Update min and max
                            iteratedMortonIsGreater = iteratedMorton > queriedMortonCode;
                            minIndex = iteratedMortonIsGreater ? minIndex : queriedNodeIndex;
                            maxIndex = iteratedMortonIsGreater ? queriedNodeIndex : maxIndex;
                        }
                    }
                    
                    indexOfClosestMorton = queriedNodeIndex;
                }

                if (indexOfClosestMorton >= 0)
                {
                    float3 iteratedNodePos = SortedNodes[queriedNodeIndex].AABB.GetCenter();
                    float closestDistanceSqSoFar = math.distancesq(position, iteratedNodePos);
                    
                    // Find the closest in a range of X from that node
                    // (this mitigates the impact of large jumps in decoded morton code positions)
                    int halfRange = 9;
                    int startIndex = math.max(0, indexOfClosestMorton - halfRange);
                    int endIndex = math.min(LeafNodeDatas.Length, indexOfClosestMorton + halfRange);
                    for (int i = startIndex; i <= endIndex; i++)
                    {
                        iteratedNodePos = SortedNodes[i].AABB.GetCenter();
                        float distanceSq = math.distancesq(position, iteratedNodePos);
                        if (distanceSq < closestDistanceSqSoFar)
                        {
                            indexOfClosestMorton = i;
                            closestDistanceSqSoFar = distanceSq;
                        }
                    }
                    
                    querier = new NearestNeighborsQuerier<TNodeData>
                    {
                        Position = position,
                        CurrentNodeIndexInLevel = indexOfClosestMorton,
                        CurrentLevel = 0,
                        MaxDistance = maxDistance,
                    };
                    return true;
                }
            }

            querier = default;
            return false;
        }

        internal unsafe void QueryNearestNeighborsInternal(in float3 position, float radius, ref NearestNeighborResultCollector<TNodeData> collector)
        {
            collector.OnBeginQuery();
        
            if (SortedNodes.Length < 1)
            {
                return;
            }
        
            Stack nodesStack = new Stack(256);
            int* nodesStackPtr = stackalloc int[nodesStack.Capacity];
            BVHNode* nodesPtr = SortedNodes.GetUnsafeReadOnlyPtr();
            TNodeData* leafDataPtr = LeafNodeDatas.GetUnsafeReadOnlyPtr();
            int leafNodesCount = LeafNodeDatas.Length;

            float radiusSq = radius * radius;
            
            nodesStack.PushLast(nodesStackPtr, SortedNodes.Length - 1);  // start at root node;
            while (nodesStack.PopLast(nodesStackPtr, out int nodeIndex))
            {
                BVHNode node = nodesPtr[nodeIndex];

                if (!node.AABB.OverlapsSphere(position, radiusSq) || !node.IsValid())
                    continue;

                if (nodeIndex < leafNodesCount)
                {
                    collector.AddNode(new NearestNeighborResult<TNodeData>
                    {
                        Data = leafDataPtr[node.DataIndex],
                        DistanceSq = node.AABB.DistanceSq(position),
                    });
                }
                else
                {
                    for (int i = 0; i < BVHUtils.NbLeavesPerNode; i++)
                    {
                        nodesStack.PushLast(nodesStackPtr, node.DataIndex + i);
                    }
                }
            }
        }
        
        public JobHandle ScheduleClearJob(JobHandle dep)
        {
            dep = new BVHClearJob
            {
                BVH = this,
            }.Schedule(dep);

            return dep;
        }

        public JobHandle SchedulePostAddNodeUnsafeJobs(bool parallel, JobHandle dep)
        {
            int workerCount = parallel ? JobsUtility.JobWorkerCount : 1;

            NativeArray<AABB> aabbForWorker = new NativeArray<AABB>(workerCount, Allocator.Domain);

            dep = new RecomputeSceneAABBsJob
            {
                WorkerCount = workerCount,
                UnsortedNodes = NodesA,
                AABBForWorker = aabbForWorker,
            }.ScheduleParallel(workerCount, 1, dep);

            dep = new RecomputeSceneAABBsMergeJob()
            {
                SceneAABB = SceneAABB,
                AABBForWorker = aabbForWorker,
            }.Schedule(dep);

            aabbForWorker.Dispose(dep);

            return dep;
        }

        public JobHandle ScheduleBuildJobs(bool parallel, JobHandle dep)
        {
            int workerCount = parallel ? JobsUtility.JobWorkerCount : 1;

            dep = new BVHComputeMortonCodesAndDataIndexesJob
            {
                WorkerCount = workerCount,
                SceneAABB = SceneAABB,
                UnsortedNodes = NodesA,
            }.ScheduleParallel(workerCount, 1, dep);

            // Radix sort by bytes of the morton code (4 bytes = 4 passes)
            for (int pass = 0; pass < BVHUtils.RadixSortPasses; pass++)
            {
                bool isEvenPass = pass % 2 == 0;
                NativeList<BVHNode> inputNodes = isEvenPass ? NodesA : NodesB;
                NativeList<BVHNode> outputNodes = isEvenPass ? NodesB : NodesA; // Final pass will be 3, which means output ends up in NodesA
                
                dep = new BVHRadixSortInitializePassJob
                {
                    WorkerCount = workerCount,
                    InputNodes = inputNodes,
                    OutputNodes = outputNodes,
                    RadixSortHistograms = RadixSortHistograms,
                }.Schedule(dep);

                dep = new BVHRadixSortComputeBucketHistogramJob
                {
                    WorkerCount = workerCount,
                    Pass = pass,
                    Nodes = inputNodes,
                    RadixSortHistograms = RadixSortHistograms,
                }.ScheduleParallel(workerCount, 1, dep);

                dep = new BVHRadixSortComputeBucketIndexRangesJob
                {
                    WorkerCount = workerCount,
                    RadixSortHistograms = RadixSortHistograms,
                }.Schedule(dep);

                dep = new BVHRadixSortJob
                {
                    WorkerCount = workerCount,
                    Pass = pass,
                    InputNodes = inputNodes,
                    OutputNodes = outputNodes,
                    RadixHistograms = RadixSortHistograms,
                }.ScheduleParallel(workerCount, 1, dep);
            }

            dep = new BVHPrecomputeHierarchyJob
            {
                SortedNodes = SortedNodes,
                NodeLevelDatas = NodeLevelDatas,
            }.Schedule(dep);

            NativeReference<int> parallelWorkersLastWrittenLevel = new NativeReference<int>(Allocator.Domain);

            dep = new BVHBuildHierarchyJob
            {
                WorkerCount = workerCount,
                ParallelWorkersLastWrittenLevel = parallelWorkersLastWrittenLevel,
                SortedNodes = SortedNodes,
                NodeLevelDatas = NodeLevelDatas,
            }.ScheduleParallel(workerCount, 1, dep);

            dep = new BVHBuildHierarchyFinalizeJob
            {
                ParallelWorkersLastWrittenLevel = parallelWorkersLastWrittenLevel,
                SortedNodes = SortedNodes,
                NodeLevelDatas = NodeLevelDatas,
            }.Schedule(dep);

            parallelWorkersLastWrittenLevel.Dispose(dep);

            return dep;
        }

        public unsafe void GetNodes(out UnsafeList<BVHNode> nodes,
            out UnsafeList<NodeLevelData> nodeLevelStartIndexesAndCounts)
        {
            nodes = (*SortedNodes.GetUnsafeList());
            nodeLevelStartIndexesAndCounts = (*NodeLevelDatas.GetUnsafeList());
        }

        [BurstCompile]
        public struct BVHClearJob : IJob
        {
            public BVH<TNodeData> BVH;

            public void Execute()
            {
                BVH.NodesA.Clear();
                BVH.NodesB.Clear();
                BVH.LeafNodeDatas.Clear();
                BVH.NodeLevelDatas.Clear();
                BVH.SceneAABB.Value = AABB.GetEmpty();
            }
        }
    }

    internal static class BVHUtils
    {
        internal const int NbLeavesPerNode = 4; 
        
        internal const int RadixBits = 8;
        internal const int RadixSortBucketCount = 1 << RadixBits; // 256 values of a byte
        internal const int RadixSortPasses = 4; // 4 bytes of the morton uint

        internal static int ComputeTotalNodesCountForEntries(int entriesCount)
        {
            // Make entries count even
            if (entriesCount % BVHUtils.NbLeavesPerNode != 0)
            {
                entriesCount += BVHUtils.NbLeavesPerNode - (entriesCount % BVHUtils.NbLeavesPerNode);
            }

            float entriesCountFloat = (float)entriesCount;
            while (entriesCountFloat > 1f)
            {
                entriesCountFloat /= BVHUtils.NbLeavesPerNode;
                entriesCount += (int)math.ceil(entriesCountFloat);
            }

            return entriesCount;
        }

        /// <summary>
        /// https://developer.nvidia.com/blog/thinking-parallel-part-iii-tree-construction-gpu/
        ///
        /// The nPos is a normalized position from 0,0,0 to 1,1,1
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ComputeMortonCode(float3 nPos)
        {
            // The normalized position coords get turned into a 0f to 1023f range. We want the 1023 range because
            // 1023 as a uint in binary is 1111111111, which is 10 bits. Later this will allow us to store the 3
            // position coords as interleaved shifted bits in a 32 bit uint.
            nPos = math.min(math.max(nPos * 1024.0f, 0.0f), 1023.0f);

            // By casting the 0-to-1023 number to uint, we get 10 significant  bits to work with. (1023 in binary
            // is 1111111111). We then "expand" the bits of each value to make space for bit interleaving. 
            uint expandedX = ExpandBits((uint)nPos.x);
            uint expandedY = ExpandBits((uint)nPos.y);
            uint expandedZ = ExpandBits((uint)nPos.z);

            // This is what creates the "interleaving" of the expanded bits. Multiplication by 4 "shifts" the bits
            // by two spaces, and multiplying by 2 shifts by one space.
            return (expandedX * 4) + (expandedY * 2) + expandedZ;
        }

        /// <summary>
        /// https://developer.nvidia.com/blog/thinking-parallel-part-iii-tree-construction-gpu/
        ///
        /// This takes a value with 10 significant bits, and inserts two zeroes in between each bit. This results in
        /// a 30 bit value (which still fits into a 32 bit uint).
        ///
        /// By ending up with a 30 bit value, we then have 2 free spaces left to "shift" the bits for interleaving later.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ExpandBits(uint val)
        {
            val = (val * 0x00010001u) & 0xFF0000FFu;
            val = (val * 0x00000101u) & 0x0F00F00Fu;
            val = (val * 0x00000011u) & 0xC30C30C3u;
            val = (val * 0x00000005u) & 0x49249249u;
            return val;
        }
    }

    [BurstCompile]
    public struct RecomputeSceneAABBsJob : IJobFor
    {
        public int WorkerCount;
        [ReadOnly]
        public NativeList<BVHNode> UnsortedNodes;
        [NativeDisableParallelForRestriction]
        public NativeArray<AABB> AABBForWorker;

        public void Execute(int workerIndex)
        {
            int nodesPerWorker = MathUtilities.DivideIntCeil(UnsortedNodes.Length, WorkerCount);
            int startIndex = workerIndex * nodesPerWorker;
            int endIndex = math.min(UnsortedNodes.Length, startIndex + nodesPerWorker);

            AABB sceneAABB = AABB.GetEmpty();
            for (int i = startIndex; i < endIndex; i++)
            {
                sceneAABB.Include(UnsortedNodes[i].AABB);
            }
            AABBForWorker[workerIndex] = sceneAABB;
        }
    }

    [BurstCompile]
    public unsafe struct RecomputeSceneAABBsMergeJob : IJob
    {
        public NativeReference<AABB> SceneAABB;
        public NativeArray<AABB> AABBForWorker;

        public void Execute()
        {
            ref AABB sceneAABBRef = ref *SceneAABB.GetUnsafePtr();

            for (int i = 0; i < AABBForWorker.Length; i++)
            {
                sceneAABBRef.Include(AABBForWorker[i]);
            }
        }
    }

    [BurstCompile]
    public unsafe struct BVHComputeMortonCodesAndDataIndexesJob : IJobFor
    {
        public int WorkerCount;
        [ReadOnly]
        public NativeReference<AABB> SceneAABB;
        [NativeDisableParallelForRestriction]
        public NativeList<BVHNode> UnsortedNodes;

        public void Execute(int index)
        {
            int nodesPerWorker = MathUtilities.DivideIntCeil(UnsortedNodes.Length, WorkerCount);
            int startIndex = index * nodesPerWorker;
            int endIndex = math.min(UnsortedNodes.Length, startIndex + nodesPerWorker);

            // Compute morton codes (from normalized position relative to scene AABB)
            AABB sceneAABB = SceneAABB.Value;
            float3 sceneDimensions = sceneAABB.Max - sceneAABB.Min;
            BVHNode* nodesPtr = UnsortedNodes.GetUnsafePtr();
            for (int i = startIndex; i < endIndex; i++)
            {
                ref BVHNode nodeRef = ref UnsafeUtility.ArrayElementAsRef<BVHNode>(nodesPtr, i);
                float3 normalizedPosition = (nodeRef.AABB.GetCenter() - sceneAABB.Min) / sceneDimensions; // Position from 0f to 1f in the scene
                nodeRef.MortonCode = BVHUtils.ComputeMortonCode(normalizedPosition);
                nodeRef.DataIndex = i; 
            }
        }
    }

    [BurstCompile]
    internal struct BVHRadixSortInitializePassJob : IJob
    {
        public int WorkerCount;
        public NativeList<BVHNode> InputNodes;
        public NativeList<BVHNode> OutputNodes;
        public NativeList<int> RadixSortHistograms;

        public void Execute()
        {
            // Ensure output buffer length
            if (OutputNodes.Length != InputNodes.Length)
            {
                OutputNodes.Resize(InputNodes.Length, NativeArrayOptions.UninitializedMemory);
            }

            // Clear histogram
            RadixSortHistograms.Resize(BVHUtils.RadixSortBucketCount * WorkerCount, NativeArrayOptions.ClearMemory);
            for (int i = 0; i < RadixSortHistograms.Length; i++)
            {
                RadixSortHistograms[i] = 0;
            }
        }
    }

    [BurstCompile]
    internal unsafe struct BVHRadixSortComputeBucketHistogramJob : IJobFor
    {
        public int WorkerCount;
        public int Pass;
        [ReadOnly]
        public NativeList<BVHNode> Nodes;
        [NativeDisableParallelForRestriction]
        public NativeList<int> RadixSortHistograms;

        public void Execute(int workerIndex)
        {
            if (Nodes.Length == 0)
                return;

            int nodesPerWorker = MathUtilities.DivideIntCeil(Nodes.Length, WorkerCount);
            int startIndex = workerIndex * nodesPerWorker;
            int endIndex = math.min(Nodes.Length, startIndex + nodesPerWorker);

            int* histogramPtrForWorker = RadixSortHistograms.GetUnsafePtr() + (long)(workerIndex * BVHUtils.RadixSortBucketCount);

            // Calculate nb of occurrences for the value of each bucket (each worker responsible for a range of nodes)
            int bitShiftForPass = Pass * BVHUtils.RadixBits;
            for (int i = startIndex; i < endIndex; i++)
            {
                uint mortonCode = Nodes[i].MortonCode;
                int bucketIndex = (int)((mortonCode >> bitShiftForPass) & (BVHUtils.RadixSortBucketCount - 1));
                histogramPtrForWorker[bucketIndex]++;
            }
        }
    }

    [BurstCompile]
    internal unsafe struct BVHRadixSortComputeBucketIndexRangesJob : IJob
    {
        public int WorkerCount;
        public NativeList<int> RadixSortHistograms;

        public void Execute()
        {
            int* histogramsPtr = RadixSortHistograms.GetUnsafePtr();

            int* bucketValueCounts = stackalloc int[BVHUtils.RadixSortBucketCount];
            int* bucketNodeStartIndexes = stackalloc int[BVHUtils.RadixSortBucketCount];

            // Compute histogram totals across all workers
            for (int bucketIndex = 0; bucketIndex < BVHUtils.RadixSortBucketCount; bucketIndex++)
            {
                int total = 0;
                for (int worker = 0; worker < WorkerCount; worker++)
                {
                    total += histogramsPtr[(worker * BVHUtils.RadixSortBucketCount) + bucketIndex];
                }
                bucketValueCounts[bucketIndex] = total;
            }

            // Compute the nodes start index for each bucket
            int indexCounter = 0;
            for (int bucketIndex = 0; bucketIndex < BVHUtils.RadixSortBucketCount; bucketIndex++)
            {
                bucketNodeStartIndexes[bucketIndex] = indexCounter;
                indexCounter += bucketValueCounts[bucketIndex];
            }

            // Store the nodes start index of each bucket for each worker in the histogram.
            // This will allow each worker to sort their respective range of nodes and store elements in all buckets.
            for (int bucketIndex = 0; bucketIndex < BVHUtils.RadixSortBucketCount; bucketIndex++)
            {
                int bucketNodesStartIndex = bucketNodeStartIndexes[bucketIndex];
                for (int workerIndex = 0; workerIndex < WorkerCount; workerIndex++)
                {
                    int bucketElementsCountForWorker = histogramsPtr[(workerIndex * BVHUtils.RadixSortBucketCount) + bucketIndex];
                    histogramsPtr[(workerIndex * BVHUtils.RadixSortBucketCount) + bucketIndex] = bucketNodesStartIndex;
                    bucketNodesStartIndex += bucketElementsCountForWorker;
                }
            }
        }
    }

    [BurstCompile]
    internal unsafe struct BVHRadixSortJob : IJobFor
    {
        public int WorkerCount;
        public int Pass;
        [ReadOnly]
        public NativeList<BVHNode> InputNodes;
        [NativeDisableParallelForRestriction]
        public NativeList<BVHNode> OutputNodes;
        [NativeDisableParallelForRestriction]
        public NativeList<int> RadixHistograms;

        public void Execute(int workerIndex)
        {
            if (InputNodes.Length == 0)
                return;

            BVHNode* outputNodesPtr = OutputNodes.GetUnsafePtr();

            int nodesPerWorker = MathUtilities.DivideIntCeil(InputNodes.Length, WorkerCount);
            int startIndex = workerIndex * nodesPerWorker;
            int endIndex = math.min(InputNodes.Length, startIndex + nodesPerWorker);

            int bitShiftForPass = Pass * BVHUtils.RadixBits;
            int* histogramPtrForWorker = RadixHistograms.GetUnsafePtr() + (workerIndex * BVHUtils.RadixSortBucketCount);

            // Store a local copy of node start indexes for this worker
            int* nodeStartIndexesForBucket = stackalloc int[BVHUtils.RadixSortBucketCount];
            UnsafeUtility.MemCpy(nodeStartIndexesForBucket, histogramPtrForWorker, BVHUtils.RadixSortBucketCount * sizeof(int));

            // For this worker's range of nodes, perform the radix sort pass
            for (int i = startIndex; i < endIndex; i++)
            {
                BVHNode node = InputNodes[i];
                uint mortonCode = node.MortonCode;
                int bucketIndex = (int)((mortonCode >> bitShiftForPass) & (BVHUtils.RadixSortBucketCount - 1));
                int writeIndex = nodeStartIndexesForBucket[bucketIndex]++;
                outputNodesPtr[writeIndex] = node;
            }
        }
    }

    [BurstCompile]
    public unsafe struct BVHPrecomputeHierarchyJob : IJob
    {
        public NativeList<BVHNode> SortedNodes;
        public NativeList<NodeLevelData> NodeLevelDatas;

        // Resize nodes to accomodate whole hierarchy and remember data of each level
        public void Execute()
        {
            NodeLevelDatas.Clear();

            // Special case for length 1 and 0
            if (SortedNodes.Length <= 1)
            {
                if (SortedNodes.Length > 0)
                {
                    NodeLevelDatas.Add(new NodeLevelData
                    {
                        StartIndex = 0,
                        Count = SortedNodes.Length,
                    });
                }

                return;
            }

            UnsafeList<int> paddingNodeIndices = new UnsafeList<int>(64, Allocator.Temp);

            // If nodes count is not dividable by nbLeaves, add padding node
            int paddingNodesNeeded = BVHUtils.NbLeavesPerNode - (SortedNodes.Length % BVHUtils.NbLeavesPerNode);
            if (paddingNodesNeeded != BVHUtils.NbLeavesPerNode)
            {
                for (int i = 0; i < paddingNodesNeeded; i++)
                {
                    paddingNodeIndices.Add(SortedNodes.Length);
                    SortedNodes.Add(new BVHNode
                    {
                        DataIndex = -1,
                        AABB = AABB.GetEmpty(),
                    });
                }
            }

            NodeLevelDatas.Add(new NodeLevelData
            {
                StartIndex = 0,
                Count = SortedNodes.Length,
            });

            // Compute each other level
            int startIndexCounter = SortedNodes.Length;
            int workingLengthForLevel = SortedNodes.Length;
            while (workingLengthForLevel > 1)
            {
                workingLengthForLevel /= BVHUtils.NbLeavesPerNode; // ok because we always ensure our workinglength is dividable by nbLeaves

                // Ensure our workinglength is dividable by nbLeaves
                paddingNodesNeeded = BVHUtils.NbLeavesPerNode - (workingLengthForLevel % BVHUtils.NbLeavesPerNode);
                if (workingLengthForLevel > 1 && paddingNodesNeeded != BVHUtils.NbLeavesPerNode)
                {
                    for (int i = 0; i < paddingNodesNeeded; i++)
                    {
                        paddingNodeIndices.AddWithGrowFactor(startIndexCounter + workingLengthForLevel);
                        workingLengthForLevel++;
                    }
                }

                NodeLevelDatas.Add(new NodeLevelData
                {
                    StartIndex = startIndexCounter,
                    Count = workingLengthForLevel,
                });

                startIndexCounter += workingLengthForLevel;
            }

            // Resize nodes for full hierarchy
            SortedNodes.Resize(startIndexCounter, NativeArrayOptions.UninitializedMemory);

            // Set padding node data
            for (int i = 0; i < paddingNodeIndices.Length; i++)
            {
                SortedNodes[paddingNodeIndices[i]] = new BVHNode
                {
                    DataIndex = -1,
                    AABB = AABB.GetEmpty(),
                };
            }
        }
    }

    [BurstCompile]
    public unsafe struct BVHBuildHierarchyJob : IJobFor
    {
        public int WorkerCount;
        [NativeDisableParallelForRestriction]
        public NativeReference<int> ParallelWorkersLastWrittenLevel;
        [NativeDisableParallelForRestriction]
        public NativeList<BVHNode> SortedNodes;
        [ReadOnly]
        public NativeList<NodeLevelData> NodeLevelDatas;

        public void Execute(int workerIndex)
        {
            if (SortedNodes.Length < BVHUtils.NbLeavesPerNode)
                return;

            int currentLevel = 0;
            NodeLevelData nodeLevelData = NodeLevelDatas[currentLevel];
            BVHNode* sortedNodesPtr = SortedNodes.GetUnsafePtr();

            // We need each worker to start with a nodes count that is a power of NbLeaves, so that we'll have a guarantee
            // that there's an even amount of nodes to process at each level of the hierarchy other than the last
            int nodesLength = MathUtilities.DivideIntCeil(nodeLevelData.Count, WorkerCount);
            int powOfNbLeaves = 1;
            while (true)
            {
                powOfNbLeaves *= BVHUtils.NbLeavesPerNode;
                if (powOfNbLeaves > nodesLength)
                {
                    nodesLength = powOfNbLeaves;
                    break;
                }
            }

            int nodesStart = nodeLevelData.StartIndex + (workerIndex * nodesLength);
            if (nodesStart >= nodeLevelData.StartIndex + nodeLevelData.Count)
                return;

            int nodesEnd = math.min(nodeLevelData.StartIndex + nodeLevelData.Count, nodesStart + nodesLength);
            int nextLevelAddIndex = NodeLevelDatas[currentLevel + 1].StartIndex + (workerIndex * nodesLength / BVHUtils.NbLeavesPerNode);

            // For each level
            while (nodesLength >= BVHUtils.NbLeavesPerNode)
            {
                // Process all nodes except last pair
                for (int i = nodesStart; i < nodesEnd - BVHUtils.NbLeavesPerNode; i += BVHUtils.NbLeavesPerNode)
                {
                    AABB aabb = sortedNodesPtr[i].AABB;
                    for (int j = 1; j < BVHUtils.NbLeavesPerNode; j++)
                    {
                        aabb.Include(sortedNodesPtr[i + j].AABB);
                    }

                    sortedNodesPtr[nextLevelAddIndex] = new BVHNode
                    {
                        AABB = aabb,
                        DataIndex = i,
                    };

                    nextLevelAddIndex++;
                }

                // Process last pair which might have exceptions
                {
                    int lastPairIndex = nodesEnd - BVHUtils.NbLeavesPerNode;
                    AABB aabb = sortedNodesPtr[lastPairIndex].AABB;
                    for (int j = 1; j < BVHUtils.NbLeavesPerNode; j++)
                    {
                        if (sortedNodesPtr[lastPairIndex + j].IsValid())
                        {
                            aabb.Include(sortedNodesPtr[lastPairIndex + j].AABB);
                        }
                    }

                    sortedNodesPtr[nextLevelAddIndex] = new BVHNode
                    {
                        AABB = aabb,
                        DataIndex = lastPairIndex,
                    };
                    nextLevelAddIndex++;
                }

                // Reached end of level
                currentLevel++;
                nodeLevelData = NodeLevelDatas[currentLevel];
                nodesLength /= BVHUtils.NbLeavesPerNode;
                nodesStart = nodeLevelData.StartIndex + (workerIndex * nodesLength);
                nodesEnd = math.min(nodeLevelData.StartIndex + nodeLevelData.Count, nodesStart + nodesLength);
                if (currentLevel + 1 < NodeLevelDatas.Length)
                {
                    nextLevelAddIndex = NodeLevelDatas[currentLevel + 1].StartIndex + (workerIndex * nodesLength / BVHUtils.NbLeavesPerNode);
                }
            }

            if (workerIndex == 0)
            {
                ParallelWorkersLastWrittenLevel.Value = currentLevel;
            }
        }
    }

    [BurstCompile]
    public unsafe struct BVHBuildHierarchyFinalizeJob : IJob
    {
        public NativeReference<int> ParallelWorkersLastWrittenLevel;
        public NativeList<BVHNode> SortedNodes;
        public NativeList<NodeLevelData> NodeLevelDatas;

        public void Execute()
        {
            if (SortedNodes.Length < BVHUtils.NbLeavesPerNode)
                return;

            // Process the last few levels after all parallel workers ended on their last two top-level nodes
            int nextLevelAddIndex = 0;
            for (int levelIndex = ParallelWorkersLastWrittenLevel.Value; levelIndex < NodeLevelDatas.Length - 1; levelIndex++)
            {
                NodeLevelData nodeLevelData = NodeLevelDatas[levelIndex];
                nextLevelAddIndex = NodeLevelDatas[levelIndex + 1].StartIndex;

                int nodesEnd = nodeLevelData.StartIndex + nodeLevelData.Count;

                for (int i = nodeLevelData.StartIndex; i < nodesEnd - BVHUtils.NbLeavesPerNode; i += BVHUtils.NbLeavesPerNode)
                {
                    AABB aabb = SortedNodes[i].AABB;
                    for (int j = 1; j < BVHUtils.NbLeavesPerNode; j++)
                    {
                        aabb.Include(SortedNodes[i + j].AABB);
                    }
                    
                    SortedNodes[nextLevelAddIndex] = new BVHNode
                    {
                        AABB = aabb,
                        DataIndex = i,
                    };

                    nextLevelAddIndex++;
                }

                // Process last pair which might have exceptions
                {
                    int lastPairIndex = nodesEnd - BVHUtils.NbLeavesPerNode;
                    AABB aabb = SortedNodes[lastPairIndex].AABB;
                    for (int j = 1; j < BVHUtils.NbLeavesPerNode; j++)
                    {
                        if (SortedNodes[lastPairIndex + j].IsValid())
                        {
                            aabb.Include(SortedNodes[lastPairIndex + j].AABB);
                        }
                    }

                    SortedNodes[nextLevelAddIndex] = new BVHNode
                    {
                        AABB = aabb,
                        DataIndex = lastPairIndex,
                    };
                    nextLevelAddIndex++;
                }
            }
        }
    }
}