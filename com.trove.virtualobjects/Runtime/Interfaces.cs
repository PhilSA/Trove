using Unity.Entities;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Trove.PolymorphicElements
{
	public interface IPolymorphicElementWriter
	{
		public ushort GetTypeId();
		public unsafe int GetTotalSize();
		public unsafe void Write(PolymorphicElementPtr ptr);
	}
}