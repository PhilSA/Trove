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

[PolymorphicTypeManagerInterface]
public interface IMyPolyInterfaceC
{
    public void DoSomething(int a);
}

[PolymorphicTypeManagerInterface]
public interface IMyPolyInterfaceD
{
    public void DoSomethingA(int a);
    [WriteBackStructData]
    public void DoSomethingB(int a);
}

[PolymorphicStruct]
public struct MyStructA : IMyPolyInterfaceA, IMyPolyInterfaceC
{ 
    public int Value;
     
    public void DoSomething(int a)
    {
    }
} 
 
[PolymorphicStruct]
public struct MyStructB : IMyPolyInterfaceA, IMyPolyInterfaceC
{
    public int Value; 

    public void DoSomething(int a)
    {  
        UnionStruct_IMyPolyInterfaceA testA;
        UnionStruct_IMyPolyInterfaceB testB; 
        TypeManager_IMyPolyInterfaceC testC;
        TypeManager_IMyPolyInterfaceD testD;  
    }
}

[PolymorphicStruct]
public struct MyStructC : IMyPolyInterfaceA, IMyPolyInterfaceC
{
    public int Value;

    public void DoSomething(int a)
    {
    }
}

[PolymorphicStruct]
public struct MyStructD : IMyPolyInterfaceB, IMyPolyInterfaceD
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
public struct MyStructE : IMyPolyInterfaceB, IMyPolyInterfaceD
{
    public int Value;

    public void DoSomethingA(int a)
    {
    }

    public void DoSomethingB(int a)
    {
    }
}