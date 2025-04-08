
The following code:
```cs
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
```

Generates this code:
```cs
using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Trove;

[StructLayout(LayoutKind.Explicit)]
public unsafe partial struct PolyExampleA : IPolymorphicObject
{
	[FieldOffset(0)]
	public int CurrentTypeId;
	[FieldOffset(4)]
	public ExampleA1 Field0;
	[FieldOffset(4)]
	public ExampleA2 Field1;
	
	
	public static implicit operator PolyExampleA (ExampleA1 s)
	{
		return new PolyExampleA
		{
			CurrentTypeId = 0,
			Field0 = s,
		};
	}
	
	public static implicit operator ExampleA1 (PolyExampleA s)
	{
		return s.Field0;
	}
	
	public static implicit operator PolyExampleA (ExampleA2 s)
	{
		return new PolyExampleA
		{
			CurrentTypeId = 1,
			Field1 = s,
		};
	}
	
	public static implicit operator ExampleA2 (PolyExampleA s)
	{
		return s.Field1;
	}
	
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int GetTypeId()
	{
		return (int)CurrentTypeId;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int GetDataBytesSize()
	{
		switch (CurrentTypeId)
		{
			case 0:
			{
				return sizeof(ExampleA1);
			}
			case 1:
			{
				return sizeof(ExampleA2);
			}
		}
		return 0;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public int GetDataBytesSizeFor(int typeId)
	{
		switch (typeId)
		{
			case 0:
			{
				return sizeof(ExampleA1);
			}
			case 1:
			{
				return sizeof(ExampleA2);
			}
		}
		return 0;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void WriteDataTo(byte* dstPtr, out int writeSize)
	{
		switch (CurrentTypeId)
		{
			case 0:
			{
				writeSize = sizeof(ExampleA1);
				*(ExampleA1*)dstPtr = Field0;
				return;
			}
			case 1:
			{
				writeSize = sizeof(ExampleA2);
				*(ExampleA2*)dstPtr = Field1;
				return;
			}
		}
		
		writeSize = 0;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SetDataFrom(int typeId, byte* srcPtr, out int readSize)
	{
		CurrentTypeId = typeId;
		
		switch (CurrentTypeId)
		{
			case 0:
			{
				readSize = sizeof(ExampleA1);
				 Field0 = *(ExampleA1*)srcPtr;
				return;
			}
			case 1:
			{
				readSize = sizeof(ExampleA2);
				 Field1 = *(ExampleA2*)srcPtr;
				return;
			}
		}
		
		readSize = 0;
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void DoSomething( int p1)
	{
		switch (CurrentTypeId)
		{
			case 0:
			{
				Field0.DoSomething( p1);
				return;
			}
			case 1:
			{
				Field1.DoSomething( p1);
				return;
			}
		}
	}
	
}
```