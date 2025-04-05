using Trove.PolymorphicStructs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Trove;

public struct GeneratedViewer
{
    private PStruct_IMyPolyInterfaceA psA;
    private PStruct_IMyPolyInterfaceB psB;

    public void Test()
    {
        // Add
        
        // Read and execute all
        DynamicBuffer<byte> buffer = new DynamicBuffer<byte>();
        int readIndex = 0;
        while (readIndex < buffer.Length)
        {
            PolymorphicObjectUtilities.GetObject(ref buffer, readIndex, out PStruct_IMyPolyInterfaceA pstruct, out int readSize);
            readIndex += readSize;
        }
    }
}

public struct TestNestedThing
{
    public Entity A;
    public BlobString B;
    public BlobArray<int> C;
    public BlobPtr<Entity> D;
    public BlobAssetReference<Collider> E;
}

[AllowEntitiesAndBlobsInPolymorphicStruct]
[PolymorphicStructInterface]
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

[PolymorphicStructInterface]
public interface IMyPolyInterfaceB
{
    public void DoSomethingA(int a);
    public void DoSomethingB(int a, ref NativeList<float3> b);
}

[PolymorphicStruct]
public struct MyStructA : IMyPolyInterfaceA, IMyPolyInterfaceB
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

    public void DoSomethingA(int a)
    {
    }

    public void DoSomethingB(int a, ref NativeList<float3> b)
    {
    }
}

[PolymorphicStruct]
public struct MyStructB : IMyPolyInterfaceA
{
    public int Value;
    public TestNestedThing Test;

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
public struct MyStructC : IMyPolyInterfaceA
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
public struct MyStructD : IMyPolyInterfaceB
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
public struct MyStructE : IMyPolyInterfaceB
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