

using NUnit.Framework;
using Unity.Entities;

namespace Trove.ObjectHandles.Tests
{
    [TestFixture]
    public class ObjectHandlesTests
    {
        public World World => World.DefaultGameObjectInjectionWorld;

        [SetUp]
        public void SetUp()
        { }

        [TearDown]
        public void TearDown()
        {
            ObjectHandlesTestUtilities.DestroyTestEntities(World);
        }

        [Test]
        public void Test1()
        {
        }
    }
}