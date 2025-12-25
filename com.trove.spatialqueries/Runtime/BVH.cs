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
    public struct BVHLeafNode
    {
        [FieldOffset(0)]
        public int DataIndex;
        [FieldOffset(4)]
        public AABB AABB;
    }
    
    public struct BVHHierarchyNode 
    {
        public int4 ChildIndices; 
        public byte ContainsLeafNodes;
        public AABB AABB;
        
        public int ChildrenStartIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ChildIndices[0];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => ChildIndices[0] = value;
        }
        
        public int ChildrenLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ChildIndices[1];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => ChildIndices[1] = value;
        }
    }
    
    public struct BVHSortingLeafNode
    {
        public ushort3 Pos;
        public int OriginalIndex;
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
        
        internal NativeList<BVHLeafNode> UnsortedLeafNodes;
        internal NativeList<BVHLeafNode> SortedLeafNodes;
        internal NativeList<BVHSortingLeafNode> SortingLeafNodes;
        internal NativeList<BVHSortingLeafNode> TmpSortingLeafNodes;
        internal NativeList<BVHHierarchyNode> HierarchyNodes;
        internal NativeList<TNodeData> LeafNodeDatas;
        internal NativeReference<AABB> SceneAABB;

        public static BVH<TNodeData> Create(Allocator allocator, int initialElementsCapacity)
        {
            BVH<TNodeData> bvh = new BVH<TNodeData>();
            bvh.UnsortedLeafNodes = new NativeList<BVHLeafNode>(initialElementsCapacity, allocator);
            bvh.SortedLeafNodes = new NativeList<BVHLeafNode>(initialElementsCapacity, allocator);
            bvh.SortingLeafNodes = new NativeList<BVHSortingLeafNode>(initialElementsCapacity, allocator);
            bvh.TmpSortingLeafNodes = new NativeList<BVHSortingLeafNode>(initialElementsCapacity, allocator);
            bvh.HierarchyNodes = new NativeList<BVHHierarchyNode>(128, allocator);
            bvh.LeafNodeDatas = new NativeList<TNodeData>(initialElementsCapacity, allocator);
            bvh.SceneAABB = new NativeReference<AABB>(allocator);

            return bvh;
        }

        public void Dispose(JobHandle jobHandle)
        {
            if (UnsortedLeafNodes.IsCreated)
            {
                UnsortedLeafNodes.Dispose(jobHandle);
            }
            
            if (SortedLeafNodes.IsCreated)
            {
                SortedLeafNodes.Dispose(jobHandle);
            }
            
            if (SortingLeafNodes.IsCreated)
            {
                SortingLeafNodes.Dispose(jobHandle);
            }
            
            if (TmpSortingLeafNodes.IsCreated)
            {
                TmpSortingLeafNodes.Dispose(jobHandle);
            }
            
            if (HierarchyNodes.IsCreated)
            {
                HierarchyNodes.Dispose(jobHandle);
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

            UnsortedLeafNodes.Add(new BVHLeafNode
            {
                AABB = aabb,
                DataIndex = LeafNodeDatas.Length,
            });
            LeafNodeDatas.Add(nodeData);
        }

        public void ReserveAddNodesUnsafe(int addNodesCount, out int startIndexOfReservedRange)
        {
            startIndexOfReservedRange = UnsortedLeafNodes.Length;
            UnsortedLeafNodes.Resize(UnsortedLeafNodes.Length + addNodesCount, NativeArrayOptions.UninitializedMemory);
            LeafNodeDatas.Resize(LeafNodeDatas.Length + addNodesCount, NativeArrayOptions.UninitializedMemory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddNodeUnsafe(in TNodeData nodeData, in AABB aabb, int atIndex)
        {
            UnsortedLeafNodes[atIndex] = new BVHLeafNode
            {
                AABB = aabb,
                DataIndex = atIndex,
            };
            LeafNodeDatas[atIndex] = nodeData;
        }

        public unsafe void AddNodesUnsafe(TNodeData* nodeDatas, AABB* aabbs, int count, int atIndex)
        {
            const int AABBsFieldOffset = 4;

            BVHLeafNode* nodesPtr = UnsortedLeafNodes.GetUnsafePtr();
            BVHLeafNode* dstNodes = nodesPtr + (long)atIndex;
            AABB* dstAABB = (AABB*)((byte*)dstNodes + (long)AABBsFieldOffset); // AABBs are at fieldOffset 8
            UnsafeUtility.MemCpyStride(
                dstAABB, AABBsFieldOffset, 
                aabbs, 0, 
                UnsafeUtility.SizeOf<AABB>(), count);

            for (int i = atIndex; i < atIndex + count; i++)
            {
                ref BVHLeafNode nodeRef = ref UnsafeUtility.ArrayElementAsRef<BVHLeafNode>(nodesPtr, i);
                nodeRef.DataIndex = i;
            }
            
            TNodeData* dstNodeDatas = LeafNodeDatas.GetUnsafePtr() + (long)atIndex;
            UnsafeUtility.MemCpy(dstNodeDatas, nodeDatas, UnsafeUtility.SizeOf<TNodeData>() * count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool QueryAABB<TCollector>(in AABB aabb, ref TCollector collector) 
            where TCollector : unmanaged, IBVHQueryCollector<TNodeData>
        {
            collector.OnBeginQuery();
        
            if (SortedLeafNodes.Length < 1)
            {
                return false;
            }
        
            Stack nodesStack = new Stack(256);
            int* nodesStackPtr = stackalloc int[nodesStack.Capacity];
            BVHLeafNode* leafNodesPtr = SortedLeafNodes.GetUnsafeReadOnlyPtr();
            BVHHierarchyNode* hierarchyNodesPtr = HierarchyNodes.GetUnsafeReadOnlyPtr();
            TNodeData* leafDataPtr = LeafNodeDatas.GetUnsafeReadOnlyPtr();

            nodesStack.PushLast(nodesStackPtr, 0);  // start at root node;
            while (nodesStack.PopLast(nodesStackPtr, out int nodeIndex))
            {
                BVHHierarchyNode hierarchyNode = hierarchyNodesPtr[nodeIndex];

                if (!aabb.OverlapsAABB(hierarchyNode.AABB))
                    continue;

                if (hierarchyNode.ContainsLeafNodes == 1)
                {
                    // Add leaf nodes
                    for (int i = hierarchyNode.ChildrenStartIndex; i < hierarchyNode.ChildrenStartIndex + hierarchyNode.ChildrenLength; i++)
                    {
                        BVHLeafNode leafNode = leafNodesPtr[i];
                        if (aabb.OverlapsAABB(leafNode.AABB))
                        {
                            collector.AddNode(leafDataPtr[leafNode.DataIndex]);
                        }
                    }
                }
                else
                {
                    nodesStack.PushLast(nodesStackPtr, hierarchyNode.ChildIndices[3]);
                    nodesStack.PushLast(nodesStackPtr, hierarchyNode.ChildIndices[2]);
                    nodesStack.PushLast(nodesStackPtr, hierarchyNode.ChildIndices[1]);
                    nodesStack.PushLast(nodesStackPtr, hierarchyNode.ChildIndices[0]);
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
                LeafNodes = UnsortedLeafNodes,
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
                UnsortedLeafNodes = UnsortedLeafNodes,
                SortedLeafNodes = SortedLeafNodes,
                SortingLeafNodes = SortingLeafNodes,
                TmpSortingLeafNodes = TmpSortingLeafNodes,
                HierarchyNodes = HierarchyNodes,
            }.Schedule(dep);

            return dep;
        }

        public unsafe void GetNodes(out UnsafeList<BVHLeafNode> leafNodes, out UnsafeList<BVHHierarchyNode> hierarchyNodes, out AABB sceneAABB)
        {
            leafNodes = *SortedLeafNodes.GetUnsafeList();
            hierarchyNodes = *HierarchyNodes.GetUnsafeList();
            sceneAABB = SceneAABB.Value;
        }

        [BurstCompile]
        public struct BVHClearJob : IJob
        {
            public BVH<TNodeData> BVH;

            public void Execute()
            {
                BVH.UnsortedLeafNodes.Clear();
                BVH.SortedLeafNodes.Clear();
                BVH.SortingLeafNodes.Clear();
                BVH.TmpSortingLeafNodes.Clear();
                BVH.HierarchyNodes.Clear();
                BVH.LeafNodeDatas.Clear();
                BVH.SceneAABB.Value = AABB.GetEmpty();
            }
        }
    }

    internal static class BVHUtils
    {
        internal const int BitsPerByte = 8;
        internal const int ValuesPerByte = 1 << BitsPerByte; // 256 values of a byte
        internal const int RadixSortPassesUInt = 4; 
        internal const int RadixSortPassesUShort = 2;
        
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
        public NativeList<BVHLeafNode> LeafNodes;
        [NativeDisableParallelForRestriction]
        public NativeArray<AABB> AABBForWorker;

        public void Execute(int workerIndex)
        {
            int nodesPerWorker = MathUtilities.DivideIntCeil(LeafNodes.Length, WorkerCount);
            int startIndex = workerIndex * nodesPerWorker;
            int endIndex = math.min(LeafNodes.Length, startIndex + nodesPerWorker);

            AABB sceneAABB = AABB.GetEmpty();
            for (int i = startIndex; i < endIndex; i++)
            {
                sceneAABB.Include(LeafNodes[i].AABB);
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
        struct WorkingHierarchyNode
        {
            public BVHHierarchyNode Node;
            public int Depth;
            public int ParentIndex;
            public int ChildIndex;
        }
        
        public NativeReference<AABB> SceneAABB;
        public NativeList<BVHLeafNode> UnsortedLeafNodes;
        public NativeList<BVHLeafNode> SortedLeafNodes;
        public NativeList<BVHSortingLeafNode> SortingLeafNodes;
        public NativeList<BVHSortingLeafNode> TmpSortingLeafNodes;
        public NativeList<BVHHierarchyNode> HierarchyNodes;

        [NativeDisableUnsafePtrRestriction]
        private BVHHierarchyNode* HierarchyNodesPtr;

        private const int MaxLeavesPerNode = 4; 
        private const int MaxDepth = 60;

        public void Execute()
        {
            // Cache node array ptrs
            HierarchyNodesPtr = HierarchyNodes.GetUnsafePtr();
            BVHLeafNode* unsortedLeafNodesPtr = UnsortedLeafNodes.GetUnsafePtr();
            SortingLeafNodes.Resize(UnsortedLeafNodes.Length, NativeArrayOptions.ClearMemory);
            BVHSortingLeafNode* sortingLeafNodesPtr = SortingLeafNodes.GetUnsafePtr();
            TmpSortingLeafNodes.Resize(UnsortedLeafNodes.Length, NativeArrayOptions.ClearMemory);
            BVHSortingLeafNode* tmpSortingLeafNodesPtr = TmpSortingLeafNodes.GetUnsafePtr();
            SortedLeafNodes.Resize(UnsortedLeafNodes.Length, NativeArrayOptions.ClearMemory);
            BVHLeafNode* sortedLeafNodesPtr = SortedLeafNodes.GetUnsafePtr();
            
            // Build the SortingLeafNodes
            float3 sceneAABBMin = SceneAABB.Value.Min;
            float3 sceneAABBDimensions = SceneAABB.Value.Max - SceneAABB.Value.Min;
            for (int i = 0; i < UnsortedLeafNodes.Length; i++)
            {
                float3 leafNodeLocalCenterNormalized = (unsortedLeafNodesPtr[i].AABB.GetCenter() - sceneAABBMin) / sceneAABBDimensions;
                sortingLeafNodesPtr[i] = new BVHSortingLeafNode
                {
                    OriginalIndex = i,
                    Pos = new ushort3(
                        (ushort)math.floor((int)(leafNodeLocalCenterNormalized.x * ushort.MaxValue)),
                        (ushort)math.floor((int)(leafNodeLocalCenterNormalized.y * ushort.MaxValue)),
                        (ushort)math.floor((int)(leafNodeLocalCenterNormalized.z * ushort.MaxValue))),
                };
            }
            
            WorkingHierarchyNode rootWorkingHierarchyNode = new WorkingHierarchyNode
            {
                Node = new BVHHierarchyNode
                {
                    AABB = SceneAABB.Value,
                    ChildrenStartIndex = 0,
                    ChildrenLength = UnsortedLeafNodes.Length,
                },
                Depth = 0,
                ParentIndex = -1,
                ChildIndex = -1,
            };

            if (rootWorkingHierarchyNode.Node.ChildrenLength < MaxLeavesPerNode)
            {
                AddNodeToHierarchy(ref rootWorkingHierarchyNode, true, out _);
            }
            else if (rootWorkingHierarchyNode.Node.ChildrenLength > 0)
            {
                int* radixSortHistogram = stackalloc int[BVHUtils.ValuesPerByte];
                int* bucketNodeStartIndexes = stackalloc int[BVHUtils.ValuesPerByte];

                // Build hierarchy from the top-down, using midpoint split
                Stack nodesStack = new Stack(256);
                WorkingHierarchyNode* nodesStackPtr = stackalloc WorkingHierarchyNode[nodesStack.Capacity];
                nodesStack.PushLast(nodesStackPtr, rootWorkingHierarchyNode);
                while (nodesStack.PopLast(nodesStackPtr, out WorkingHierarchyNode workingHierarchyNode))
                {
                    int addedIndex;

                    // Add node to hierarchy
                    AddNodeToHierarchy(ref workingHierarchyNode, false, out addedIndex);

                    int childrenLength = workingHierarchyNode.Node.ChildrenLength;
                    int childrenStart = workingHierarchyNode.Node.ChildrenStartIndex;
                    int childrenEnd = childrenStart + childrenLength;

                    // Find the best split axis (longest axis)
                    int splitAxis = 0;
                    {
                        // Find min/max pos on each axis
                        ushort3 minPos = new ushort3(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);
                        ushort3 maxPos = new ushort3(ushort.MinValue, ushort.MinValue, ushort.MinValue);
                        for (int i = childrenStart; i < childrenEnd; i++)
                        {
                            ushort3 pos = sortingLeafNodesPtr[i].Pos;
                            minPos = MathUtilities.min(minPos, pos);
                            maxPos = MathUtilities.max(maxPos, pos);
                        }

                        // Find split axis
                        ushort3 dimensions = new ushort3(maxPos.x - minPos.x, maxPos.y - minPos.y, maxPos.z - minPos.z);
                        ushort splitAxisDimension = dimensions[splitAxis];
                        for (int axis = 1; axis < 3; axis++)
                        {
                            ushort tmpDimension = dimensions[axis];
                            if (tmpDimension > splitAxisDimension)
                            {
                                splitAxisDimension = tmpDimension;
                                splitAxis = axis;
                            }
                        }
                    }

                    // Sort nodes on split axis
                    BVHSortingLeafNode* sortingLeafNodesRangePtr = sortingLeafNodesPtr + (long)childrenStart;
                    BVHSortingLeafNode* tmpSortingLeafNodesRangePtr = tmpSortingLeafNodesPtr + (long)childrenStart;
                    RadixSortLeavesRangeOnAxis(
                        sortingLeafNodesRangePtr,
                        tmpSortingLeafNodesRangePtr,
                        childrenLength,
                        radixSortHistogram,
                        bucketNodeStartIndexes,
                        splitAxis);

                    int nextDepth = workingHierarchyNode.Depth + 1;
                    int lengthPerChildren = MathUtilities.DivideIntCeil(childrenLength, 4); 

                    // Add child nodes to stack or to hierarchy if leaf
                    WorkingHierarchyNode childNode = new WorkingHierarchyNode
                    {
                        Node = new BVHHierarchyNode
                        {
                            AABB = AABB.GetEmpty(),
                        }, 
                        Depth = nextDepth,
                        ParentIndex = addedIndex,
                    }; 
                    for (int i = 3; i >= 0; i--)
                    {
                        childNode.ChildIndex = i;
                        childNode.Node.ChildrenStartIndex = childrenStart + (lengthPerChildren * i);
                        childNode.Node.ChildrenLength =  math.min(childrenEnd, childNode.Node.ChildrenStartIndex + lengthPerChildren) -
                                                         childNode.Node.ChildrenStartIndex;
                        
                        // Add to hierarchy as leaf if few enough children or if exceed depth limit.
                        // Otherwise, push to hierarchy stack
                        if (childNode.Node.ChildrenLength < MaxLeavesPerNode || childNode.Depth >= MaxDepth)
                        {
                            AddNodeToHierarchy(ref childNode, true, out addedIndex);
                        }
                        else if (childNode.Node.ChildrenLength > 0)
                        {
                            nodesStack.PushLast(nodesStackPtr, childNode);
                        }
                    }
                }

                // Reorder source leaf nodes
                for (int i = 0; i < SortingLeafNodes.Length; i++)
                {
                    sortedLeafNodesPtr[i] = unsortedLeafNodesPtr[sortingLeafNodesPtr[i].OriginalIndex];
                }

                // Once hierarchy is built and leaf nodes sorted, rebuild hierarchy node AABBs from bottom-up
                for (int i = HierarchyNodes.Length - 1; i >= 0; i--)
                {
                    ref BVHHierarchyNode nodeRef =
                        ref UnsafeUtility.ArrayElementAsRef<BVHHierarchyNode>(HierarchyNodesPtr, i);
                    if (nodeRef.ContainsLeafNodes == 1)
                    {
                        for (int j = nodeRef.ChildrenStartIndex;
                             j < nodeRef.ChildrenStartIndex + nodeRef.ChildrenLength;
                             j++)
                        {
                            nodeRef.AABB.Include(sortedLeafNodesPtr[j].AABB);
                        }
                    }
                    else
                    {
                        nodeRef.AABB.Include(HierarchyNodesPtr[nodeRef.ChildIndices[0]].AABB);
                        nodeRef.AABB.Include(HierarchyNodesPtr[nodeRef.ChildIndices[1]].AABB);
                        nodeRef.AABB.Include(HierarchyNodesPtr[nodeRef.ChildIndices[2]].AABB);
                        nodeRef.AABB.Include(HierarchyNodesPtr[nodeRef.ChildIndices[3]].AABB);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void RadixSortLeavesRangeOnAxis(
            BVHSortingLeafNode* nodes, 
            BVHSortingLeafNode* tmpSortingNodes, 
            int nodesLength, 
            int* histogram,
            int* bucketNodeStartIndexes,
            int axis)
        {
            for (int pass = 0; pass < BVHUtils.RadixSortPassesUShort; pass++)
            {
                bool isEvenPass = pass % 2 == 0;
                BVHSortingLeafNode* inputNodes = isEvenPass ? nodes : tmpSortingNodes;
                BVHSortingLeafNode* outputNodes = isEvenPass ? tmpSortingNodes : nodes; // Final pass will be non-even, which means output ends up in "nodes"
                
                int bitShiftForPass = pass * BVHUtils.BitsPerByte;
                
                // Clear histogram
                for (int i = 0; i < BVHUtils.ValuesPerByte; i++)
                {
                    histogram[i] = 0;
                }

                // Compute histogram
                for (int i = 0; i < nodesLength; i++)
                {
                    ushort sortedValue = inputNodes[i].Pos[axis];
                    int bucketIndex = (int)((sortedValue >> bitShiftForPass) & (BVHUtils.ValuesPerByte - 1));
                    histogram[bucketIndex]++;
                }

                // Compute the nodes start index for each bucket
                int indexCounter = 0;
                for (int bucketIndex = 0; bucketIndex < BVHUtils.ValuesPerByte; bucketIndex++)
                {
                    bucketNodeStartIndexes[bucketIndex] = indexCounter;
                    indexCounter += histogram[bucketIndex];
                }
                
                // Perform the radix sort pass
                for (int i = 0; i < nodesLength; i++)
                {
                    BVHSortingLeafNode node = inputNodes[i];
                    ushort sortedValue = inputNodes[i].Pos[axis];
                    int bucketIndex = (int)((sortedValue >> bitShiftForPass) & (BVHUtils.ValuesPerByte - 1));
                    int writeIndex = bucketNodeStartIndexes[bucketIndex]++;
                    outputNodes[writeIndex] = node;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddNodeToHierarchy(ref WorkingHierarchyNode hierarchyNode, bool containsLeafNodes, out int addedIndex)
        {
            addedIndex = HierarchyNodes.Length;
            if (hierarchyNode.ParentIndex >= 0)
            {
                ref BVHHierarchyNode parent =
                    ref UnsafeUtility.ArrayElementAsRef<BVHHierarchyNode>(HierarchyNodesPtr, hierarchyNode.ParentIndex);
                parent.ChildIndices[hierarchyNode.ChildIndex] = addedIndex;
            }
             
            hierarchyNode.Node.ContainsLeafNodes = containsLeafNodes ? (byte)1 : (byte)0;

            int prevCapacity = HierarchyNodes.Capacity;
            HierarchyNodes.Add(hierarchyNode.Node);
            if (HierarchyNodes.Capacity != prevCapacity)
            {
                // TODO: handle this better?
                HierarchyNodesPtr = HierarchyNodes.GetUnsafePtr();
            }
        }
    }
}