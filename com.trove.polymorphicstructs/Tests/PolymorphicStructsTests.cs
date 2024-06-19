using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Trove.PolymorphicStructs;
using NUnit.Framework;
using Unity.Entities;

public interface TestPolyInterface
{
    public void DoSomething(int a);
    public void DoSomethingA(int a);
}


//namespace Trove.PolymorphicStructs.Tests
//{
//    [PolymorphicStruct]
//    public partial struct StructC : ITestPolymorphicStruct
//    {
//        public float Multiplier;
//        public float3 VectorA;
//        public float3 VectorB;

//        public int TestProperty
//        {
//            get
//            {
//                return (int)Multiplier;
//            }
//            set
//            {
//                Multiplier = (float)value;
//            }
//        }

//        public float GetResult()
//        {
//            return Multiplier;
//        }

//        public float GetResultFromInData(in TestPolymorphicStructData data)
//        {
//            return data.A * Multiplier;
//        }

//        public void ChangeByRef(ref TestPolymorphicStructData data)
//        {
//            data.A = Multiplier;
//        }

//        public float ReturnUniqueOperationResult(in TestPolymorphicStructData data)
//        {
//            return 4f * 5f;
//        }

//        public bool ReturnOutParam(in TestPolymorphicStructData data, out float output)
//        {
//            output = Multiplier;
//            return true;
//        }

//        public void ModifySelf()
//        {
//            Multiplier += 1;
//        }

//        public TestGeneric<float> AddToGeneric(TestGeneric<float> val)
//        {
//            val.A += Multiplier;
//            return val;
//        }
//    }
//}

//namespace Trove.PolymorphicStructs.Tests
//{
//    [PolymorphicStructInterface]
//    public interface ITestEmptyPolymorphicStruct
//    {
//    }

//    [PolymorphicStructInterface]
//    public interface ITestEmptyPolymorphicStructWithMethod
//    {
//        public float GetResult();
//    }

//    [PolymorphicStructInterface]
//    public interface ITestPolymorphicStruct
//    {
//        public int TestProperty { get; set; }
//        public float GetResult();
//        public float GetResultFromInData(in TestPolymorphicStructData data);
//        public void ChangeByRef(ref TestPolymorphicStructData data);
//        public float ReturnUniqueOperationResult(in TestPolymorphicStructData data);
//        public bool ReturnOutParam(in TestPolymorphicStructData data, out float output);
//        public void ModifySelf();
//        public TestGeneric<float> AddToGeneric(TestGeneric<float> val);
//    }

//    public struct TestPolymorphicStructData
//    {
//        public float A;
//    }

//    public struct TestGeneric<T> where T : struct
//    {
//        public float A;
//    }

//    public struct TestBlobA
//    {
//        public float A;
//    }
//    public struct TestBlobB
//    {
//        public float A;
//    }


//    [PolymorphicStruct]
//    public partial struct StructA : ITestPolymorphicStruct
//    {
//        public float Multiplier;
//        public BlobAssetReference<TestBlobA> Blob1;
//        public BlobAssetReference<TestBlobA> Blob2;

//        public int TestProperty
//        {
//            get
//            {
//                return (int)Multiplier;
//            }
//            set
//            {
//                Multiplier = (float)value;
//            }
//        }

//        public float GetResult()
//        {
//            return Multiplier;
//        }

//        public float GetResultFromInData(in TestPolymorphicStructData data)
//        {
//            return data.A * Multiplier;
//        }

//        public void ChangeByRef(ref TestPolymorphicStructData data)
//        {
//            data.A = Multiplier;
//        }

//        public float ReturnUniqueOperationResult(in TestPolymorphicStructData data)
//        {
//            return 2f * 3f;
//        }

//        public bool ReturnOutParam(in TestPolymorphicStructData data, out float output)
//        {
//            output = Multiplier;
//            return true;
//        }

//        public void ModifySelf()
//        {
//            Multiplier += 1;
//        }

