using UnityEngine;
using Trove.PolymorphicStructs.Generated;

[PolymorphicUnionStructInterface]
public interface IMyPolyInterfaceA
{ 
    public void DoSomething(int a);
}

[PolymorphicUnionStructInterface]
public interface IMyPolyInterfaceB
{
    public void DoSomethingA(int a);
    public void DoSomethingB(int a); 
}

[PolymorphicStruct]  
public struct MyStructA : IMyPolyInterfaceA
{ 
    public int Value;

    public void DoSomething(int a)
    {
    }
}

[PolymorphicStruct]
public struct MyStructB : IMyPolyInterfaceA
{
    public int Value;

    public void DoSomething(int a)
    {
    }
}

[PolymorphicStruct]
public struct MyStructC : IMyPolyInterfaceA
{
    public int Value;

    public void DoSomething(int a)
    {
    }
}

[PolymorphicStruct]
public struct MyStructD : IMyPolyInterfaceB
{
    public int Value;

    public void DoSomethingA(int a)
    {
    }

    public void DoSomethingB(int a)
    {
    }
}

[PolymorphicStruct]
public struct MyStructE : IMyPolyInterfaceB
{
    public int Value;

    public void DoSomethingA(int a)
    {
    }

    public void DoSomethingB(int a)
    {
    }
}