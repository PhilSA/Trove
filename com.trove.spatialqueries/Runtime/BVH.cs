using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Trove.SpatialQueries
{
    public struct BVHLeafNode
    {
        public AABB AABB;
        public int NodeDataIndex;
    }

    public struct BVHHierarchyNode
    {
        public AABB AABB;
        public int LeftIndex;
        public int RightIndex;
        public byte ContainsLeafNodes;

        public int ChildrenStartIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => LeftIndex;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => LeftIndex = value;
        }

        public int ChildrenLength
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => RightIndex;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => RightIndex = value;
        }
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

    public struct BVH<TNodeData> where TNodeData : unmanaged
    {
        // Nodes A and B are used to ping pong between buffers during sorting.
        // After sorting, one of them becomes the "SortedNodes" and the other becomes the "ReorderedNodes"
        internal NativeList<BVHLeafNode> LeafNodes;
        internal NativeList<BVHHierarchyNode> HierarchyNodes;
        internal NativeList<TNodeData> LeafNodeDatas;
        internal NativeReference<AABB> SceneAABB;

        public static BVH<TNodeData> Create(Allocator allocator, int initialElementsCapacity)
        {
            BVH<TNodeData> bvh = new BVH<TNodeData>();
            bvh.LeafNodes = new NativeList<BVHLeafNode>(
                BVHUtils.ComputeTotalNodesCountForEntries(initialElementsCapacity),
                allocator);
            bvh.HierarchyNodes = new NativeList<BVHHierarchyNode>(initialElementsCapacity, allocator);
            bvh.LeafNodeDatas = new NativeList<TNodeData>(initialElementsCapacity, allocator);
            bvh.SceneAABB = new NativeReference<AABB>(allocator);

            return bvh;
        }

        public void Dispose(JobHandle jobHandle)
        {
            if (LeafNodes.IsCreated)
            {
                LeafNodes.Dispose(jobHandle);
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

        public void GetNodes(out NativeList<BVHHierarchyNode> hierarchyNodes, out NativeList<BVHLeafNode> leafNodes)
        {
            hierarchyNodes = HierarchyNodes;
            leafNodes = LeafNodes;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddNode(in TNodeData nodeData, in AABB aabb)
        {
            ref AABB sceneAABBRef = ref *SceneAABB.GetUnsafePtr();
            sceneAABBRef.Include(aabb);

            LeafNodes.Add(new BVHLeafNode
            {
                AABB = aabb,
                NodeDataIndex = LeafNodeDatas.Length,
            });
            LeafNodeDatas.Add(nodeData);
        }

        public void ReserveAddNodesUnsafe(int addNodesCount, out int startIndexOfReservedRange)
        {
            startIndexOfReservedRange = LeafNodes.Length;
            LeafNodes.Resize(LeafNodes.Length + addNodesCount, NativeArrayOptions.UninitializedMemory);
            LeafNodeDatas.Resize(LeafNodeDatas.Length + addNodesCount, NativeArrayOptions.UninitializedMemory);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void AddNodeUnsafe(in TNodeData nodeData, in AABB aabb, int atIndex)
        {
            LeafNodes[atIndex] = new BVHLeafNode
            {
                AABB = aabb,
                NodeDataIndex = atIndex,
            };
            LeafNodeDatas[atIndex] = nodeData;
        }

        public unsafe bool QueryAABB<TCollector>(in AABB aabb, ref TCollector collector)
            where TCollector : unmanaged, IBVHQueryCollector<TNodeData>
        {
            collector.OnBeginQuery();

            if (HierarchyNodes.Length < 1)
            {
                return false;
            }

            UnsafeList<BVHLeafNode> leafNodes = *LeafNodes.GetUnsafeList();
            UnsafeList<BVHHierarchyNode> hierarchyNodes = *HierarchyNodes.GetUnsafeList();
            UnsafeList<TNodeData> leafNodeDatas = *LeafNodeDatas.GetUnsafeList();

            QueryAABBRecursive(0, aabb, leafNodes, hierarchyNodes, leafNodeDatas, ref collector);

            return collector.HasFoundResults();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void QueryAABBRecursive<TCollector>(int nodeIndex, in AABB aabb, UnsafeList<BVHLeafNode> leafNodes,
            UnsafeList<BVHHierarchyNode> hierarchyNodes, UnsafeList<TNodeData> leafNodeDatas, ref TCollector collector)
            where TCollector : unmanaged, IBVHQueryCollector<TNodeData>
        {
            BVHHierarchyNode hierarchyNode = hierarchyNodes[nodeIndex];

            // Early out if no overlap
            if (!aabb.OverlapsAABB(hierarchyNode.AABB))
                return;

            if (hierarchyNode.ContainsLeafNodes == 1)
            {
                // Query leaf nodes
                for (int i = hierarchyNode.ChildrenStartIndex;
                     i < hierarchyNode.ChildrenStartIndex + hierarchyNode.ChildrenLength;
                     i++)
                {
                    BVHLeafNode leafNode = leafNodes[i];
                    if (aabb.OverlapsAABB(leafNode.AABB))
                    {
                        collector.AddNode(leafNodeDatas[leafNode.NodeDataIndex]);
                    }
                }
            }
            else
            {
                // Internal node - recurse to children
                QueryAABBRecursive(hierarchyNode.LeftIndex, aabb, leafNodes, hierarchyNodes, leafNodeDatas, ref collector);
                QueryAABBRecursive(hierarchyNode.RightIndex, aabb, leafNodes, hierarchyNodes, leafNodeDatas, ref collector);
            }
        }

        public unsafe bool QuerySphere<TCollector>(in float3 position, float radius, ref TCollector collector)
            where TCollector : unmanaged, IBVHQueryCollector<TNodeData>
        {
            collector.OnBeginQuery();

            if (HierarchyNodes.Length < 1)
            {
                return false;
            }

            UnsafeList<BVHLeafNode> leafNodes = *LeafNodes.GetUnsafeList();
            UnsafeList<BVHHierarchyNode> hierarchyNodes = *HierarchyNodes.GetUnsafeList();
            UnsafeList<TNodeData> leafNodeDatas = *LeafNodeDatas.GetUnsafeList();

            QuerySphereRecursive(0, position, radius * radius, leafNodes, hierarchyNodes, leafNodeDatas, ref collector);

            return collector.HasFoundResults();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void QuerySphereRecursive<TCollector>(int nodeIndex, in float3 position, float radiusSq, UnsafeList<BVHLeafNode> leafNodes,
            UnsafeList<BVHHierarchyNode> hierarchyNodes, UnsafeList<TNodeData> leafNodeDatas, ref TCollector collector)
            where TCollector : unmanaged, IBVHQueryCollector<TNodeData>
        {
            BVHHierarchyNode hierarchyNode = hierarchyNodes[nodeIndex];

            // Early out if no overlap
            if (!hierarchyNode.AABB.OverlapsSphere(position, radiusSq))
                return;

            if (hierarchyNode.ContainsLeafNodes == 1)
            {
                // Query leaf nodes
                for (int i = hierarchyNode.ChildrenStartIndex;
                     i < hierarchyNode.ChildrenStartIndex + hierarchyNode.ChildrenLength;
                     i++)
                {
                    BVHLeafNode leafNode = leafNodes[i];
                    if (leafNode.AABB.OverlapsSphere(position, radiusSq))
                    {
                        collector.AddNode(leafNodeDatas[leafNode.NodeDataIndex]);
                    }
                }
            }
            else
            {
                // Internal node - recurse to children
                QuerySphereRecursive(hierarchyNode.LeftIndex, position, radiusSq, leafNodes, hierarchyNodes, leafNodeDatas, ref collector);
                QuerySphereRecursive(hierarchyNode.RightIndex, position, radiusSq, leafNodes, hierarchyNodes, leafNodeDatas, ref collector);
            }
        }

        public unsafe bool QueryRay<TCollector>(float3 rayOrigin, float3 rayDirectionNormalized, float rayLength,
            ref TCollector collector)
            where TCollector : unmanaged, IBVHQueryCollector<TNodeData>
        {
            collector.OnBeginQuery();

            if (HierarchyNodes.Length < 1)
            {
                return false;
            }

            UnsafeList<BVHLeafNode> leafNodes = *LeafNodes.GetUnsafeList();
            UnsafeList<BVHHierarchyNode> hierarchyNodes = *HierarchyNodes.GetUnsafeList();
            UnsafeList<TNodeData> leafNodeDatas = *LeafNodeDatas.GetUnsafeList();

            QueryRayRecursive(0, rayOrigin, rayDirectionNormalized, rayLength, leafNodes, hierarchyNodes,
                leafNodeDatas, ref collector);

            return collector.HasFoundResults();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void QueryRayRecursive<TCollector>(int nodeIndex, in float3 rayOrigin, in float3 rayDirectionNormalized,
            float rayLength, UnsafeList<BVHLeafNode> leafNodes, UnsafeList<BVHHierarchyNode> hierarchyNodes,
            UnsafeList<TNodeData> leafNodeDatas, ref TCollector collector)
            where TCollector : unmanaged, IBVHQueryCollector<TNodeData>
        {
            BVHHierarchyNode hierarchyNode = hierarchyNodes[nodeIndex];

            // Early out if no intersection
            if (!hierarchyNode.AABB.IntersectsRay(rayOrigin, rayDirectionNormalized, rayLength))
                return;

            if (hierarchyNode.ContainsLeafNodes == 1)
            {
                // Query leaf nodes
                for (int i = hierarchyNode.ChildrenStartIndex;
                     i < hierarchyNode.ChildrenStartIndex + hierarchyNode.ChildrenLength;
                     i++)
                {
                    BVHLeafNode leafNode = leafNodes[i];
                    if (leafNode.AABB.IntersectsRay(rayOrigin, rayDirectionNormalized, rayLength))
                    {
                        collector.AddNode(leafNodeDatas[leafNode.NodeDataIndex]);
                    }
                }
            }
            else
            {
                // Internal node - recurse to children
                QueryRayRecursive(hierarchyNode.LeftIndex, rayOrigin, rayDirectionNormalized, rayLength, leafNodes, hierarchyNodes,
                    leafNodeDatas, ref collector);
                QueryRayRecursive(hierarchyNode.RightIndex, rayOrigin, rayDirectionNormalized, rayLength, leafNodes, hierarchyNodes,
                    leafNodeDatas, ref collector);
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
                LeafNodes = LeafNodes,
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

            dep = new BVHBuildSAHSplitHierarchyJob
            {
                LeafNodes = LeafNodes,
                HierarchyNodes = HierarchyNodes,
                SceneAABB = SceneAABB,
            }.Schedule(dep);

            return dep;
        }

        [BurstCompile]
        public struct BVHClearJob : IJob
        {
            public BVH<TNodeData> BVH;

            public void Execute()
            {
                BVH.LeafNodes.Clear();
                BVH.HierarchyNodes.Clear();
                BVH.LeafNodeDatas.Clear();
                BVH.SceneAABB.Value = AABB.GetEmpty();
            }
        }
    }

    internal static class BVHUtils
    {
        internal const int RadixBits = 8;
        internal const int RadixSortBucketCount = 1 << RadixBits; // 256 values of a byte
        internal const int RadixSortPasses = 4; // 4 bytes of the morton uint

        internal static int ComputeTotalNodesCountForEntries(int entriesCount)
        {
            // Make entries count even
            if (entriesCount % 2 != 0)
            {
                entriesCount++;
            }

            float entriesCountFloat = (float)entriesCount;

            while (entriesCountFloat > 1f)
            {
                entriesCountFloat *= 0.5f;
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
    public unsafe struct BVHBuildSAHSplitHierarchyJob : IJob
    {
        public struct AABBAndCount
        {
            public AABB AABB;
            public int Count;
        }

        public struct SplitInfo
        {
            public int Axis;
            public float Position;
            public float Cost;
        }

        public NativeReference<AABB> SceneAABB;
        public NativeList<BVHLeafNode> LeafNodes;
        public NativeList<BVHHierarchyNode> HierarchyNodes;

        public UnsafeList<BVHLeafNode> LeafNodesUnsafe;

        private const int LeavesPerNode = 4; 
        private const int MaxDepth = 30;
        private const int NbBins = 16;
        private const float TraversalCost = 1f;
        private const float IntersectCost = 1.5f;

        public void Execute()
        {
            HierarchyNodes.Clear();
            LeafNodesUnsafe = *LeafNodes.GetUnsafeList();

            BVHHierarchyNode root = new BVHHierarchyNode
            {
                AABB = SceneAABB.Value,
                ChildrenStartIndex = 0,
                ChildrenLength = LeafNodesUnsafe.Length,
            };

            int depth = 0;
            BuildRecursive(root, depth, -1, false);
        }

        private void BuildRecursive(BVHHierarchyNode node, int depth, int parentIndex, bool isLeftChild)
        { 
            // Add to hierarchy if few enough children, or if exceed depth limit
            if (node.ChildrenLength < LeavesPerNode || depth >= MaxDepth)
            {
                AddNodeToHierarchy(ref node, parentIndex, isLeftChild, true, out _);
                return;
            }

            FindSplit(in node, out SplitInfo split);

            // If no split, add to hierarchy
            if (split.Axis == -1)
            {
                AddNodeToHierarchy(ref node, parentIndex, isLeftChild, true, out _);
                return;
            }

            // If the split would be less efficient than no split, add to hierarchy
            // float leafCost = node.ChildrenLength * IntersectCost;
            // if (split.Cost >= leafCost)
            // {
            //     Debug.Log("C");
            //     AddNodeToHierarchy(ref node, parentIndex, isLeftChild, true, out _);
            //     return;
            // }

            BVHHierarchyNode leftNode = new BVHHierarchyNode
            {
                AABB = AABB.GetEmpty(),
                ChildrenStartIndex = node.ChildrenStartIndex,
                ChildrenLength = 0,
            };
            BVHHierarchyNode rightNode = new BVHHierarchyNode
            {
                AABB = AABB.GetEmpty(),
                ChildrenStartIndex = -1, // we don't know yet
                ChildrenLength = 0,
            };

            // Reorder children in the buffer range so that it contains all left children, then all right children
            {
                for (int leftNodeIndex = node.ChildrenStartIndex;
                     leftNodeIndex < node.ChildrenStartIndex + node.ChildrenLength;
                     leftNodeIndex++)
                {
                    BVHLeafNode childFromLeft = LeafNodesUnsafe[leftNodeIndex];
                    float centerOnAxisChlidFromLeft = childFromLeft.AABB.GetCenter()[split.Axis];

                    if (centerOnAxisChlidFromLeft < split.Position)
                    {
                        leftNode.AABB.Include(childFromLeft.AABB);
                        leftNode.ChildrenLength++;
                    }
                    else
                    {
                        // If node goes on the right, iterate nodes from the right until we find one that goes left.
                        // Then swap them
                        for (int rightNodeIndex = node.ChildrenStartIndex + node.ChildrenLength - 1 - rightNode.ChildrenLength;
                             rightNodeIndex >= leftNodeIndex; rightNodeIndex--)
                        {
                            BVHLeafNode childFromRight = LeafNodesUnsafe[rightNodeIndex];
                            float centerOnAxisChlidFromRight = childFromRight.AABB.GetCenter()[split.Axis];

                            if (centerOnAxisChlidFromRight >= split.Position)
                            {
                                rightNode.AABB.Include(childFromRight.AABB);
                                rightNode.ChildrenLength++;
                            }
                            else
                            {
                                // Swap
                                BVHLeafNode tmpNode = childFromRight;
                                LeafNodesUnsafe[rightNodeIndex] = childFromLeft;
                                LeafNodesUnsafe[leftNodeIndex] = tmpNode;

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

            // Add node to hierarchy
            AddNodeToHierarchy(ref node, parentIndex, isLeftChild, false, out int addedIndex);

            depth++;
            BuildRecursive(leftNode, depth, addedIndex, true);
            BuildRecursive(rightNode, depth, addedIndex, false);
        }

        // Find the best split on the best axis to separate the children
        private void FindSplit(in BVHHierarchyNode parent, out SplitInfo bestSplit)
        {
            bestSplit = new SplitInfo
            {
                Cost = float.PositiveInfinity,
                Axis = -1, // invalid
            };

            Span<AABBAndCount> bins = stackalloc AABBAndCount[NbBins];
            Span<AABBAndCount> leftBinSums = stackalloc AABBAndCount[NbBins - 1];
            Span<AABBAndCount> rightBinSums = stackalloc AABBAndCount[NbBins - 1];

            // For x, y, z axis
            for (int axis = 0; axis < 3; axis++)
            {
                // Clear bins
                for (int i = 0; i < bins.Length; i++)
                {
                    bins[i] = default;
                }

                // Init bins
                float parentMinOnAxis = parent.AABB.Min[axis];
                float parentMaxOnAxis = parent.AABB.Max[axis];
                float binValueRange = (parentMaxOnAxis - parentMinOnAxis) / bins.Length;

                if (binValueRange <= 0f)
                    continue;

                // Compute children counts and AABBs for bins
                for (int nodeIndex = parent.ChildrenStartIndex; nodeIndex < parent.ChildrenStartIndex + parent.ChildrenLength; nodeIndex++)
                {
                    AABB childAABB = LeafNodesUnsafe[nodeIndex].AABB;
                    float centerOnAxis = childAABB.GetCenter()[axis];
                    int binIndex = (int)math.floor((centerOnAxis - parentMinOnAxis) / binValueRange);
                    binIndex = math.clamp(binIndex, 0, bins.Length - 1);
                    bins[binIndex].Count++;
                    bins[binIndex].AABB.Include(childAABB);
                }

                // Compute info about bins to the left of the end of each bin
                AABBAndCount cummulativeAABBAndCount = new AABBAndCount
                {
                    Count = 0,
                    AABB = AABB.GetEmpty(),
                };
                for (int i = 0; i < bins.Length - 2; i++)
                {
                    cummulativeAABBAndCount.Count += bins[i].Count;
                    cummulativeAABBAndCount.AABB.Include(bins[i].AABB);
                    leftBinSums[i] = cummulativeAABBAndCount;
                }

                // Compute info about bins to the right of the end of each bin
                cummulativeAABBAndCount = new AABBAndCount
                {
                    Count = 0,
                    AABB = AABB.GetEmpty(),
                };
                for (int i = bins.Length - 2; i >= 0; i--)
                {
                    cummulativeAABBAndCount.Count += bins[i + 1].Count;
                    cummulativeAABBAndCount.AABB.Include(bins[i + 1].AABB);
                    rightBinSums[i] = cummulativeAABBAndCount;
                }

                // Find the best bin to split at
                float parentSurfaceArea = parent.AABB.CalculateSurfaceArea();
                for (int i = 0; i < bins.Length - 2; i++)
                {
                    AABBAndCount leftBinSum = leftBinSums[i];
                    AABBAndCount rightBinSum = rightBinSums[i];

                    if (leftBinSum.Count == 0 || rightBinSum.Count == 0)
                        continue;

                    // Calculate the cost of separating at that bin. Basically, the best split is the split that
                    // generates the best ratio of surface area to node count of the left and right children AABBs.
                    // In other words, a high cost would be if we have very few nodes in a very large AABB.
                    float leftSurfaceArea = leftBinSum.AABB.CalculateSurfaceArea();
                    float rightSurfaceArea = rightBinSum.AABB.CalculateSurfaceArea();
                    float leftProbability = leftSurfaceArea / parentSurfaceArea;
                    float rightProbability = rightSurfaceArea / parentSurfaceArea;
                    float splitCost = TraversalCost +
                           (leftProbability * leftBinSum.Count * IntersectCost) +
                           (rightProbability * rightBinSum.Count * IntersectCost);

                    // Remember split if best so far
                    if (splitCost < bestSplit.Cost)
                    {
                        bestSplit = new SplitInfo
                        {
                            Axis = axis,
                            Cost = splitCost,
                            Position = parentMinOnAxis + ((i + 1) * binValueRange), // We split at bin's end
                        };
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddNodeToHierarchy(ref BVHHierarchyNode node, int parentIndex, bool isLeft, bool containsLeafNodes, out int addedIndex)
        {
            addedIndex = HierarchyNodes.Length;
            if (parentIndex >= 0)
            {
                ref BVHHierarchyNode parent =
                    ref UnsafeUtility.ArrayElementAsRef<BVHHierarchyNode>(HierarchyNodes.GetUnsafePtr(), parentIndex);
                if (isLeft)
                {
                    parent.LeftIndex = addedIndex;
                }
                else
                {
                    parent.RightIndex = addedIndex;
                }
            }

            node.ContainsLeafNodes = containsLeafNodes ? (byte)1 : (byte)0;
            HierarchyNodes.Add(node);
        }
    }
}