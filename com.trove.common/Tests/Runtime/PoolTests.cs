using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

namespace Trove.Tests
{
    public interface IPoolTestElement
    {
        public int Value { get; set; }
    }
    
    public struct TestPoolElement : IBufferElementData, IPoolElement, IPoolTestElement
    {
        public int Value { get; set; }
        public int Version { get; set; }
    }
    
    public struct TestPoolIndexRange : IBufferElementData
    {
        public IndexRange Value;
    }
    
    [TestFixture]
    public class PoolTests
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
        public void PoolTest()
        {
            bool success = false;
            World world = World.DefaultGameObjectInjectionWorld;
            EntityManager entityManager = world.EntityManager;
            Entity testEntity = TestUtilities.CreateTestEntity(entityManager);
            DynamicBuffer<TestPoolElement> elemBuffer = entityManager.AddBuffer<TestPoolElement>(testEntity);

            Pool.Init(ref elemBuffer, 5);
            Assert.AreEqual(5, elemBuffer.Length);
            
            Pool.AddElement(ref elemBuffer, new TestPoolElement { Value = 0 }, out PoolElementHandle e0Handle);
            Pool.AddElement(ref elemBuffer, new TestPoolElement { Value = 1 }, out PoolElementHandle e1Handle);
            Pool.AddElement(ref elemBuffer, new TestPoolElement { Value = 2 }, out PoolElementHandle e2Handle);
            Pool.AddElement(ref elemBuffer, new TestPoolElement { Value = 3 }, out PoolElementHandle e3Handle);
            Pool.AddElement(ref elemBuffer, new TestPoolElement { Value = 4 }, out PoolElementHandle e4Handle);
            Assert.AreEqual(5, elemBuffer.Length);

            success = Pool.Exists(ref elemBuffer, e3Handle);
            Assert.IsTrue(success);

            success = FreeRangesPool.TryGetObject(ref elemBuffer, e3Handle, out var e3);
            Assert.IsTrue(success);
            Assert.AreEqual(3, e3.Value);

            success = Pool.TryRemoveObject(ref elemBuffer, e3Handle);
            Assert.IsTrue(success);
            success = Pool.Exists(ref elemBuffer, e3Handle);
            Assert.IsFalse(success);
            success = Pool.TryRemoveObject(ref elemBuffer, e3Handle);
            Assert.IsFalse(success);
            
            success = Pool.TryRemoveObject(ref elemBuffer, e1Handle);
            Assert.IsTrue(success);
            
            success = Pool.TryRemoveObject(ref elemBuffer, e2Handle);
            Assert.IsTrue(success);
            
            Pool.Trim(ref elemBuffer);
            Assert.AreEqual(5, elemBuffer.Length);
            
            Pool.AddElement(ref elemBuffer, new TestPoolElement { Value = 5 }, out PoolElementHandle e5Handle);
            success = Pool.TryRemoveObject(ref elemBuffer, e4Handle);
            Assert.IsTrue(success);
            
            Pool.Trim(ref elemBuffer);
            Assert.AreEqual(2, elemBuffer.Length);
            
            TestUtilities.DestroyTestEntities(world);
        }
        
