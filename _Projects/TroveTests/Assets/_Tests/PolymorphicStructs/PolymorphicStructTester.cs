using Trove.PolymorphicStructs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

public struct GeneratedViewer
{
    UnionStruct_IMyPolyInterfaceA testA;
    UnionStruct_IMyPolyInterfaceB testB;
    ByteArray_IMyPolyInterfaceC testC;
    ByteArray_IMyPolyInterfaceD testD;
}

public struct TestNestedThing
{
    public Entity A;
    public BlobString B;
    public BlobArray<int> C;
    public BlobPtr<Entity> D;
    public BlobAssetReference<Collider> E;
}

[PolymorphicUnionStructInterface]
public interface IMyPolyInterfaceA
{
    public int PropA { get; set; }
    public int PropB { set; }
    public int PropC { get; }

    public void DoSomething(int a);

    public TGenA DoSomething2<TGenA, TGen2>(NativeList<TGenA> a, TGen2 b)
        where TGenA : unmanaged, IBufferElementData
        where TGen2 : unmanaged, IComponentData;
}

[PolymorphicUnionStructInterface]
public interface IMyPolyInterfaceB
{
    public void DoSomethingA(int a);
    public void DoSomethingB(int a, ref NativeList<float3> b);
}

[PolymorphicByteArrayInterface]
public interface IMyPolyInterfaceC
{
    public void DoSomething(int a);
}

[PolymorphicByteArrayInterface]
public interface IMyPolyInterfaceD
{
    public void DoSomethingA(int a);

    [WriteBackStructData]
    public TGenA DoSomething2<TGenA, TGen2>(NativeList<TGenA> a, TGen2 b)
        where TGenA : unmanaged, IBufferElementData
        where TGen2 : unmanaged, IComponentData;
}

[PolymorphicStruct]
public struct MyStructA : IMyPolyInterfaceA, IMyPolyInterfaceC
{
    public int Value;

    public int PropA { get; set; }
    public int PropB { get; set; }
    public int PropC { get; }

    public void DoSomething(int a)
    {
    }

    public TGenA DoSomething2<TGenA, TGen2>(NativeList<TGenA> a, TGen2 b) where TGenA : unmanaged, IBufferElementData where TGen2 : unmanaged, IComponentData
    {
        return a[0];
    }
}

[PolymorphicStruct]
public struct MyStructB : IMyPolyInterfaceA, IMyPolyInterfaceC
{
    public int Value;

    public int PropA { get; set; }
    public int PropB { get; set; }
    public int PropC { get; }

    public void DoSomething(int a)
    {
    }

    public TGenA DoSomething2<TGenA, TGen2>(NativeList<TGenA> a, TGen2 b) where TGenA : unmanaged, IBufferElementData where TGen2 : unmanaged, IComponentData
    {
        return a[0];
    }
}

[PolymorphicStruct]
public struct MyStructC : IMyPolyInterfaceA, IMyPolyInterfaceC
{
    public int Value;

    public int PropA { get; set; }
    public int PropB { get; set; }
    public int PropC { get; }

    public void DoSomething(int a)
    {
    }

    public TGenA DoSomething2<TGenA, TGen2>(NativeList<TGenA> a, TGen2 b) where TGenA : unmanaged, IBufferElementData where TGen2 : unmanaged, IComponentData
    {
        return a[0];
    }
}

[PolymorphicStruct]
public struct MyStructD : IMyPolyInterfaceB, IMyPolyInterfaceD
{
    public int Value;

    public void DoSomethingA(int a)
    {
    }

    public void DoSomethingB(int a, ref NativeList<float3> b)
    {
        
    }

    public TGenA DoSomething2<TGenA, TGen2>(NativeList<TGenA> a, TGen2 b) where TGenA : unmanaged, IBufferElementData where TGen2 : unmanaged, IComponentData
    {
        return a[0];
    }
}

[PolymorphicStruct]
public struct MyStructE : IMyPolyInterfaceB, IMyPolyInterfaceD
{
    public int Value;

    public void DoSomethingA(int a)
    {
    }

    public void DoSomethingB(int a, ref NativeList<float3> b)
    {
    }

    public TGenA DoSomething2<TGenA, TGen2>(NativeList<TGenA> a, TGen2 b) where TGenA : unmanaged, IBufferElementData where TGen2 : unmanaged, IComponentData
    {
        return a[0];
    }
}