//        public TestGeneric<float> AddToGeneric(TestGeneric<float> val)
//        {
//            val.A += Multiplier;
//            return val;
//        }
//    }

//    [PolymorphicStruct]
//    public partial struct StructB : ITestPolymorphicStruct
//    {
//        public float Multiplier;
//        public float3 VectorA;
//        public BlobAssetReference<TestBlobB> Blob2;

//        public int TestProperty
//        {
//            get
//            {
//                return (int)Multiplier;
//            }
//            set
//            {
//                Multiplier = (float)value;
//            }
//        }

//        public float GetResult()
//        {
//            return Multiplier;
//        }

//        public float GetResultFromInData(in TestPolymorphicStructData data)
//        {
//            return data.A * Multiplier;
//        }

//        public void ChangeByRef(ref TestPolymorphicStructData data)
//        {
//            data.A = Multiplier;
//        }

//        public float ReturnUniqueOperationResult(in TestPolymorphicStructData data)
//        {
//            return 3f * 4f;
//        }

//        public bool ReturnOutParam(in TestPolymorphicStructData data, out float output)
//        {
//            output = Multiplier;
//            return false;
//        }

//        public void ModifySelf()
//        {
//            Multiplier += 1;
//        }

//        public TestGeneric<float> AddToGeneric(TestGeneric<float> val)
//        {
//            val.A += Multiplier;
//            return val;
//        }
//    }

//    [TestFixture]
//    public class PolymorphicStructsTests
//    {
//        [Test]
//        public void EmptyPolymorphicStructs()
//        {
//            TestEmptyPolymorphicStruct e1 = new TestEmptyPolymorphicStruct();
//            TestEmptyPolymorphicStructWithMethod e2 = new TestEmptyPolymorphicStructWithMethod();

//            Assert.AreEqual(0f, e2.GetResult()); // yeah
//        }

//        [Test]
//        public void CastingAndVariables()
//        {
//            StructA structA = new StructA
//            {
//                Multiplier = 1f,
//            };
//            StructB structB = new StructB
//            {
//                Multiplier = 2f,
//                VectorA = math.up(),
//            };
//            StructC structC = new StructC
//            {
//                Multiplier = 3f,
//                VectorA = math.right(),
//                VectorB = math.forward(),
//            };

//            TestPolymorphicStruct unionA = structA;
//            TestPolymorphicStruct unionB = structB;
//            TestPolymorphicStruct unionC = structC;

//            Assert.AreEqual(TestPolymorphicStruct.TypeId.StructA, unionA.CurrentTypeId);
//            Assert.AreEqual(TestPolymorphicStruct.TypeId.StructB, unionB.CurrentTypeId);
//            Assert.AreEqual(TestPolymorphicStruct.TypeId.StructC, unionC.CurrentTypeId);
//            Assert.AreEqual(1f, unionA.Single_0);
//            Assert.AreEqual(2f, unionB.Single_0);
//            Assert.AreEqual(math.up(), unionB.float3_1);
//            Assert.AreEqual(3f, unionC.Single_0);
//            Assert.AreEqual(math.right(), unionC.float3_1);
//            Assert.AreEqual(math.forward(), unionC.float3_2);

//            Assert.AreEqual(1f, ((StructA)unionA).Multiplier);
//            Assert.AreEqual(2f, ((StructB)unionB).Multiplier);
//            Assert.AreEqual(3f, ((StructC)unionC).Multiplier);

//            structA = unionB;
//            structB = unionC;
//            structC = unionA;

//            Assert.AreEqual(2f, structA.Multiplier);
//            Assert.AreEqual(3f, structB.Multiplier);
//            Assert.AreEqual(1f, structC.Multiplier);
//        }

//        [Test]
//        public void GetResult()
//        {
//            StructA structA = new StructA
//            {
//                Multiplier = 1f,
//            };
//            StructB structB = new StructB
//            {
//                Multiplier = 2f,
//                VectorA = math.up(),
//            };
//            StructC structC = new StructC
//            {
//                Multiplier = 3f,
//                VectorA = math.right(),
//                VectorB = math.forward(),
//            };

