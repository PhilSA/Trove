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
    
    public struct TestPoolElement : IBufferElementData, IPoolObject, IPoolTestElement
    {
        public int Value { get; set; }
        public int Version { get; set; }
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
        public void SubListTest()
        {
            bool success = false;
            World world = World.DefaultGameObjectInjectionWorld;
            EntityManager entityManager = world.EntityManager;
            Entity testEntity = TestUtilities.CreateTestEntity(entityManager);
            DynamicBuffer<TestPoolElement> buffer = entityManager.AddBuffer<TestPoolElement>(testEntity);

            
            TestUtilities.DestroyTestEntities(world);
        }
    }
}