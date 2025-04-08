
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
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
        public int Test1(int a, in int b, ref int c, out int d);
        public ref int Test2(int a, in int b, ref int c, out int d);
        public int Test3(Entity e1, int e2Index, out Entity r1, out Entity r2);
    }

    [PolymorphicStructInterface]
    [IsMergedFieldsPolymorphicStruct]
    public interface IMyTestPolyMerged : IMyTestCommonPoly
    {
        public int Test1(int a, in int b, ref int c, out int d);
        public ref int Test2(int a, in int b, ref int c, out int d);
        public int Test3(Entity e1, int e2Index, out Entity r1, out Entity r2);
    }

    [PolymorphicStructInterface]
    public interface IMyTestPolyProps
    {
        public int TestProp1 { get; set; }
        public int TestProp2 { get; }
        public int TestProp3 { set; }
    }

    [PolymorphicStruct]
    public struct MyTestPolyA : IMyTestPoly, IMyTestPolyMerged
    {
        public int A;

        public int Test1(int a, in int b, ref int c, out int d)
        {
            A += a;
            
            c += 1;
            d = a + b + c;
            return d;
        }
        
        public ref int Test2(int a, in int b, ref int c, out int d)
        {
            c += 1;
            d = a + b + c;
            return ref c;
        }

        public int Test3(Entity e1, int e2Index, out Entity r1, out Entity r2)
        {
            r1 = Entity.Null;
            r2 = Entity.Null;
            return 0;
        }
        
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

    [PolymorphicStruct]
    public struct MyTestPolyPropsA : IMyTestPolyProps
    {
        public int A;

        public int TestProp1 { get; set; }

        public int TestProp2
        {
            get => A;
        }

        public int TestProp3
        {
            set => A = value;
        }
    }

    [PolymorphicStruct]
    public struct MyTestPolyPropsB : IMyTestPolyProps
    {
        public int A;

        public int TestProp1 { get; set; }

        public int TestProp2
        {
            get => A;
        }

        public int TestProp3
        {
            set => A = value;
        }
    }

    namespace N1
    {

        [PolymorphicStruct]
        public struct MyTestPolyA : IMyTestPoly, IMyTestPolyMerged
        {
            public float3 B;
            public int A;
            public float3 D;
            public int C;
            public float3 F;
            public int E;

            public int Test1(int a, in int b, ref int c, out int d)
            {
                E += a;
                
                c += 1;
                d = a + b + c;
                return d;
            }
        
            public ref int Test2(int a, in int b, ref int c, out int d)
            {
                c += 1;
                d = a + b + c;
                return ref c;
            }

            public int Test3(Entity e1, int e2Index, out Entity r1, out Entity r2)
            {
                r1 = Entity.Null;
                r2 = Entity.Null;
                return 0;
            }

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
                public ComponentLookup<Disabled> DisabledLookup;
                public NativeList<Entity> Entities;

                public int Test1(int a, in int b, ref int c, out int d)
                {
                    Entities.Resize(Entities.Length + a, NativeArrayOptions.ClearMemory);
                    
                    c += 1;
                    d = a + b + c;
                    return d;
                }
        
                public ref int Test2(int a, in int b, ref int c, out int d)
                {
                    c += 1;
                    d = a + b + c;
                    return ref c;
                }

                public int Test3(Entity e1, int e2Index, out Entity r1, out Entity r2)
                {
                    int counter = 0;
                    r1 = Entity.Null;
                    r2 = Entity.Null;
                    if (DisabledLookup.HasComponent(e1))
                    {
                        r1 = e1;
                        counter++;
                    }

                    if (e2Index < Entities.Length)
                    {
                        r2 = Entities[e2Index];
                        counter++;
                    }
                    return counter;;
                }

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
        public void PolymorphicStructsTest1()
        {
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            SimulationSystemGroup simulationSystemGroup = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<SimulationSystemGroup>();
            ComponentLookup<Disabled> disabledLookup = simulationSystemGroup.GetComponentLookup<Disabled>();
            NativeList<byte> bytesList = new NativeList<byte>(Allocator.Temp);
            NativeList<Entity> entitiesList = new NativeList<Entity>(Allocator.Temp);
            entitiesList.Add(new Entity { Index = 123 });
            Entity testLookupEntity = entityManager.CreateEntity(typeof(Disabled));
            int refInt8 = 8;
            int refInt4 = 4;
            int outRes = 0;
            int returnRes = 0;

            ////////////////////////////////////////////////////////////////////
            ////////////////////////////////////////////////////////////////////
            ////////////////////////////////////////////////////////////////////
            
            MyTestPolyA testPolyA = new MyTestPolyA
            {
                A = 10,
            };
            N1.MyTestPolyA testPolyB = new N1.MyTestPolyA
            {
                A = 1,
                B = 2,
                C = 3,
                D = 4,
                E = 5, 
                F = 6,
            };
            N1.N2.MyTestPolyC testPolyC = new N1.N2.MyTestPolyC
            {
                Entities = entitiesList,
                DisabledLookup = disabledLookup,
            };

            // -----------------------------------------
            
            // Union Struct
            {
                PolyMyTestPoly testPoly;
                testPoly = testPolyA;
                returnRes = testPoly.Test1(1, in refInt4, ref refInt8, out outRes);
                testPolyA = testPoly;
                Assert.AreEqual(14, returnRes);
                Assert.AreEqual(14, outRes);
                Assert.AreEqual(11, testPolyA.A);
                Assert.AreEqual(9, refInt8);
                refInt8 = 8;
                ref int refReturnRes = ref testPoly.Test2(6, in refInt8, ref refInt4, out outRes);
                Assert.AreEqual(5, refReturnRes);
                Assert.AreEqual(19, outRes);
                Assert.AreEqual(5, refInt4);
                refInt4 = 4;
                testPoly = testPolyC;
                int counter = testPoly.Test3(testLookupEntity, 0, out Entity lookedUpEntity, out Entity listEntity);
                Assert.AreEqual(2, counter);
                Assert.AreEqual(testLookupEntity.Index, lookedUpEntity.Index);
                Assert.AreEqual(123, listEntity.Index);
            }
            
            // -----------------------------------------
            
            // Reset
            testPolyA = new MyTestPolyA
            {
                A = 10,
            };
            testPolyB = new N1.MyTestPolyA
            {
                A = 1,
                B = 2,
                C = 3,
                D = 4,
                E = 5, 
                F = 6,
            };
            testPolyC = new N1.N2.MyTestPolyC
            {
                Entities = entitiesList,
                DisabledLookup = disabledLookup,
            };
            
            // -----------------------------------------
            
            // MergedFields Struct 
            {
                PolyMyTestPolyMerged testPolyMerged;
                testPolyMerged = testPolyA;
                returnRes = testPolyMerged.Test1(1, in refInt4, ref refInt8, out outRes);
                testPolyA = testPolyMerged;
                Assert.AreEqual(14, returnRes);
                Assert.AreEqual(14, outRes);
                Assert.AreEqual(11, testPolyA.A);
                Assert.AreEqual(9, refInt8);
                refInt8 = 8;
                ref int refReturnRes = ref testPolyMerged.Test2(6, in refInt8, ref refInt4, out outRes);
                Assert.AreEqual(5, refReturnRes);
                Assert.AreEqual(19, outRes);
                Assert.AreEqual(5, refInt4);
                refInt4 = 4;
                testPolyMerged = testPolyC;
                int counter = testPolyMerged.Test3(testLookupEntity, 0, out Entity lookedUpEntity, out Entity listEntity);
                Assert.AreEqual(2, counter);
                Assert.AreEqual(testLookupEntity.Index, lookedUpEntity.Index);
                Assert.AreEqual(123, listEntity.Index);
            }
            
            // -----------------------------------------
            
            // Reset
            testPolyA = new MyTestPolyA
            {
                A = 10,
            };
            testPolyB = new N1.MyTestPolyA
            {
                A = 1,
                B = 2,
                C = 3,
                D = 4,
                E = 5, 
                F = 6,
            };
            testPolyC = new N1.N2.MyTestPolyC
            {
                Entities = entitiesList,
                DisabledLookup = disabledLookup,
            };
            
            // -----------------------------------------
            
            // ByteArray Struct 
            {
                PolyMyTestPoly testPolyByteArray;
                testPolyByteArray = testPolyA;
                PolymorphicObjectUtilities.AddObject(testPolyByteArray, ref bytesList, out int writeByteIndex,
                    out int writeSize);
                PolymorphicObjectUtilities.ReadObject(ref bytesList, writeByteIndex, out testPolyByteArray,
                    out int readSize);
                returnRes = testPolyByteArray.Test1(1, in refInt4, ref refInt8, out outRes);
                PolymorphicObjectUtilities.WriteObject(testPolyByteArray, ref bytesList, writeByteIndex, out writeSize);
                PolymorphicObjectUtilities.ReadObject(ref bytesList, writeByteIndex, out testPolyByteArray,
                    out readSize);
                testPolyA = testPolyByteArray;
                Assert.AreEqual(14, returnRes);
                Assert.AreEqual(14, outRes);
                Assert.AreEqual(11, testPolyA.A);
                Assert.AreEqual(9, refInt8);
                refInt8 = 8;
                ref int refReturnRes = ref testPolyByteArray.Test2(6, in refInt8, ref refInt4, out outRes);
                PolymorphicObjectUtilities.WriteObject(testPolyByteArray, ref bytesList, writeByteIndex, out writeSize);
                PolymorphicObjectUtilities.ReadObject(ref bytesList, writeByteIndex, out testPolyByteArray,
                    out readSize);
                Assert.AreEqual(5, refReturnRes);
                Assert.AreEqual(19, outRes);
                Assert.AreEqual(5, refInt4);
                refInt4 = 4;
                
                // A, B, A
                testPolyByteArray = testPolyB;
                Assert.AreEqual(new float3(6f), testPolyB.F);
                PolymorphicObjectUtilities.AddObject(testPolyByteArray, ref bytesList, out _,
                    out _);
                testPolyByteArray = testPolyA;
                PolymorphicObjectUtilities.AddObject(testPolyByteArray, ref bytesList, out _,
                    out _);
                int iterationCounter = 0;
                PolymorphicObjectNativeListIterator<PolyMyTestPoly> iterator = PolymorphicObjectUtilities.GetIterator<PolyMyTestPoly>(bytesList);
                while (iterator.GetNext(out PolyMyTestPoly e, out _, out _))
                {
                    switch (iterationCounter)
                    {
                        case 0:
                        {
                            MyTestPolyA a = e;
                            Assert.AreEqual(11, a.A);
                            break;
                        }
                        case 1:
                        {
                            N1.MyTestPolyA a = e;
                            Assert.AreEqual(new float3(6f), a.F);
                            break;
                        }
                        case 2:
                        {
                            MyTestPolyA a = e;
                            Assert.AreEqual(11, a.A);
                            break;
                        }
                    }
                    iterationCounter++;
                }
                Assert.AreEqual(3, iterationCounter);
                
                PolymorphicObjectUtilities.RemoveObject(ref bytesList, 0, out PolyMyTestPoly removedObject, out _);
                Assert.AreEqual(11, ((MyTestPolyA)removedObject).A);
                iterator = PolymorphicObjectUtilities.GetIterator<PolyMyTestPoly>(bytesList);
                iterationCounter = 0;
                while (iterator.GetNext(out PolyMyTestPoly e, out _, out _))
                {
                    switch (iterationCounter)
                    {
                        case 0:
                        {
                            N1.MyTestPolyA a = e;
                            Assert.AreEqual(new float3(6f), a.F);
                            break;
                        }
                        case 1:
                        {
                            MyTestPolyA a = e;
                            Assert.AreEqual(11, a.A);
                            break;
                        }
                    }
                    iterationCounter++;
                }
                Assert.AreEqual(2, iterationCounter);
            }

            // -----------------------------------------

            MyTestPolyPropsA testPolyPropsA = new MyTestPolyPropsA
            {
                A = 10,
            };
            MyTestPolyPropsB testPolyPropsB = new MyTestPolyPropsB
            {
                A = 20,
            };
            
            // -----------------------------------------
            
            PolyMyTestPolyProps testPolyProps = testPolyPropsA;
            Assert.AreEqual(0, testPolyProps.TestProp1);
            testPolyProps.TestProp1 = 1;
            Assert.AreEqual(1, testPolyProps.TestProp1);
            testPolyPropsA = testPolyProps;
            Assert.AreEqual(1, testPolyPropsA.TestProp1);
            testPolyPropsA.TestProp1 = 2;
            Assert.AreEqual(2, testPolyPropsA.TestProp1);
            testPolyProps = testPolyPropsA;
            Assert.AreEqual(2, testPolyProps.TestProp1);
            
            ////////////////////////////////////////////////////////////////////
            ////////////////////////////////////////////////////////////////////
            ////////////////////////////////////////////////////////////////////
            
            entityManager.DestroyEntity(testLookupEntity);
            bytesList.Dispose();
            entitiesList.Dispose();
        }
    }
}