        [Test]
        public void FreeRangesPoolTest()
        {
            bool success = false;
            World world = World.DefaultGameObjectInjectionWorld;
            EntityManager entityManager = world.EntityManager;
            Entity testEntity = TestUtilities.CreateTestEntity(entityManager);
            DynamicBuffer<TestPoolElement> elemBuffer = entityManager.AddBuffer<TestPoolElement>(testEntity);
            DynamicBuffer<IndexRange> indexBuffer = entityManager.AddBuffer<TestPoolIndexRange>(testEntity).Reinterpret<IndexRange>();
            elemBuffer = entityManager.GetBuffer<TestPoolElement>(testEntity);
            
            FreeRangesPool.Init(ref elemBuffer, ref indexBuffer, 5);
            Assert.AreEqual(5, elemBuffer.Length);
            Assert.AreEqual(1, indexBuffer.Length);
            
            Assert.AreEqual(0, indexBuffer[0].Start);
            Assert.AreEqual(5, indexBuffer[0].Length);
            FreeRangesPool.AddElement(ref elemBuffer, ref indexBuffer, new TestPoolElement { Value = 0 }, out PoolElementHandle e0Handle);
            Assert.AreEqual(1, indexBuffer[0].Start);
            Assert.AreEqual(4, indexBuffer[0].Length);
            FreeRangesPool.AddElement(ref elemBuffer, ref indexBuffer, new TestPoolElement { Value = 1 }, out PoolElementHandle e1Handle);
            Assert.AreEqual(2, indexBuffer[0].Start);
            Assert.AreEqual(3, indexBuffer[0].Length);
            FreeRangesPool.AddElement(ref elemBuffer, ref indexBuffer, new TestPoolElement { Value = 2 }, out PoolElementHandle e2Handle);
            Assert.AreEqual(3, indexBuffer[0].Start);
            Assert.AreEqual(2, indexBuffer[0].Length);
            FreeRangesPool.AddElement(ref elemBuffer, ref indexBuffer, new TestPoolElement { Value = 3 }, out PoolElementHandle e3Handle);
            Assert.AreEqual(4, indexBuffer[0].Start);
            Assert.AreEqual(1, indexBuffer[0].Length);
            FreeRangesPool.AddElement(ref elemBuffer, ref indexBuffer, new TestPoolElement { Value = 4 }, out PoolElementHandle e4Handle);
            Assert.AreEqual(5, elemBuffer.Length);
            Assert.AreEqual(0, indexBuffer.Length);

            success = FreeRangesPool.Exists(ref elemBuffer, e3Handle);
            Assert.IsTrue(success);

            success = FreeRangesPool.TryGetObject(ref elemBuffer, e3Handle, out var e3);
            Assert.IsTrue(success);
            Assert.AreEqual(3, e3.Value);
            
            success = FreeRangesPool.TryRemoveObject(ref elemBuffer, ref indexBuffer, e3Handle);
            Assert.IsTrue(success);
            Assert.AreEqual(3, indexBuffer[0].Start);
            Assert.AreEqual(1, indexBuffer[0].Length);
            success = FreeRangesPool.Exists(ref elemBuffer, e3Handle);
            Assert.IsFalse(success);
            success = FreeRangesPool.TryRemoveObject(ref elemBuffer, ref indexBuffer, e3Handle);
            Assert.IsFalse(success);
            
            success = FreeRangesPool.TryRemoveObject(ref elemBuffer, ref indexBuffer, e1Handle);
            Assert.IsTrue(success);
            Assert.AreEqual(1, indexBuffer[0].Start);
            Assert.AreEqual(1, indexBuffer[0].Length);
            Assert.AreEqual(3, indexBuffer[1].Start);
            Assert.AreEqual(1, indexBuffer[1].Length);
            
            success = FreeRangesPool.TryRemoveObject(ref elemBuffer, ref indexBuffer, e2Handle);
            Assert.IsTrue(success);
            Assert.AreEqual(1, indexBuffer[0].Start);
            Assert.AreEqual(3, indexBuffer[0].Length);
            Assert.AreEqual(1, indexBuffer.Length);
            
            FreeRangesPool.Trim(ref elemBuffer, ref indexBuffer);
            Assert.AreEqual(5, elemBuffer.Length);
            
            FreeRangesPool.AddElement(ref elemBuffer, ref indexBuffer, new TestPoolElement { Value = 5 }, out PoolElementHandle e5Handle);
            Assert.AreEqual(2, indexBuffer[0].Start);
            Assert.AreEqual(2, indexBuffer[0].Length);
            Assert.AreEqual(1, indexBuffer.Length);
            success = FreeRangesPool.TryRemoveObject(ref elemBuffer, ref indexBuffer, e4Handle);
            Assert.IsTrue(success);
            Assert.AreEqual(2, indexBuffer[0].Start);
            Assert.AreEqual(3, indexBuffer[0].Length);
            Assert.AreEqual(1, indexBuffer.Length);
            
            FreeRangesPool.Trim(ref elemBuffer, ref indexBuffer);
            Assert.AreEqual(2, elemBuffer.Length);
            Assert.AreEqual(0, indexBuffer.Length);
            
            TestUtilities.DestroyTestEntities(world);
        }
    }
}