//            TestPolymorphicStruct unionA = structA;
//            TestPolymorphicStruct unionB = structB;
//            TestPolymorphicStruct unionC = structC;

//            Assert.AreEqual(1f, unionA.GetResult());
//            Assert.AreEqual(2f, unionB.GetResult());
//            Assert.AreEqual(3f, unionC.GetResult());
//        }

//        [Test]
//        public void GetResultFromInData()
//        {
//            TestPolymorphicStructData data = new TestPolymorphicStructData
//            {
//                A = 5f,
//            };

//            StructA structA = new StructA
//            {
//                Multiplier = 1f,
//            };
//            StructB structB = new StructB
//            {
//                Multiplier = 2f,
//                VectorA = math.up(),
//            };
//            StructC structC = new StructC
//            {
//                Multiplier = 3f,
//                VectorA = math.right(),
//                VectorB = math.forward(),
//            };

//            TestPolymorphicStruct unionA = structA;
//            TestPolymorphicStruct unionB = structB;
//            TestPolymorphicStruct unionC = structC;

//            Assert.AreEqual(5f, unionA.GetResultFromInData(in data));
//            Assert.AreEqual(10f, unionB.GetResultFromInData(in data));
//            Assert.AreEqual(15f, unionC.GetResultFromInData(in data));
//        }

//        [Test]
//        public void ChangeByRef()
//        {
//            TestPolymorphicStructData data = new TestPolymorphicStructData
//            {
//                A = 5f,
//            };

//            StructA structA = new StructA
//            {
//                Multiplier = 1f,
//            };
//            StructB structB = new StructB
//            {
//                Multiplier = 2f,
//                VectorA = math.up(),
//            };
//            StructC structC = new StructC
//            {
//                Multiplier = 3f,
//                VectorA = math.right(),
//                VectorB = math.forward(),
//            };

//            TestPolymorphicStruct unionA = structA;
//            TestPolymorphicStruct unionB = structB;
//            TestPolymorphicStruct unionC = structC;

//            unionA.ChangeByRef(ref data);
//            Assert.AreEqual(1f, data.A);

//            unionB.ChangeByRef(ref data);
//            Assert.AreEqual(2f, data.A);

//            unionC.ChangeByRef(ref data);
//            Assert.AreEqual(3f, data.A);
//        }

//        [Test]
//        public void ReturnUniqueOperationResult()
//        {
//            TestPolymorphicStructData data = new TestPolymorphicStructData
//            {
//                A = 5f,
//            };

//            StructA structA = new StructA
//            {
//                Multiplier = 1f,
//            };
//            StructB structB = new StructB
//            {
//                Multiplier = 2f,
//                VectorA = math.up(),
//            };
//            StructC structC = new StructC
//            {
//                Multiplier = 3f,
//                VectorA = math.right(),
//                VectorB = math.forward(),
//            };

//            TestPolymorphicStruct unionA = structA;
//            TestPolymorphicStruct unionB = structB;
//            TestPolymorphicStruct unionC = structC;

//            Assert.AreEqual(6f, unionA.ReturnUniqueOperationResult(in data));
//            Assert.AreEqual(12f, unionB.ReturnUniqueOperationResult(in data));
//            Assert.AreEqual(20f, unionC.ReturnUniqueOperationResult(in data));
//        }

//        [Test]
//        public void ReturnOutParam()
//        {
//            TestPolymorphicStructData data = new TestPolymorphicStructData
//            {
//                A = 5f,
//            };

//            StructA structA = new StructA
//            {
//                Multiplier = 1f,
//            };
//            StructB structB = new StructB
//            {
//                Multiplier = 2f,
//                VectorA = math.up(),
//            };
//            StructC structC = new StructC
//            {
//                Multiplier = 3f,
//                VectorA = math.right(),
//                VectorB = math.forward(),
//            };

//            TestPolymorphicStruct unionA = structA;
//            TestPolymorphicStruct unionB = structB;
//            TestPolymorphicStruct unionC = structC;

