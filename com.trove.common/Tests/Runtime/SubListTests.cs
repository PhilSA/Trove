using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace Trove.Tests
{
    public interface ISublistTestElement
    {
        public int Value { get; set; }
    }
    
    public struct TestSubListElement : IBufferElementData, ISubListElement, ISublistTestElement
    {
        public int Value { get; set; }
        public SubList.InternalElementData SubListData { get; set; }
    }
    
    // public struct TestPooledSubListElement : IBufferElementData, IPooledSubListElement, ISublistTestElement
    // {
    //     public int Value { get; set; }
    //     public PooledSubList.InternalElementData PooledSubListData { get; set; }
    // }
    //
    // public struct TestCompactSubListElement : IBufferElementData, ICompactSubListElement, ISublistTestElement
    // {
    //     public int Value { get; set; }
    //     public CompactSubList.InternalElementData CompactSubListData { get; set; }
    // }
    
    [TestFixture]
    public class SubListTests
    {
        private static void LogBuffer<T>(ref DynamicBuffer<T> buffer)
            where T : unmanaged, IBufferElementData, ISublistTestElement
        {
            string str = "";
            for (int i = 0; i < buffer.Length; i++)
            {
                str += $"{buffer[i].Value} ";
            }
            Debug.Log(str);
        }
        
        [Test]
        public void SubListTest()
        {
            bool success = false;
            Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(0);
            World world = World.DefaultGameObjectInjectionWorld;
            EntityManager entityManager = world.EntityManager;
            Entity testEntity = TestUtilities.CreateTestEntity(entityManager);
            DynamicBuffer<TestSubListElement> buffer = entityManager.AddBuffer<TestSubListElement>(testEntity);

            TestSubListElement elemResult;
            
            int initialCapacity = 5;
            SubList subList1 = SubList.Create(ref buffer, initialCapacity, 2f);
            SubList subList2 = SubList.Create(ref buffer, initialCapacity, 2f);

            // Add in capacity
            for (int i = 0; i < 5; i++)
            {
                SubList.Add(ref subList1, ref buffer, new TestSubListElement { Value = i });
                Assert.AreEqual(i + 1, subList1.Length);
                
                SubList.Add(ref subList2, ref buffer, new TestSubListElement { Value = i });
                Assert.AreEqual(i + 1, subList2.Length);
            }
            
            Assert.AreEqual(0, subList1.ElementsStartIndex);
            Assert.AreEqual(5, subList2.ElementsStartIndex);
            Assert.AreEqual(5, subList1.Capacity);
            Assert.AreEqual(5, subList2.Capacity);
            
            // Add 6th element that breaks capacity
            {
                SubList.Add(ref subList1, ref buffer, new TestSubListElement { Value = 5 });
                Assert.AreEqual(6, subList1.Length);

                SubList.Add(ref subList2, ref buffer, new TestSubListElement { Value = 5 });
                Assert.AreEqual(6, subList2.Length);
            }
            
            Assert.AreEqual(10, subList1.ElementsStartIndex);
            Assert.AreEqual(0, subList2.ElementsStartIndex);
            Assert.AreEqual(10, subList1.Capacity);
            Assert.AreEqual(10, subList2.Capacity);

            // Read
            for (int i = 0; i < 6; i++)
            {
                success = SubList.TryGet(ref subList1, ref buffer,i, out elemResult);
                Assert.IsTrue(success);
                Assert.AreEqual(i, elemResult.Value);
                
                success = SubList.TryGet(ref subList2, ref buffer, i, out elemResult);
                Assert.IsTrue(success);
                Assert.AreEqual(i, elemResult.Value);
            }

            // Values:
            // 0 1 2 3 4 5 0 0 0 0 0 1 2 3 4 5 0 0 0 0
            // -------sl2---------|--------sl1--------
            
            // Remove
            {
                // Remove first elem of sl2 and of buffer
                success = SubList.TryRemoveAt(ref subList2, ref buffer, 0);
                Assert.IsTrue(success);
                Assert.AreEqual(5, subList2.Length);
                
                success = SubList.TryGet(ref subList2, ref buffer, 0, out elemResult);
                Assert.IsTrue(success);
                Assert.AreEqual(1, elemResult.Value);
                success = SubList.TryGet(ref subList1, ref buffer, 1, out elemResult);
                Assert.IsTrue(success);
                Assert.AreEqual(1, elemResult.Value);
                
                // Remove outside of length
                success = SubList.TryRemoveAt(ref subList2, ref buffer, 6);
                Assert.IsFalse(success);
                
                // Remove last elem of sl1
                success = SubList.TryRemoveAt(ref subList1, ref buffer, 5);
                Assert.IsTrue(success);
                Assert.AreEqual(5, subList1.Length);
                
                success = SubList.TryGet(ref subList1, ref buffer, subList1.Length - 1, out elemResult);
                Assert.IsTrue(success);
                Assert.AreEqual(4, elemResult.Value);
                
                // Remove swapback
                success = SubList.TryRemoveAtSwapBack(ref subList1, ref buffer, 0);
                Assert.IsTrue(success);
                Assert.AreEqual(4, subList1.Length);
                
                success = SubList.TryGet(ref subList1, ref buffer, 0, out elemResult);
                Assert.IsTrue(success);
                Assert.AreEqual(4, elemResult.Value);
                
                // Many removes sl1
                for (int i = 0; i < 10; i++)
                {
                    success = SubList.TryRemoveAt(ref subList1, ref buffer, 0);
                    Assert.AreEqual(i < 4 ? true : false, success);
                }
                Assert.AreEqual(0, subList1.Length);
                
                // Many removeatswapbacks sl2
                for (int i = 0; i < 10; i++)
                {
                    success = SubList.TryRemoveAtSwapBack(ref subList2, ref buffer, 0);
                    Assert.AreEqual(i < 5 ? true : false, success);
                }
                Assert.AreEqual(0, subList2.Length);
            }
            
            // Add sublist 3
            SubList subList3 = SubList.Create(ref buffer, initialCapacity, 2f);
            
            // Clear all
            SubList.Clear(ref subList1, ref buffer);
            SubList.Clear(ref subList2, ref buffer);
            SubList.Clear(ref subList3, ref buffer);
            SubList.Resize(ref subList1, ref buffer, subList1.Capacity);
            SubList.Resize(ref subList2, ref buffer, subList2.Capacity);
            SubList.Resize(ref subList3, ref buffer, subList3.Capacity);
            Assert.AreEqual(subList1.Capacity, subList1.Length);
            Assert.AreEqual(subList2.Capacity, subList2.Length);
            Assert.AreEqual(subList3.Capacity, subList3.Length);
            
            // Fill with respective Values
            for (int i = 0; i < subList1.Length; i++)
            {
                SubList.TrySet(ref subList1, ref buffer, i, new TestSubListElement { Value = 1 });
            }
            for (int i = 0; i < subList2.Length; i++)
            {
                SubList.TrySet(ref subList2, ref buffer, i, new TestSubListElement { Value = 2 });
            }
            for (int i = 0; i < subList3.Length; i++)
            {
                SubList.TrySet(ref subList3, ref buffer, i, new TestSubListElement { Value = 3 });
            }
            
            // 2 2 2 2 2 2 2 2 2 2 1 1 1 1 1 1 1 1 1 1 3 3 3 3 3 
            for (int i = 0; i < buffer.Length; i++)
            {
                int expectedVal = 0;
                if (i < subList2.Length)
                {
                    expectedVal = 2;
                }
                else if (i < subList2.Length + subList1.Length)
                {
                    expectedVal = 1;
                }
                else if (i < subList2.Length + subList1.Length + subList3.Length)
                {
                    expectedVal = 3;
                }
                
                Assert.AreEqual(expectedVal, buffer[i].Value);
            }
            
            // Expand sl1 past capacity
            SubList.Resize(ref subList1, ref buffer, 12);
            for (int i = 0; i < subList1.Length; i++)
            {
                SubList.TrySet(ref subList1, ref buffer, i, new TestSubListElement { Value = 1 });
            }

            // 2 2 2 2 2 2 2 2 2 2 0 0 0 0 0 0 0 0 0 0 3 3 3 3 3 1 1 1 1 1 1 1 1 1 1 1 1
            
            for (int i = subList3.ElementsStartIndex + subList3.Length; i < buffer.Length; i++)
            {
                Assert.AreEqual(1, buffer[i].Value);
            }

            // Add elem and expand sl3 past capacity
            SubList.Add(ref subList3, ref buffer, new TestSubListElement { Value = 3 });
            SubList.Resize(ref subList3, ref buffer, subList3.Capacity);
            for (int i = 0; i < subList3.Length; i++)
            {
                SubList.TrySet(ref subList3, ref buffer, i, new TestSubListElement { Value = 3 });
            }
            
            // 2 2 2 2 2 2 2 2 2 2 3 3 3 3 3 3 3 3 3 3 0 0 0 0 0 1 1 1 1 1 1 1 1 1 1 1 1 
            
            for (int i = subList2.ElementsStartIndex + subList2.Length; i < subList2.ElementsStartIndex + subList2.Length + subList3.Length; i++)
            {
                Assert.AreEqual(3, buffer[i].Value);
            }
            Assert.AreEqual(10, subList3.ElementsStartIndex);
            Assert.AreEqual(10, subList3.Capacity);
            
            // Check that resize clears values but not IsOccupied
            SubList.Clear(ref subList3, ref buffer);
            for (int i = subList3.ElementsStartIndex; i < subList3.ElementsStartIndex + subList3.Length; i++)
            {
                Assert.AreEqual(3, buffer[i].Value);
                Assert.AreEqual(1, buffer[i].SubListData.IsAllocated);
            }
            SubList.Resize(ref subList3, ref buffer, 5);
            for (int i = subList3.ElementsStartIndex; i < subList3.ElementsStartIndex + subList3.Length; i++)
            {
                Assert.AreEqual(0, buffer[i].Value);
                Assert.AreEqual(1, buffer[i].SubListData.IsAllocated);
            }
            
            // 2 2 2 2 2 2 2 2 2 2 0 0 0 0 0 3 3 3 3 3 0 0 0 0 0 1 1 1 1 1 1 1 1 1 1 1 1 
            
            // Check shrink capacity marks indexes unoccupied
            int prev3Capacity = subList3.Capacity;
            SubList.SetCapacity(ref subList3, ref buffer, 5);
            for (int i = subList3.ElementsStartIndex; i < subList3.ElementsStartIndex + prev3Capacity; i++)
            {
                if (i < subList3.ElementsStartIndex + subList3.Capacity)
                {
                    Assert.AreEqual(1, buffer[i].SubListData.IsAllocated);
                }
                else
                {
                    Assert.AreEqual(0, buffer[i].SubListData.IsAllocated);
                }
            }
            
            success = SubList.SetCapacity(ref subList3, ref buffer, 3);
            Assert.IsFalse(success);
            Assert.AreEqual(5, subList3.Capacity);
            
            // Dispose
            SubList.Dispose(ref subList1, ref buffer);
            for (int i = subList1.ElementsStartIndex; i < subList1.ElementsStartIndex + subList1.Length; i++)
            {
                Assert.AreEqual(0, buffer[i].SubListData.IsAllocated);
            }
            SubList.Dispose(ref subList2, ref buffer);
            for (int i = subList2.ElementsStartIndex; i < subList2.ElementsStartIndex + subList2.Length; i++)
            {
                Assert.AreEqual(0, buffer[i].SubListData.IsAllocated);
            }
            SubList.Dispose(ref subList3, ref buffer);
            for (int i = subList3.ElementsStartIndex; i < subList3.ElementsStartIndex + subList3.Length; i++)
            {
                Assert.AreEqual(0, buffer[i].SubListData.IsAllocated);
            }
            
            // Recreate
            subList1 = SubList.Create(ref buffer, initialCapacity, 2f);
            subList2 = SubList.Create(ref buffer, initialCapacity, 2f);
            subList3 = SubList.Create(ref buffer, initialCapacity, 2f);
            
            // Jumble
            Assert.DoesNotThrow(() =>
            {
                SubList.Clear(ref subList1, ref buffer);
                SubList.Clear(ref subList2, ref buffer);
                SubList.Clear(ref subList3, ref buffer);

                for (int i = 0; i < 2000; i++)
                {
                    int jumbleOperation = random.NextInt(0, 4);
                    int jumbleList = random.NextInt(0, 2);
                    switch (jumbleOperation)
                    {
                        case 0:
                            switch (jumbleList)
                            {
                                case 0:
                                    SubList.Add(ref subList1, ref buffer,
                                        new TestSubListElement { Value = random.NextInt(0, 9) });
                                    break;
                                case 1:
                                    SubList.Add(ref subList2, ref buffer,
                                        new TestSubListElement { Value = random.NextInt(0, 9) });
                                    break;
                                case 2:
                                    SubList.Add(ref subList3, ref buffer,
                                        new TestSubListElement { Value = random.NextInt(0, 9) });
                                    break;
                            }
                            break;
                        case 1:
                            switch (jumbleList)
                            {
                                case 0:
                                    success = SubList.TryRemoveAt(ref subList1, ref buffer, random.NextInt(-1, subList1.Length + 1));
                                    break;
                                case 1:
                                    success = SubList.TryRemoveAt(ref subList2, ref buffer, random.NextInt(-1, subList2.Length + 1));
                                    break;
                                case 2:
                                    success = SubList.TryRemoveAt(ref subList3, ref buffer, random.NextInt(-1, subList3.Length + 1));
                                    break;
                            }
                            break;
                        case 2:
                            switch (jumbleList)
                            {
                                case 0:
                                    success = SubList.TryRemoveAtSwapBack(ref subList1, ref buffer, random.NextInt(-1, subList1.Length + 1));
                                    break;
                                case 1:
                                    success = SubList.TryRemoveAtSwapBack(ref subList2, ref buffer, random.NextInt(-1, subList2.Length + 1));
                                    break;
                                case 2:
                                    success = SubList.TryRemoveAtSwapBack(ref subList3, ref buffer, random.NextInt(-1, subList3.Length + 1));
                                    break;
                            }
                            break;
                        case 3:
                            switch (jumbleList)
                            {
                                case 0:
                                    SubList.Resize(ref subList1, ref buffer, random.NextInt(subList1.Length - 2, subList1.Length + 2));
                                    break;
                                case 1:
                                    SubList.Resize(ref subList2, ref buffer, random.NextInt(subList2.Length - 2, subList2.Length + 2));
                                    break;
                                case 2:
                                    SubList.Resize(ref subList3, ref buffer, random.NextInt(subList3.Length - 2, subList3.Length + 2));
                                    break;
                            }
                            break;
                        case 4:
                            switch (jumbleList)
                            {
                                case 0:
                                    SubList.SetCapacity(ref subList1, ref buffer, random.NextInt(subList1.Capacity - 2, subList1.Capacity + 2));
                                    break;
                                case 1:
                                    SubList.SetCapacity(ref subList2, ref buffer, random.NextInt(subList2.Capacity - 2, subList2.Capacity + 2));
                                    break;
                                case 2:
                                    SubList.SetCapacity(ref subList3, ref buffer, random.NextInt(subList3.Capacity - 2, subList3.Capacity + 2));
                                    break;
                            }
                            break;
                        case 5:
                            int chances = random.NextInt(0, 5);
                            if (chances == 1)
                            {
                                switch (jumbleList)
                                {
                                    case 0:
                                        SubList.Clear(ref subList1, ref buffer);
                                        break;
                                    case 1:
                                        SubList.Clear(ref subList2, ref buffer);
                                        break;
                                    case 2:
                                        SubList.Clear(ref subList3, ref buffer);
                                        break;
                                }
                            }

                            break;
                    }
                }
            });
            
            TestUtilities.DestroyTestEntities(world);
        }
        
        // [Test]
        // public void PooledSubListTest()
        // {
        //     bool success = false;
        //     Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(0);
        //     World world = World.DefaultGameObjectInjectionWorld;
        //     EntityManager entityManager = world.EntityManager;
        //     Entity testEntity = TestUtilities.CreateTestEntity(entityManager);
        //     DynamicBuffer<TestPooledSubListElement> buffer = entityManager.AddBuffer<TestPooledSubListElement>(testEntity);
        //     
        //     PooledSubList subList1 = new PooledSubList();
        //     PooledSubList subList2 = new PooledSubList();
        //     UnsafeList<PooledSubList.ElementHandle> handles1 = new UnsafeList<PooledSubList.ElementHandle>(16, Allocator.Temp);
        //     UnsafeList<PooledSubList.ElementHandle> handles2 = new UnsafeList<PooledSubList.ElementHandle>(16, Allocator.Temp);
        //     
        //     // Add
        //     for (int i = 0; i < 6; i++)
        //     {
        //         PooledSubList.Add(ref subList1, ref buffer, new TestPooledSubListElement { Value = i }, out PooledSubList.ElementHandle handle1);
        //         handles1.Add(handle1);
        //         Assert.AreEqual(i + 1, subList1.Count);
        //         Assert.AreEqual((i*2), handle1.Index);
        //         Assert.AreEqual(1, handle1.Version);
        //         
        //         PooledSubList.Add(ref subList2, ref buffer, new TestPooledSubListElement { Value = i }, out PooledSubList.ElementHandle handle2);
        //         handles2.Add(handle2);
        //         Assert.AreEqual(i + 1, subList2.Count);
        //         Assert.AreEqual((i*2)+1, handle2.Index);
        //         Assert.AreEqual(1, handle2.Version);
        //     }
        //     
        //     // Read
        //     for (int i = 0; i < 6; i++)
        //     {
        //         success = PooledSubList.TryGet(ref buffer, handles1[i], out TestPooledSubListElement elem1);
        //         Assert.IsTrue(success);
        //         Assert.AreEqual(i, elem1.Value);
        //         
        //         success = PooledSubList.TryGet(ref buffer, handles2[i], out TestPooledSubListElement elem2);
        //         Assert.IsTrue(success);
        //         Assert.AreEqual(i, elem2.Value);
        //     }
        //     
        //     TestUtilities.DestroyTestEntities(world);
        //     handles1.Dispose();
        //     handles2.Dispose();
        // }
        //
        // [Test]
        // public void CompactSubListTest()
        // {
        //     bool success = false;
        //     Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(0);
        //     World world = World.DefaultGameObjectInjectionWorld;
        //     EntityManager entityManager = world.EntityManager;
        //     Entity testEntity = TestUtilities.CreateTestEntity(entityManager);
        //     DynamicBuffer<TestCompactSubListElement> buffer = entityManager.AddBuffer<TestCompactSubListElement>(testEntity);
        //     
        //     CompactSubList subList1 = CompactSubList.Create();
        //     CompactSubList subList2 = CompactSubList.Create();
        //     
        //     // Add
        //     for (int i = 0; i < 6; i++)
        //     {
        //         CompactSubList.Add(ref subList1, ref buffer, new TestCompactSubListElement { Value = i });
        //         Assert.AreEqual(i + 1, subList1.Length);
        //         
        //         CompactSubList.Add(ref subList2, ref buffer, new TestCompactSubListElement { Value = i });
        //         Assert.AreEqual(i + 1, subList2.Length);
        //     }
        //     
        //     // Read
        //     for (int i = 0; i < 6; i++)
        //     {
        //         success = CompactSubList.TryGet(ref subList1, ref buffer, i, out TestCompactSubListElement elem1, out int elemIndex1);
        //         Assert.IsTrue(success);
        //         Assert.AreEqual((i*2), elemIndex1);
        //         Assert.AreEqual(i, elem1.Value);
        //         
        //         success = CompactSubList.TryGet(ref subList2, ref buffer, i, out TestCompactSubListElement elem2, out int elemIndex2);
        //         Assert.IsTrue(success);
        //         Assert.AreEqual((i*2)+1, elemIndex2);
        //         Assert.AreEqual(i, elem2.Value);
        //     }
        //     
        //     TestUtilities.DestroyTestEntities(world);
        // }
    }
}