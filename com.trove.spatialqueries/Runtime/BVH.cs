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
using UnityEditor.Graphs;
using UnityEngine;

namespace Trove.SpatialQueries
{
    [StructLayout(LayoutKind.Explicit)]
    public struct BVHNode 
    {
        [FieldOffset(0)]
        public int ChildrenStartIndex; // Also serves as the nodeData's index for leaf nodes
        [FieldOffset(4)]
        public int ChildrenLength;
        [FieldOffset(8)]
        public byte ContainsLeafNodes;
        [FieldOffset(9)]
        public AABB AABB;

        public int LeafNodeDataIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ChildrenStartIndex;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => ChildrenStartIndex = value;
        }

        public int LeftIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ChildrenStartIndex;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => ChildrenStartIndex = value;
        }
        
        public int RightIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ChildrenLength;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => ChildrenLength = value;
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

    // public struct NearestNeighborResultCollector<TNodeData> : IBVHQueryCollector<NearestNeighborResult<TNodeData>>
    //     where TNodeData : unmanaged
    // {
    //     public UnsafeList<NearestNeighborResult<TNodeData>> Results;
    //
    //     public bool IsCreated => Results.IsCreated;
    //
    //     public NearestNeighborResultCollector(int capacity, Allocator allocator)
    //     {
    //         Results = new UnsafeList<NearestNeighborResult<TNodeData>>(capacity, allocator);
    //     }
    //
    //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //     public void OnBeginQuery()
    //     {
    //         Results.Clear();
    //     }
    //
    //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //     public void AddNode(in NearestNeighborResult<TNodeData> node)
    //     {
    //         Results.AddWithGrowFactor(node);
    //     }
    //
    //     [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //     public bool HasFoundResults()
    //     {
    //         return Results.Length > 0;
    //     }
    //
    //     public void Dispose()
    //     {
    //         Results.Dispose();
    //     }
    // }

    // public struct NearestNeighborsQuerier<TNodeData> where TNodeData : unmanaged
    // {
    //     internal float3 Position;
    //     internal int CurrentNodeIndexInLevel;
    //     internal int CurrentLevel;
    //     internal float MaxDistance;
    //
    //     private bool InvalidatedForNextBatches;
    //
    //     public bool NextResultsBatch(in BVH<TNodeData> bvh, ref NearestNeighborResultCollector<TNodeData> collector, bool sortResults = true)
    //     {
    //         collector.OnBeginQuery();
    //             
    //         if (CurrentLevel >= bvh.NodeLevelDatas.Length || InvalidatedForNextBatches)
    //             return false;
    //             
    //         NodeLevelData levelData = bvh.NodeLevelDatas[CurrentLevel];
    //             
    //         // Do a query at the current distance
    //         AABB currentNodeAABB = bvh.SortedNodes[levelData.StartIndex + CurrentNodeIndexInLevel].AABB;
    //         float queryDistance = math.distance(currentNodeAABB.FarthestPoint(Position), Position);
    //
    //         if (queryDistance > MaxDistance)
    //         {
    //             InvalidatedForNextBatches = true;
    //             queryDistance = math.min(queryDistance, MaxDistance);
    //         }
    //             
    //         bvh.QueryNearestNeighborsInternal(Position, queryDistance, ref collector);
    //
    //         if (collector.Results.Length == 0)
    //             return false;
    //             
    //         if (sortResults)
    //         {
    //             collector.Results.Sort();
    //         }
    //             
    //         CurrentLevel++;
    //         CurrentNodeIndexInLevel /= BVHUtils.NbLeavesPerNode; // parent node
    //
    //         return true;
    //     }
    // }
    
    // public struct NearestNeighborResult<TNodeData> : IComparable<NearestNeighborResult<TNodeData>>
    //     where TNodeData : unmanaged
    // {
    //     public TNodeData Data;
    //     public float DistanceSq;
    //
    //     public int CompareTo(NearestNeighborResult<TNodeData> other)
    //     {
    //         return DistanceSq.CompareTo(other.DistanceSq);
    //     }
    // }

    public struct BVH<TNodeData> where TNodeData : unmanaged
    {
        // Nodes A and B are used to ping pong between buffers during sorting.
        // After sorting, one of them becomes the "SortedNodes" and the other becomes the "ReorderedNodes"
        internal NativeList<BVHNode> Nodes;
        internal NativeList<TNodeData> LeafNodeDatas;
        internal NativeReference<AABB> SceneAABB;

        public static BVH<TNodeData> Create(Allocator allocator, int initialElementsCapacity)
        {
            BVH<TNodeData> bvh = new BVH<TNodeData>();
            bvh.Nodes = new NativeList<BVHNode>(initialElementsCapacity, allocator);
            bvh.LeafNodeDatas = new NativeList<TNodeData>(initialElementsCapacity, allocator);
            bvh.SceneAABB = new NativeReference<AABB>(allocator);

            return bvh;
        }

        public void Dispose(JobHandle jobHandle)
        {
            if (Nodes.IsCreated)
            {
                Nodes.Dispose(jobHandle);
            }

            if (LeafNodeDatas.IsCreated)
            {
                LeafNodeDatas.Dispose(jobHandle);
            }
            if (SceneAABB.IsCreated)
            {
                SceneAABB.Dispose(jobHandle);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddNode(in TNodeData nodeData, in AABB aabb)
        {
            ref AABB sceneAABBRef = ref *SceneAABB.GetUnsafePtr();
            sceneAABBRef.Include(aabb);

            Nodes.Add(new BVHNode
            {
                AABB = aabb,
                LeafNodeDataIndex = LeafNodeDatas.Length,
            });
            LeafNodeDatas.Add(nodeData);
        }

        public void ReserveAddNodesUnsafe(int addNodesCount, out int startIndexOfReservedRange)
        {
            startIndexOfReservedRange = Nodes.Length;
            Nodes.Resize(Nodes.Length + addNodesCount, NativeArrayOptions.UninitializedMemory);
            LeafNodeDatas.Resize(LeafNodeDatas.Length + addNodesCount, NativeArrayOptions.UninitializedMemory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNodeUnsafe(in TNodeData nodeData, in AABB aabb, int atIndex)
        {
            Nodes[atIndex] = new BVHNode
            {
                AABB = aabb,
                LeafNodeDataIndex = atIndex,
            };
            LeafNodeDatas[atIndex] = nodeData;
        }

        public unsafe void AddNodesUnsafe(TNodeData* nodeDatas, AABB* aabbs, int count, int atIndex)
        {
            const int AABBsFieldOffset = 9;

            BVHNode* nodesPtr = Nodes.GetUnsafePtr();
            BVHNode* dstNodes = nodesPtr + (long)atIndex;
            AABB* dstAABB = (AABB*)((byte*)dstNodes + (long)AABBsFieldOffset); // AABBs are at fieldOffset 8
            UnsafeUtility.MemCpyStride(
                dstAABB, AABBsFieldOffset, 
                aabbs, 0, 
                UnsafeUtility.SizeOf<AABB>(), count);

            for (int i = atIndex; i < atIndex + count; i++)
            {
                ref BVHNode nodeRef = ref UnsafeUtility.ArrayElementAsRef<BVHNode>(nodesPtr, i);
                nodeRef.LeafNodeDataIndex = i;
            }
            
            TNodeData* dstNodeDatas = LeafNodeDatas.GetUnsafePtr() + (long)atIndex;
            UnsafeUtility.MemCpy(dstNodeDatas, nodeDatas, UnsafeUtility.SizeOf<TNodeData>() * count);
        }

        public unsafe bool QueryAABB<TCollector>(in AABB aabb, ref TCollector collector) 
            where TCollector : unmanaged, IBVHQueryCollector<TNodeData>
        {
            collector.OnBeginQuery();
        
            if (Nodes.Length < 1)
            {
                return false;
            }
        
            Stack nodesStack = new Stack(256);
            int* nodesStackPtr = stackalloc int[nodesStack.Capacity];
            BVHNode* nodesPtr = Nodes.GetUnsafeReadOnlyPtr();
            TNodeData* leafDataPtr = LeafNodeDatas.GetUnsafeReadOnlyPtr();
            int leafNodesCount = LeafNodeDatas.Length;

            nodesStack.PushLast(nodesStackPtr, leafNodesCount);  // start at root node;
            while (nodesStack.PopLast(nodesStackPtr, out int nodeIndex))
            {
                BVHNode node = nodesPtr[nodeIndex];

                if (!aabb.OverlapsAABB(node.AABB))
                    continue;

                if (nodeIndex < leafNodesCount)
                {
                    collector.AddNode(leafDataPtr[node.LeafNodeDataIndex]);
                }
                else if (node.ContainsLeafNodes == 1)
                {
                    // Add leaf nodes
                    for (int i = node.ChildrenStartIndex; i < node.ChildrenStartIndex + node.ChildrenLength; i++)
                    {
                        nodesStack.PushLast(nodesStackPtr, i);
                    }
                }
                else
                {
                    nodesStack.PushLast(nodesStackPtr, node.LeftIndex);
                    nodesStack.PushLast(nodesStackPtr, node.RightIndex);
                }
            }

            return collector.HasFoundResults();
        }

        // public unsafe bool QuerySphere<TCollector>(in float3 position, float radius, ref TCollector collector)
        //     where TCollector : unmanaged, IBVHQueryCollector<TNodeData>
        // {
        //     collector.OnBeginQuery();
        //
        //     if (SortedNodes.Length < 1)
        //     {
        //         return false;
        //     }
        //
        //     Stack nodesStack = new Stack(256);
        //     int* nodesStackPtr = stackalloc int[nodesStack.Capacity];
        //     BVHNode* nodesPtr = SortedNodes.GetUnsafeReadOnlyPtr();
        //     TNodeData* leafDataPtr = LeafNodeDatas.GetUnsafeReadOnlyPtr();
        //     int leafNodesCount = LeafNodeDatas.Length;
        //     
        //     float radiusSq = radius * radius;
        //
        //     nodesStack.PushLast(nodesStackPtr, SortedNodes.Length - 1);  // start at root node;
        //     while (nodesStack.PopLast(nodesStackPtr, out int nodeIndex))
        //     {
        //         BVHNode node = nodesPtr[nodeIndex];
        //
        //         if (!node.AABB.OverlapsSphere(position, radiusSq) || !node.IsValid())
        //             continue;
        //
        //         if (nodeIndex < leafNodesCount)
        //         {
        //             collector.AddNode(leafDataPtr[node.DataIndex]);
        //         }
        //         else
        //         {
        //             for (int i = 0; i < BVHUtils.NbLeavesPerNode; i++)
        //             {
        //                 nodesStack.PushLast(nodesStackPtr, node.DataIndex + i);
        //             }
        //         }
        //     }
        //
        //     return collector.HasFoundResults();
        // }
        //
        // public unsafe bool QueryRay<TCollector>(float3 rayOrigin, float3 rayDirectionNormalized, float rayLength,
        //     ref TCollector collector)
        //     where TCollector : unmanaged, IBVHQueryCollector<TNodeData>
        // {
        //     collector.OnBeginQuery();
        //
        //     if (SortedNodes.Length < 1)
        //     {
        //         return false;
        //     }
        //
        //     Stack nodesStack = new Stack(256);
        //     int* nodesStackPtr = stackalloc int[nodesStack.Capacity];
        //     BVHNode* nodesPtr = SortedNodes.GetUnsafeReadOnlyPtr();
        //     TNodeData* leafDataPtr = LeafNodeDatas.GetUnsafeReadOnlyPtr();
        //     int leafNodesCount = LeafNodeDatas.Length;
        //
        //     nodesStack.PushLast(nodesStackPtr, SortedNodes.Length - 1);  // start at root node;
        //     while (nodesStack.PopLast(nodesStackPtr, out int nodeIndex))
        //     {
        //         BVHNode node = nodesPtr[nodeIndex];
        //
        //         if (!node.AABB.IntersectsRay(rayOrigin, rayDirectionNormalized, rayLength) || !node.IsValid())
        //             continue;
        //
        //         if (nodeIndex < leafNodesCount)
        //         {
        //             collector.AddNode(leafDataPtr[node.DataIndex]);
        //         }
        //         else
        //         {
        //             for (int i = 0; i < BVHUtils.NbLeavesPerNode; i++)
        //             {
        //                 nodesStack.PushLast(nodesStackPtr, node.DataIndex + i);
        //             }
        //         }
        //     }
        //
        //     return collector.HasFoundResults();
        // }
        //
        // public bool QueryNearestNeighbor(float3 position, ref NearestNeighborResultCollector<TNodeData> collector, 
        //     out NearestNeighborResult<TNodeData> nearestResult, float maxDistance = float.MaxValue)
        // {
        //     if (CreateNearestNeighborsQuerier(position, out NearestNeighborsQuerier<TNodeData> querier, maxDistance))
        //     {
        //         if(querier.NextResultsBatch(in this, ref collector, false))
        //         {
        //             UnsafeList<NearestNeighborResult<TNodeData>> results = collector.Results;
        //             nearestResult = results[0];
        //             for (int i = 1; i < results.Length; i++)
        //             {
        //                 if (results[i].DistanceSq < nearestResult.DistanceSq)
        //                 {
        //                     nearestResult = results[i];
        //                 }
        //             }
        //             return true;
        //         }
        //     }
        //
        //     nearestResult = default;
        //     return false;
        // }
        //
        // public unsafe bool CreateNearestNeighborsQuerier(float3 position, out NearestNeighborsQuerier<TNodeData> querier, float maxDistance = float.MaxValue)
        // {
        //     // Project position onto Scene AABB if not inside it
        //     if (!SceneAABB.Value.Contains(position))
        //     {
        //         float3 positionOnScene = SceneAABB.Value.ClosestPoint(position);
        //         float3 positionToSceneNorm = math.normalize(positionOnScene - position);
        //         position = positionOnScene + (positionToSceneNorm * 0.1f);
        //     }
        //
        //     int deepestSmallestContainingNodeIndex = int.MaxValue;
        //     float deepestSmallestContainingNodeVolume = float.MaxValue;
        //     if (SortedNodes.Length >= 1)
        //     {
        //         // Calculate the morton code of the position
        //         float3 sceneDimensions = SceneAABB.Value.Max - SceneAABB.Value.Min;
        //         float3 normalizedPositionInScene = (position - SceneAABB.Value.Min) / sceneDimensions; 
        //         uint queriedMortonCode = BVHUtils.ComputeMortonCode(normalizedPositionInScene);
        //         
        //         // Approximate the index of this morton code in sorted leaf nodes
        //         float normMortonValue = (float)queriedMortonCode / (float)uint.MaxValue;
        //         int queriedNodeIndex = (int)math.round(LeafNodeDatas.Length * normMortonValue);
        //         
        //         // Search for closest morton from that index
        //         int indexOfClosestMorton = -1;
        //         uint iteratedMorton = SortedNodes[queriedNodeIndex].MortonCode;
        //         if (iteratedMorton == queriedMortonCode)
        //         {
        //             indexOfClosestMorton = queriedNodeIndex;
        //         }
        //         else
        //         {
        //             // binary search for match
        //             bool iteratedMortonIsGreater = iteratedMorton > queriedMortonCode;
        //             int minIndex = iteratedMortonIsGreater ? 0 : queriedNodeIndex;
        //             int maxIndex = iteratedMortonIsGreater ? queriedNodeIndex : LeafNodeDatas.Length ;
        //         
        //             while (maxIndex - minIndex > 1)
        //             {
        //                 queriedNodeIndex = minIndex + ((maxIndex - minIndex) / 2);
        //                 iteratedMorton = SortedNodes[queriedNodeIndex].MortonCode;
        //                 if (iteratedMorton == queriedMortonCode)
        //                 {
        //                     break;
        //                 }
        //                 else
        //                 {
        //                     // Update min and max
        //                     iteratedMortonIsGreater = iteratedMorton > queriedMortonCode;
        //                     minIndex = iteratedMortonIsGreater ? minIndex : queriedNodeIndex;
        //                     maxIndex = iteratedMortonIsGreater ? queriedNodeIndex : maxIndex;
        //                 }
        //             }
        //             
        //             indexOfClosestMorton = queriedNodeIndex;
        //         }
        //
        //         if (indexOfClosestMorton >= 0)
        //         {
        //             float3 iteratedNodePos = SortedNodes[queriedNodeIndex].AABB.GetCenter();
        //             float closestDistanceSqSoFar = math.distancesq(position, iteratedNodePos);
        //             
        //             // Find the closest in a range of X from that node
        //             // (this mitigates the impact of large jumps in decoded morton code positions)
        //             int halfRange = 9;
        //             int startIndex = math.max(0, indexOfClosestMorton - halfRange);
        //             int endIndex = math.min(LeafNodeDatas.Length, indexOfClosestMorton + halfRange);
        //             for (int i = startIndex; i <= endIndex; i++)
        //             {
        //                 iteratedNodePos = SortedNodes[i].AABB.GetCenter();
        //                 float distanceSq = math.distancesq(position, iteratedNodePos);
        //                 if (distanceSq < closestDistanceSqSoFar)
        //                 {
        //                     indexOfClosestMorton = i;
        //                     closestDistanceSqSoFar = distanceSq;
        //                 }
        //             }
        //             
        //             querier = new NearestNeighborsQuerier<TNodeData>
        //             {
        //                 Position = position,
        //                 CurrentNodeIndexInLevel = indexOfClosestMorton,
        //                 CurrentLevel = 0,
        //                 MaxDistance = maxDistance,
        //             };
        //             return true;
        //         }
        //     }
        //
        //     querier = default;
        //     return false;
        // }
        //
        // internal unsafe void QueryNearestNeighborsInternal(in float3 position, float radius, ref NearestNeighborResultCollector<TNodeData> collector)
        // {
        //     collector.OnBeginQuery();
        //
        //     if (SortedNodes.Length < 1)
        //     {
        //         return;
        //     }
        //
        //     Stack nodesStack = new Stack(256);
        //     int* nodesStackPtr = stackalloc int[nodesStack.Capacity];
        //     BVHNode* nodesPtr = SortedNodes.GetUnsafeReadOnlyPtr();
        //     TNodeData* leafDataPtr = LeafNodeDatas.GetUnsafeReadOnlyPtr();
        //     int leafNodesCount = LeafNodeDatas.Length;
        //
        //     float radiusSq = radius * radius;
        //     
        //     nodesStack.PushLast(nodesStackPtr, SortedNodes.Length - 1);  // start at root node;
        //     while (nodesStack.PopLast(nodesStackPtr, out int nodeIndex))
        //     {
        //         BVHNode node = nodesPtr[nodeIndex];
        //
        //         if (!node.AABB.OverlapsSphere(position, radiusSq) || !node.IsValid())
        //             continue;
        //
        //         if (nodeIndex < leafNodesCount)
        //         {
        //             collector.AddNode(new NearestNeighborResult<TNodeData>
        //             {
        //                 Data = leafDataPtr[node.DataIndex],
        //                 DistanceSq = node.AABB.DistanceSq(position),
        //             });
        //         }
        //         else
        //         {
        //             for (int i = 0; i < BVHUtils.NbLeavesPerNode; i++)
        //             {
        //                 nodesStack.PushLast(nodesStackPtr, node.DataIndex + i);
        //             }
        //         }
        //     }
        // }
        
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
                UnsortedNodes = Nodes,
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

            dep = new BVHBuildHierarchyMidpointSplitJob()
            {
                SceneAABB = SceneAABB,
                Nodes = Nodes,
            }.Schedule(dep);

            return dep;
        }

        public unsafe void GetNodes(out UnsafeList<BVHNode> nodes, out int leafNodesCount, out AABB sceneAABB)
        {
            nodes = (*Nodes.GetUnsafeList());
            leafNodesCount = LeafNodeDatas.Length;
            sceneAABB = SceneAABB.Value;
        }

        [BurstCompile]
        public struct BVHClearJob : IJob
        {
            public BVH<TNodeData> BVH;

            public void Execute()
            {
                BVH.Nodes.Clear();
                BVH.LeafNodeDatas.Clear();
                BVH.SceneAABB.Value = AABB.GetEmpty();
            }
        }
    }

    internal static class BVHUtils
    {
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
    public unsafe struct BVHBuildHierarchyMidpointSplitJob : IJob
    {
        struct WorkingNode
        {
            public BVHNode Node;
            public int depth;
            public int parentIndex;
            public bool isLeftChild;
        }
        
        public NativeReference<AABB> SceneAABB;
        public NativeList<BVHNode> Nodes;

        [NativeDisableUnsafePtrRestriction]
        private BVHNode* NodesPtr;

        private const int MaxLeavesPerNode = 4; 
        private const int MaxDepth = 60;

        public void Execute()
        {
            NodesPtr = Nodes.GetUnsafePtr();
            
            Stack nodesStack = new Stack(256);
            WorkingNode* nodesStackPtr = stackalloc WorkingNode[nodesStack.Capacity];
            
            WorkingNode rootWorkingNode = new WorkingNode
            {
                Node = new BVHNode
                {
                    AABB = SceneAABB.Value,
                    ChildrenStartIndex = 0,
                    ChildrenLength = Nodes.Length,
                },
                depth = 0,
                parentIndex = -1,
                isLeftChild = false,
            };
            
            nodesStack.PushLast(nodesStackPtr, rootWorkingNode);
            while (nodesStack.PopLast(nodesStackPtr, out WorkingNode workingNode))
            {
                int addedIndex;
                
                // Add to hierarchy if few enough children, or if exceed depth limit
                if (workingNode.Node.ChildrenLength < MaxLeavesPerNode || workingNode.depth >= MaxDepth)
                {
                    AddNodeToHierarchy(ref workingNode, true, out addedIndex);
                    
                    // Add each leaf at the end
                    // int prevCapacity = Nodes.Capacity;
                    // ref BVHNode addedNode =
                    //     ref UnsafeUtility.ArrayElementAsRef<BVHNode>(NodesPtr, addedIndex);
                    // int prevChildrenStartIndex = addedNode.ChildrenStartIndex;
                    // addedNode.ChildrenStartIndex = Nodes.Length; // patch children start
                    // for (int i = 0; i < addedNode.ChildrenLength; i++)
                    // {
                    //     Nodes.Add(Nodes[prevChildrenStartIndex + i]);
                    // }
                    // if (Nodes.Capacity != prevCapacity)
                    // {
                    //     // TODO: handle this better?
                    //     NodesPtr = Nodes.GetUnsafePtr();
                    // }
                    
                    continue;
                }

                // Add node to hierarchy
                AddNodeToHierarchy(ref workingNode, false, out addedIndex);

                // Find the best split (longest AABB axis)
                int splitAxis = 0;
                float splitPosition = 0f;
                {
                    float3 nodeExtents = workingNode.Node.AABB.GetExtents();
                    float splitAxisExtent = nodeExtents[splitAxis];
                    for (int axis = 1; axis < 3; axis++)
                    {
                        float tmpExtent = nodeExtents[axis];
                        if (tmpExtent > splitAxisExtent)
                        {
                            splitAxisExtent = tmpExtent;
                            splitAxis = axis;
                        }
                    }

                    splitPosition = workingNode.Node.AABB.Min[splitAxis] + splitAxisExtent;
                }

                BVHNode leftNode = new BVHNode
                {
                    AABB = AABB.GetEmpty(),
                    ChildrenStartIndex = workingNode.Node.ChildrenStartIndex,
                    ChildrenLength = 0,
                };
                BVHNode rightNode = new BVHNode
                {
                    AABB = AABB.GetEmpty(),
                    ChildrenStartIndex = -1, // we don't know yet
                    ChildrenLength = 0,
                };

                // Reorder children in the buffer range so that it contains all left children, then all right children
                {
                    for (int leftNodeIndex = workingNode.Node.ChildrenStartIndex;
                         leftNodeIndex < workingNode.Node.ChildrenStartIndex + workingNode.Node.ChildrenLength;
                         leftNodeIndex++)
                    {
                        BVHNode childFromLeft = NodesPtr[leftNodeIndex];
                        float centerOnAxisChildFromLeft = childFromLeft.AABB.GetCenter()[splitAxis];

                        if (centerOnAxisChildFromLeft < splitPosition)
                        {
                            leftNode.AABB.Include(childFromLeft.AABB);
                            leftNode.ChildrenLength++;
                        }
                        else
                        {
                            // If node goes on the right, iterate nodes from the right until we find one that goes left.
                            // Then swap them
                            for (int rightNodeIndex = workingNode.Node.ChildrenStartIndex + workingNode.Node.ChildrenLength - 1 - rightNode.ChildrenLength;
                                 rightNodeIndex >= leftNodeIndex; rightNodeIndex--)
                            {
                                BVHNode childFromRight = NodesPtr[rightNodeIndex];
                                float centerOnAxisChildFromRight = childFromRight.AABB.GetCenter()[splitAxis];

                                if (centerOnAxisChildFromRight >= splitPosition)
                                {
                                    rightNode.AABB.Include(childFromRight.AABB);
                                    rightNode.ChildrenLength++;
                                }
                                else
                                {
                                    // Swap
                                    BVHNode tmpChildFromRight = childFromRight;
                                    NodesPtr[rightNodeIndex] = childFromLeft;
                                    NodesPtr[leftNodeIndex] = tmpChildFromRight;

                                    leftNode.AABB.Include(childFromRight.AABB);
                                    leftNode.ChildrenLength++;

                                    rightNode.AABB.Include(childFromLeft.AABB);
                                    rightNode.ChildrenLength++;

                                    break;
                                }

                                if (rightNodeIndex == leftNodeIndex)
                                {
                                    break;
                                }
                            }
                        }
                    }

                    // Patch right node start index
                    rightNode.ChildrenStartIndex = leftNode.ChildrenStartIndex + leftNode.ChildrenLength;
                }
                
                int nextDepth = workingNode.depth + 1;
                nodesStack.PushLast(nodesStackPtr, new WorkingNode
                {
                    Node = leftNode,
                    depth = nextDepth,
                    parentIndex = addedIndex,
                    isLeftChild = true,
                });
                nodesStack.PushLast(nodesStackPtr, new WorkingNode
                {
                    Node = rightNode,
                    depth = nextDepth,
                    parentIndex = addedIndex,
                    isLeftChild = false,
                });
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddNodeToHierarchy(ref WorkingNode node, bool containsLeafNodes, out int addedIndex)
        {
            addedIndex = Nodes.Length;
            if (node.parentIndex >= 0)
            {
                ref BVHNode parent =
                    ref UnsafeUtility.ArrayElementAsRef<BVHNode>(NodesPtr, node.parentIndex);
                if (node.isLeftChild)
                {
                    parent.LeftIndex = addedIndex;
                }
                else
                {
                    parent.RightIndex = addedIndex;
                }
            }
             
            node.Node.ContainsLeafNodes = containsLeafNodes ? (byte)1 : (byte)0;

            int prevCapacity = Nodes.Capacity;
            Nodes.Add(node.Node);
            if (Nodes.Capacity != prevCapacity)
            {
                // TODO: handle this better?
                NodesPtr = Nodes.GetUnsafePtr();
            }
        }
    }
}