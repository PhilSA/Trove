using Unity.Entities;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Trove.PolymorphicElements
{
	public interface IPolymorphicUnionElement
	{
		public void AppendElementVariableSized(ref NativeStream.Writer streamWriter);
		public void AppendElementVariableSized(ref UnsafeStream.Writer streamWriter);
		public void AppendElementVariableSized<S>(ref S streamWriter) where S : unmanaged, IStreamWriter;
		public PolymorphicElementMetaData AddElementVariableSized(ref DynamicBuffer<byte> buffer);
		public PolymorphicElementMetaData AddElementVariableSized<B>(ref DynamicBuffer<B> buffer) where B : unmanaged, IBufferElementData, IByteBufferElement;
		public PolymorphicElementMetaData AddElementVariableSized(ref NativeList<byte> list);
		public PolymorphicElementMetaData AddElementVariableSized(ref UnsafeList<byte> list);
		public PolymorphicElementMetaData AddElementVariableSized<L>(ref L list) where L : unmanaged, IByteList;
		public PolymorphicElementMetaData InsertElementVariableSized(ref DynamicBuffer<byte> buffer, int atByteIndex);
		public PolymorphicElementMetaData InsertElementVariableSized<B>(ref DynamicBuffer<B> buffer, int atByteIndex) where B : unmanaged, IBufferElementData, IByteBufferElement;
		public PolymorphicElementMetaData InsertElementVariableSized(ref NativeList<byte> list, int atByteIndex);
		public PolymorphicElementMetaData InsertElementVariableSized(ref UnsafeList<byte> list, int atByteIndex);
		public PolymorphicElementMetaData InsertElementVariableSized<L>(ref L list, int atByteIndex) where L : unmanaged, IByteList;
	}
}