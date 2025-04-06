using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using NUnit.Framework;
using Unity.Collections;
using Trove;
using Unity.Collections.LowLevel.Unsafe;

namespace Trove.PolymorphicStructs.Tests
{
    public partial struct PolyMyTestPoly
    { }
    public partial struct PolyMyTestPolyMerged
    { } 
    
    [PolymorphicStructInterface]
    public interface IMyTestPoly
    { 
        public void DoSomething1A();
        public int DoSomething1B(int a);
        public int DoSomething1C(ref int a);
        public int DoSomething1D(ref int a, out int b);
        public NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> DoSomething1E(ref int a, out int b, in NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> c);
        public ref NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> DoSomething1F(ref int a, out int b, ref NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> c);
    }
    
    [PolymorphicStructInterface]
    [IsMergedFieldsPolymorphicStruct]
    public interface IMyTestPolyMerged
    {
        
    }
    
    [PolymorphicStruct]
    public struct MyTestPolyA : IMyTestPoly, IMyTestPolyMerged
    {
        public int A;

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
    
        public NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> DoSomething1E(ref int a, out int b, in NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> c)
        {
            b = default;
            return default;
        }
    
        public ref NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> DoSomething1F(ref int a, out int b, ref NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> c)
        {
            b = default;
            return ref c;
        }
    } 
    
    namespace N1
    {
    
        [PolymorphicStruct]
        public struct MyTestPolyB : IMyTestPoly, IMyTestPolyMerged
        {
            public int A; 
        
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
    
            public NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> DoSomething1E(ref int a, out int b, in NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> c)
            {
                b = default;
                return default;
            }
    
            public ref NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> DoSomething1F(ref int a, out int b, ref NativeHashMap<int, N1.N2.TestGenericType<float3, float4>> c)
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
                public int A; 
        
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
    
                public NativeHashMap<int, TestGenericType<float3, float4>> DoSomething1E(ref int a, out int b, in NativeHashMap<int, TestGenericType<float3, float4>> c)
                {
                    b = default;
                    return default;
                }
    
                public ref NativeHashMap<int, TestGenericType<float3, float4>> DoSomething1F(ref int a, out int b, ref NativeHashMap<int, TestGenericType<float3, float4>> c)
                {
                    b = default;
                    return ref c;
                }
            }
    
            public struct TestGenericType<T,R> where T : unmanaged where R : unmanaged
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
        }
    }
}