using UnityEngine;
using Trove.PolymorphicStructs.Generated;

//[PolymorphicUnionStructInterface]
public interface IMyPolyInterfaceA
{
    public void DoSomething(int a);
}

//[PolymorphicUnionStructInterface]
public interface IMyPolyInterfaceB  
{
    public void DoSomething(int a);
}

[PolymorphicStruct]  
public struct MyStructA
{
    public int Value;
}
  
//[PolymorphicStruct]
//public struct MyStructB
//{
//    public int Value;
//}

//[PolymorphicStruct]
//public struct MyStructC
//{
//    public int Value;
//}

//[PolymorphicStruct]
//public struct MyStructD
//{
//    public int Value;
//}

//[PolymorphicStruct]
//public struct MyStructE
//{
//    public int Value;
//}