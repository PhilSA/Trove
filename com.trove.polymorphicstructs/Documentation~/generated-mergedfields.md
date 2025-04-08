
The following code:
```cs
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
```

Generates this code:
```cs
using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Trove;

public unsafe partial struct PolyExampleB
{
	public int CurrentTypeId;
	public int Field0;
	public Unity.Collections.NativeList<Unity.Mathematics.float3> Field1;
	public int Field2;
	public Unity.Mathematics.float3 Field3;
	
	
	public static implicit operator PolyExampleB (ExampleB1 s)
	{
		PolyExampleB newPolyStruct = default;
		newPolyStruct.CurrentTypeId = 2;
		newPolyStruct.Field0 = s.A;
		newPolyStruct.Field1 = s.B;
		return newPolyStruct;
	}
	
	public static implicit operator ExampleB1 (PolyExampleB s)
	{
		ExampleB1 newStruct = default;
		newStruct.A = s.Field0;
		newStruct.B = s.Field1;
		return newStruct;
	}
	
	public static implicit operator PolyExampleB (ExampleB2 s)
	{
		PolyExampleB newPolyStruct = default;
		newPolyStruct.CurrentTypeId = 3;
		newPolyStruct.Field0 = s.A;
		newPolyStruct.Field2 = s.B;
		newPolyStruct.Field3 = s.C;
		return newPolyStruct;
	}
	
	public static implicit operator ExampleB2 (PolyExampleB s)
	{
		ExampleB2 newStruct = default;
		newStruct.A = s.Field0;
		newStruct.B = s.Field2;
		newStruct.C = s.Field3;
		return newStruct;
	}
	
	
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void DoSomething( int p1)
	{
		switch (CurrentTypeId)
		{
			case 2:
			{
				ExampleB1 specificStruct = this;
				specificStruct.DoSomething( p1);
				this = specificStruct;
				return;
			}
			case 3:
			{
				ExampleB2 specificStruct = this;
				specificStruct.DoSomething( p1);
				this = specificStruct;
				return;
			}
		}
	}
	
}
```