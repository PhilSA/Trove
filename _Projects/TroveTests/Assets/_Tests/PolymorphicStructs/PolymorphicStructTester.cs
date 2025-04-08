using System.Runtime.InteropServices;
using Trove.PolymorphicStructs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Trove;
using Unity.Collections.LowLevel.Unsafe;

public partial struct PolyExampleA { }
public partial struct PolyExampleB { }


[PolymorphicStructInterface]
public interface IExampleA
{
    public void DoSomething(int p1);
}

[PolymorphicStruct]
public struct ExampleA1 : IExampleA
{
    public int A;
    public NativeList<float3> B;
    
    public void DoSomething(int p1)
    { }
}

[PolymorphicStruct]
public struct ExampleA2 : IExampleA
{
    public int A;
    public int B;
    public float3 C;
    
    public void DoSomething(int p1)
    { }
}

[IsMergedFieldsPolymorphicStruct]
[PolymorphicStructInterface]
public interface IExampleB
{
    public void DoSomething(int p1);
}

[PolymorphicStruct]
public struct ExampleB1 : IExampleB
{
    public int A;
    public NativeList<float3> B;
    
    public void DoSomething(int p1)
    { }
}

[PolymorphicStruct]
public struct ExampleB2 : IExampleB
{
    public int A;
    public int B;
    public float3 C;
    
    public void DoSomething(int p1)
    { }
}