//            Assert.AreEqual(true, unionA.ReturnOutParam(in data, out float output));
//            Assert.AreEqual(1f, output);
//            Assert.AreEqual(false, unionB.ReturnOutParam(in data, out output));
//            Assert.AreEqual(2f, output);
//            Assert.AreEqual(true, unionC.ReturnOutParam(in data, out output));
//            Assert.AreEqual(3f, output);
//        }

//        [Test]
//        public void ModifySelf()
//        {
//            StructA structA = new StructA
//            {
//                Multiplier = 1f,
//            };
//            StructB structB = new StructB
//            {
//                Multiplier = 2f,
//                VectorA = math.up(),
//            };
//            StructC structC = new StructC
//            {
//                Multiplier = 3f,
//                VectorA = math.right(),
//                VectorB = math.forward(),
//            };

//            TestPolymorphicStruct unionA = structA;
//            TestPolymorphicStruct unionB = structB;
//            TestPolymorphicStruct unionC = structC;

//            unionA.ModifySelf();
//            unionB.ModifySelf();
//            unionC.ModifySelf();

//            Assert.AreEqual(2f, unionA.Single_0);
//            Assert.AreEqual(3f, unionB.Single_0);
//            Assert.AreEqual(4f, unionC.Single_0);

//            structA = unionA;
//            structB = unionB;
//            structC = unionC;

//            Assert.AreEqual(2f, structA.Multiplier);
//            Assert.AreEqual(3f, structB.Multiplier);
//            Assert.AreEqual(4f, structC.Multiplier);
//        }

//        [Test]
//        public void AddToGeneric()
//        {
//            StructA structA = new StructA
//            {
//                Multiplier = 1f,
//            };
//            StructB structB = new StructB
//            {
//                Multiplier = 2f,
//                VectorA = math.up(),
//            };
//            StructC structC = new StructC
//            {
//                Multiplier = 3f,
//                VectorA = math.right(),
//                VectorB = math.forward(),
//            };

//            TestPolymorphicStruct unionA = structA;
//            TestPolymorphicStruct unionB = structB;
//            TestPolymorphicStruct unionC = structC;

//            TestGeneric<float> val = new TestGeneric<float> { A = 1f, };
//            TestGeneric<float> valA = unionA.AddToGeneric(val);
//            TestGeneric<float> valB = unionB.AddToGeneric(val);
//            TestGeneric<float> valC = unionC.AddToGeneric(val);

//            Assert.AreEqual(2f, valA.A);
//            Assert.AreEqual(3f, valB.A);
//            Assert.AreEqual(4f, valC.A);
//        }

//        [Test]
//        public void Properties()
//        {
//            StructA structA = new StructA
//            {
//                Multiplier = 1f,
//            };
//            StructB structB = new StructB
//            {
//                Multiplier = 2f,
//                VectorA = math.up(),
//            };
//            StructC structC = new StructC
//            {
//                Multiplier = 3f,
//                VectorA = math.right(),
//                VectorB = math.forward(),
//            };

//            TestPolymorphicStruct unionA = structA;
//            TestPolymorphicStruct unionB = structB;
//            TestPolymorphicStruct unionC = structC;

//            Assert.AreEqual(1, unionA.TestProperty);
//            Assert.AreEqual(2, unionB.TestProperty);
//            Assert.AreEqual(3, unionC.TestProperty);

//            unionA.TestProperty = 4;
//            unionB.TestProperty = 5;
//            unionC.TestProperty = 6;

//            Assert.AreEqual(4, unionA.TestProperty);
//            Assert.AreEqual(5, unionB.TestProperty);
//            Assert.AreEqual(6, unionC.TestProperty);

//            structA = unionA;
//            structB = unionB;
//            structC = unionC;

//            Assert.AreEqual(4f, structA.Multiplier);
//            Assert.AreEqual(5f, structB.Multiplier);
//            Assert.AreEqual(6f, structC.Multiplier);
//        }
//    }
//}