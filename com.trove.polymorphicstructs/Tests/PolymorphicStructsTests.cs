
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;


namespace Trove.PolymorphicStructs.Tests
{
    public partial struct PolyMyTestPoly
    {
    }

    public partial struct PolyMyTestPolyMerged
    {
    }

    public interface IMyTestCommonPoly
    {
        public void DoSomething1A();
        public int DoSomething1B(int a);
        public int DoSomething1C(ref int a);
        public int DoSomething1D(ref int a, out int b);

        public NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> DoSomething1E(ref int a, out int b,
            in NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> c);

        public ref NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> DoSomething1F(ref int a, out int b,
            ref NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> c);
    }

    [PolymorphicStructInterface]
    public interface IMyTestPoly : IMyTestCommonPoly
    {
    }

    [PolymorphicStructInterface]
    [IsMergedFieldsPolymorphicStruct]
    public interface IMyTestPolyMerged : IMyTestCommonPoly
    {
    }

    [PolymorphicStruct]
    public struct MyTestPolyA : IMyTestPoly, IMyTestPolyMerged
    {
        public float3 A;

        public void DoSomething1A()
        {
        }

        public int DoSomething1B(int a)
        {
            return a + 1;
        }

        public int DoSomething1C(ref int a)
        {
            return a;
        }

        public int DoSomething1D(ref int a, out int b)
        {
            b = default;
            return a;
        }

        public NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> DoSomething1E(ref int a, out int b,
            in NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> c)
        {
            b = default;
            return default;
        }

        public ref NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> DoSomething1F(ref int a, out int b,
            ref NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> c)
        {
            b = default;
            return ref c;
        }
    }

    namespace N1
    {

        [PolymorphicStruct]
        public struct MyTestPolyA : IMyTestPoly, IMyTestPolyMerged
        {
            public int A;
            public float3 B;
            public int C;
            public float3 D;
            public int E;
            public float3 F;

            public void DoSomething1A()
            {
            }

            public int DoSomething1B(int a)
            {
                return a;
            }

            public int DoSomething1C(ref int a)
            {
                return a;
            }

            public int DoSomething1D(ref int a, out int b)
            {
                b = default;
                return a;
            }

            public NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> DoSomething1E(ref int a, out int b,
                in NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> c)
            {
                b = default;
                return default;
            }

            public ref NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> DoSomething1F(ref int a, out int b,
                ref NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> c)
            {
                b = default;
                return ref c;
            }
        }

        namespace N2
        {
            [PolymorphicStruct]
            public struct MyTestPolyC : IMyTestPoly, IMyTestPolyMerged
            {
                public float3 A;
                public quaternion B;

                public void DoSomething1A()
                {
                }

                public int DoSomething1B(int a)
                {
                    return a;
                }

                public int DoSomething1C(ref int a)
                {
                    return a;
                }

                public int DoSomething1D(ref int a, out int b)
                {
                    b = default;
                    return a;
                }

                public NativeHashMap<int, TestGenericType<float3, float4>> DoSomething1E(ref int a, out int b,
                    in NativeHashMap<int, TestGenericType<float3, float4>> c)
                {
                    b = default;
                    return default;
                }

                public ref NativeHashMap<int, TestGenericType<float3, float4>> DoSomething1F(ref int a, out int b,
                    ref NativeHashMap<int, TestGenericType<float3, float4>> c)
                {
                    b = default;
                    return ref c;
                }
            }

            public struct TestGenericType<T, R> where T : unmanaged where R : unmanaged
            {
                public T a;
                public R b;
            }
        }
    }


    [TestFixture]
    public class PolymorphicStructsTests
    {
        [SetUp]
        public void SetUp()
        {
        }

        [TearDown]
        public void TearDown()
        {
        }

        [Test]
        public void GlobalEventTest1()
        {
            PolyMyTestPoly testPoly = new PolyMyTestPoly();
            PolyMyTestPolyMerged testPolyMerged = new PolyMyTestPolyMerged();
        }
    }
}