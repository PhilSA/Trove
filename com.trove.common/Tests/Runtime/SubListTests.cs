using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Trove.Tests
{
    public struct TestSubListElement : IBufferElementData, ISubListElement
    {
        public int Value;
        public byte IsOccupied { get; set; }
    }
    
    public struct TestPooledSubListElement : IBufferElementData, IPooledSubListElement
    {
        public int Value;
        public int Version { get; set; }
        public PooledSubList.ElementHandle PrevElementHandle { get; set; }
    }
    
    public struct TestCompactSubListElement : IBufferElementData, ICompactSubListElement
    {
        public int Value;
        public int NextElementIndex { get; set; }
        public int LastElementIndex { get; set; }
        public byte IsCreated { get; set; }
        public byte IsPinnedFirstElement { get; set; }
    }
    
    [TestFixture]
    public class SubListTests
    {
        [Test]
        public void SubListTest()
        {
            bool success = false;
            World world = World.DefaultGameObjectInjectionWorld;
            EntityManager entityManager = world.EntityManager;
            Entity testEntity = TestUtilities.CreateTestEntity(entityManager);
            DynamicBuffer<TestSubListElement> buffer = entityManager.AddBuffer<TestSubListElement>(testEntity);

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
                Assert.AreEqual(5 + 1, subList1.Length);

                SubList.Add(ref subList2, ref buffer, new TestSubListElement { Value = 5 });
                Assert.AreEqual(5 + 1, subList2.Length);
            }
            
            Assert.AreEqual(10, subList1.ElementsStartIndex);
            Assert.AreEqual(0, subList2.ElementsStartIndex);
            Assert.AreEqual(10, subList1.Capacity);
            Assert.AreEqual(10, subList2.Capacity);

            // Read
            for (int i = 0; i < 6; i++)
            {
                success = SubList.TryGet(ref subList1, ref buffer,i, out TestSubListElement elem1);
                Assert.IsTrue(success);
                Assert.AreEqual(i, elem1.Value);
                
                success = SubList.TryGet(ref subList2, ref buffer, i, out TestSubListElement elem2);
                Assert.IsTrue(success);
                Assert.AreEqual(i, elem2.Value);
            }

            TestUtilities.DestroyTestEntities(world);
        }
        
        [Test]
        public void PooledSubListTest()
        {
            bool success = false;
            World world = World.DefaultGameObjectInjectionWorld;
            EntityManager entityManager = world.EntityManager;
            Entity testEntity = TestUtilities.CreateTestEntity(entityManager);
            DynamicBuffer<TestPooledSubListElement> buffer = entityManager.AddBuffer<TestPooledSubListElement>(testEntity);
            
            PooledSubList subList1 = new PooledSubList();
            PooledSubList subList2 = new PooledSubList();
            UnsafeList<PooledSubList.ElementHandle> handles1 = new UnsafeList<PooledSubList.ElementHandle>(16, Allocator.Temp);
            UnsafeList<PooledSubList.ElementHandle> handles2 = new UnsafeList<PooledSubList.ElementHandle>(16, Allocator.Temp);
            
            // Add
            for (int i = 0; i < 6; i++)
            {
                PooledSubList.Add(ref subList1, ref buffer, new TestPooledSubListElement { Value = i }, out PooledSubList.ElementHandle handle1);
                handles1.Add(handle1);
                Assert.AreEqual(i + 1, subList1.Count);
                Assert.AreEqual((i*2), handle1.Index);
                Assert.AreEqual(1, handle1.Version);
                
                PooledSubList.Add(ref subList2, ref buffer, new TestPooledSubListElement { Value = i }, out PooledSubList.ElementHandle handle2);
                handles2.Add(handle2);
                Assert.AreEqual(i + 1, subList2.Count);
                Assert.AreEqual((i*2)+1, handle2.Index);
                Assert.AreEqual(1, handle2.Version);
            }
            
            // Read
            for (int i = 0; i < 6; i++)
            {
                success = PooledSubList.TryGet(ref buffer, handles1[i], out TestPooledSubListElement elem1);
                Assert.IsTrue(success);
                Assert.AreEqual(i, elem1.Value);
                
                success = PooledSubList.TryGet(ref buffer, handles2[i], out TestPooledSubListElement elem2);
                Assert.IsTrue(success);
                Assert.AreEqual(i, elem2.Value);
            }
            
            TestUtilities.DestroyTestEntities(world);
            handles1.Dispose();
            handles2.Dispose();
        }
        
        [Test]
        public void CompactSubListTest()
        {
            bool success = false;
            World world = World.DefaultGameObjectInjectionWorld;
            EntityManager entityManager = world.EntityManager;
            Entity testEntity = TestUtilities.CreateTestEntity(entityManager);
            DynamicBuffer<TestCompactSubListElement> buffer = entityManager.AddBuffer<TestCompactSubListElement>(testEntity);
            
            CompactSubList subList1 = CompactSubList.Create();
            CompactSubList subList2 = CompactSubList.Create();
            
            // Add
            for (int i = 0; i < 6; i++)
            {
                CompactSubList.Add(ref subList1, ref buffer, new TestCompactSubListElement { Value = i });
                Assert.AreEqual(i + 1, subList1.Count);
                
                CompactSubList.Add(ref subList2, ref buffer, new TestCompactSubListElement { Value = i });
                Assert.AreEqual(i + 1, subList2.Count);
            }
            
            // Read
            for (int i = 0; i < 6; i++)
            {
                success = CompactSubList.TryGet(ref subList1, ref buffer, i, out TestCompactSubListElement elem1, out int elemIndex1);
                Assert.IsTrue(success);
                Assert.AreEqual((i*2), elemIndex1);
                Assert.AreEqual(i, elem1.Value);
                
                success = CompactSubList.TryGet(ref subList2, ref buffer, i, out TestCompactSubListElement elem2, out int elemIndex2);
                Assert.IsTrue(success);
                Assert.AreEqual((i*2)+1, elemIndex2);
                Assert.AreEqual(i, elem2.Value);
            }
            
            TestUtilities.DestroyTestEntities(world);
        }
    }
}