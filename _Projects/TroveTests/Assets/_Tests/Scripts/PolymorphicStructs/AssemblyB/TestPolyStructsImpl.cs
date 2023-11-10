using Trove.PolymorphicStructs;
using Unity.Mathematics;
using Unity.Entities;


[PolymorphicStruct]
public partial struct PolyStructA : ITestPolyStruct
{
    public float3 A;
    public Entity B;

    public void DoSomething()
    {
    }
}

[PolymorphicStruct]
public partial struct PolyStructB : ITestPolyStruct
{
    public float A;
    public Entity B;


    public void DoSomething()
    {
    }
}

[PolymorphicStruct]
public partial struct PolyStructC : ITestPolyStruct
{
    public float A;

    public void DoSomething()
    {
    }
}