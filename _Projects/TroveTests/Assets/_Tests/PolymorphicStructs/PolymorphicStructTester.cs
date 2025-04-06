using Trove.PolymorphicStructs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Trove;
using Trove.EventSystems.Tests;


public struct GeneratedViewer
{
    private PStruct_IMyOutOfNamespacePolyInterface ASDsad;
    // private ExampleParentNamespace.ParentSomething.ExampleNamespace.Something.PStruct_IMyPolyInterfaceA psA;
    // private ExampleParentNamespace.ParentSomething.ExampleNamespace.Something.PStruct_IMyPolyInterfaceB psB;

    //private PStruct_ITestGlobalPolymorphicEvent psD;
    
    public void Test()
    {
    }
}

// public struct TestNestedThing
// {
//     public Entity A;
//     public BlobString B;
//     public BlobArray<int> C;
//     public BlobPtr<Entity> D;
//     public BlobAssetReference<Collider> E;
// }


[PolymorphicStructInterface]
public interface IMyOutOfNamespacePolyInterface
{
    public void DoSomething(int a);
}

[PolymorphicStruct]
public struct MyOutOfNamespaceStructA : IMyOutOfNamespacePolyInterface
{
    public int Value;

    public void DoSomething(int a)
    {
    }
}

// [PolymorphicStruct]
// public struct MyOutOfNamespaceStructB : IMyOutOfNamespacePolyInterface
// {
//     public float3 Value;

//     public void DoSomething(int a)
//     {
//     }
// }

// namespace TestNamespaceA
// {
//     [PolymorphicStructInterface]
//     public interface IMyOutOfNamespacePolyInterface
//     {
//         public void DoSomething(int a);
//     }
//
//     [PolymorphicStruct]
//     public struct MyOutOfNamespaceStructA : IMyOutOfNamespacePolyInterface
//     {
//         public int Value;
//
//         public void DoSomething(int a)
//         {
//         }
//     }
// }


// namespace ExampleParentNamespace.ParentSomething
// {
//     namespace ExampleNamespace.Something
//     {
//         public struct TestType
//         {
//             public float2 Val;
//         }

//         public interface ITestParentInterface
//         {
//             public void DoParentInterfaceThing(ref float3 a);
//         }

//         [AllowEntitiesAndBlobsInPolymorphicStruct]
//         [PolymorphicStructInterface]
//         public interface IMyPolyInterfaceA : ITestParentInterface
//         {
//             public int PropA { get; set; }
//             public int PropB { set; }
//             public int PropC { get; }

//             public void DoSomething(int a);

//             public TGenA DoSomething2<TGenA, TGen2>(NativeList<TGenA> a, TGen2 b)
//                 where TGenA : unmanaged, IBufferElementData
//                 where TGen2 : unmanaged, IComponentData;
//         }

//         [PolymorphicStructInterface]
//         public interface IMyPolyInterfaceB
//         {
//             public void DoSomethingA(TestType a);
//             public void DoSomethingB(int a, ref NativeList<float3> b);
//         }

//         [PolymorphicStruct]
//         public struct MyStructA : IMyPolyInterfaceA, IMyPolyInterfaceB
//         {
//             public int Value;

//             public int PropA { get; set; }
//             public int PropB { get; set; }
//             public int PropC { get; }

//             public void DoSomething(int a)
//             {
//             }

//             public TGenA DoSomething2<TGenA, TGen2>(NativeList<TGenA> a, TGen2 b)
//                 where TGenA : unmanaged, IBufferElementData where TGen2 : unmanaged, IComponentData
//             {
//                 return a[0];
//             }

//             public void DoSomethingA(TestType a)
//             {
//             }

//             public void DoSomethingB(int a, ref NativeList<float3> b)
//             {
//             }

//             public void DoParentInterfaceThing(ref float3 a)
//             {
//             }
//         }

//         [PolymorphicStruct]
//         public struct MyStructB : IMyPolyInterfaceA
//         {
//             public int Value;
//             public TestNestedThing Test;

//             public int PropA { get; set; }
//             public int PropB { get; set; }
//             public int PropC { get; }

//             public void DoSomething(int a)
//             {
//             }

//             public TGenA DoSomething2<TGenA, TGen2>(NativeList<TGenA> a, TGen2 b)
//                 where TGenA : unmanaged, IBufferElementData where TGen2 : unmanaged, IComponentData
//             {
//                 return a[0];
//             }

//             public void DoParentInterfaceThing(ref float3 a)
//             {
//             }
//         }

//         [PolymorphicStruct]
//         public struct MyStructC : IMyPolyInterfaceA
//         {
//             public int Value;

//             public int PropA { get; set; }
//             public int PropB { get; set; }
//             public int PropC { get; }

//             public void DoSomething(int a)
//             {
//             }

//             public TGenA DoSomething2<TGenA, TGen2>(NativeList<TGenA> a, TGen2 b)
//                 where TGenA : unmanaged, IBufferElementData where TGen2 : unmanaged, IComponentData
//             {
//                 return a[0];
//             }

//             public void DoParentInterfaceThing(ref float3 a)
//             {
//             }
//         }

//         [PolymorphicStruct]
//         public struct MyStructD : IMyPolyInterfaceB
//         {
//             public int Value;

//             public void DoSomethingA(TestType a)
//             {
//             }

//             public void DoSomethingB(int a, ref NativeList<float3> b)
//             {

//             }

//             public TGenA DoSomething2<TGenA, TGen2>(NativeList<TGenA> a, TGen2 b)
//                 where TGenA : unmanaged, IBufferElementData where TGen2 : unmanaged, IComponentData
//             {
//                 return a[0];
//             }
//         }

//         [PolymorphicStruct]
//         public struct MyStructE : IMyPolyInterfaceB
//         {
//             public int Value;

//             public void DoSomethingA(TestType a)
//             {
//             }

//             public void DoSomethingB(int a, ref NativeList<float3> b)
//             {
//             }

//             public TGenA DoSomething2<TGenA, TGen2>(NativeList<TGenA> a, TGen2 b)
//                 where TGenA : unmanaged, IBufferElementData where TGen2 : unmanaged, IComponentData
//             {
//                 return a[0];
//             }
//         }
//     }
// }