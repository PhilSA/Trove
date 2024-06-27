using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Trove.ObjectHandles
{

    /// <summary>
    /// Note: unsafe due to operating on a ptr to the data in the dynamicBuffer of bytes
    /// </summary>
    public unsafe struct UnsafeArrayView<T>
        where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction]
        internal T* _ptr;
        internal int _length;

        public int Length => _length;

        public UnsafeArrayView(T* ptr, int length)
        {
            _ptr = ptr;
            _length = length;
        }

        public T this[int i]
        {
            // TODO: index bounds check
            get
            {
                return _ptr[i];
            }
            set
            {
                _ptr[i] = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T* GetUnsafePtr()
        {
            return _ptr;
        }
    }

    public unsafe struct UnsafeVirtualArray<T>
        where T : unmanaged
    {
        internal int _length;
        internal VirtualObjectHandle<T> _dataHandle;

        public int Length => _length;
        public VirtualObjectHandle<T> DataHandle => _dataHandle;

        public static UnsafeVirtualArray<T> Allocate<V>(
            ref V voView,
            int capacity)
            where V : unmanaged, IVirtualObjectView
        {
            UnsafeVirtualArray<T> array = new UnsafeVirtualArray<T>();
            array._length = 0;

            int objectSize = array.GetSizeBytes();
            VirtualObjectHandle<T> _dataHandle = VirtualObjectManager.AllocateObject(
                ref voView,
                objectSize,
                out T* _);

            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetDataCapacitySizeBytes()
        {
            return UnsafeUtility.SizeOf<T>() * _length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSizeBytes()
        {
            return UnsafeUtility.SizeOf<UnsafeVirtualArray<T>>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetElementAt<V>(
            ref V voView,
            int index,
            out T value)
            where V : unmanaged, IVirtualObjectView
        {
            if (!ObjectManagerUtilities.IndexIsValid(index, _length))
            {
                value = default;
                return false;
            }

            if (VirtualObjectManager.TryGetObjectValuePtr(
                ref voView,
                this._dataHandle,
                out T* arrayDataPtr))
            {
                value = arrayDataPtr[index];
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Note: unsafe because as soon as the array grows and gets reallocated, the ref is no longer valid
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T TryGetUnsafeRefElementAt<V>(
            ref V voView,
            int index,
            out bool success)
            where V : unmanaged, IVirtualObjectView
        {
            if (!ObjectManagerUtilities.IndexIsValid(index, _length))
            {
                success = false;
                return ref *(T*)default;
            }

            if (VirtualObjectManager.TryGetObjectValuePtr(
                ref voView,
                this._dataHandle,
                out T* arrayDataPtr))
            {
                success = true;
                return ref *(arrayDataPtr + (long)index);
            }

            success = false;
            return ref *(T*)default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetElementAt<V>(
            ref V voView,
            int index,
            T value)
            where V : unmanaged, IVirtualObjectView
        {
            if (!ObjectManagerUtilities.IndexIsValid(index, _length))
            {
                return false;
            }

            if (VirtualObjectManager.TryGetObjectValuePtr(
                ref voView,
                this._dataHandle,
                out T* arrayDataPtr))
            {
                arrayDataPtr[index] = value;
                return true;
            }

            return false;
        }

        public bool TryAsUnsafeArrayView<V>(
            ref V voView,
            out UnsafeArrayView<T> unsafeArray)
            where V : unmanaged, IVirtualObjectView
        {
            if (VirtualObjectManager.TryGetObjectValuePtr(
                ref voView,
                this._dataHandle,
                out T* arrayDataPtr))
            {
                unsafeArray = new UnsafeArrayView<T>(arrayDataPtr, this._length);
                return true;
            }

            unsafeArray = default;
            return false;
        }
    }

    public unsafe struct VirtualArrayHandle<T> where T : unmanaged
    {
        internal readonly VirtualObjectHandle<UnsafeVirtualArray<T>> ArrayHandle;

        internal VirtualArrayHandle(VirtualObjectHandle<UnsafeVirtualArray<T>> unsafeArrayHandle)
        {
            ArrayHandle = unsafeArrayHandle;
        }

        public static implicit operator VirtualArrayHandle<T>(VirtualObjectHandle<UnsafeVirtualArray<T>> o) => new VirtualArrayHandle<T>(o);
        public static implicit operator VirtualObjectHandle<UnsafeVirtualArray<T>>(VirtualArrayHandle<T> o) => o.ArrayHandle;

        public static VirtualArrayHandle<T> Allocate<V>(
            ref V voView,
            int capacity)
            where V : unmanaged, IVirtualObjectView
        {
            UnsafeVirtualArray<T> unsafeArray = UnsafeVirtualArray<T>.Allocate(ref voView, capacity);
            VirtualObjectHandle<UnsafeVirtualArray<T>> unsafeArrayHandle = VirtualObjectManager.CreateObject(ref voView, unsafeArray);
            VirtualArrayHandle<T> array = unsafeArrayHandle;
            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetLength<V>(
            ref V voView,
            out int length)
            where V : unmanaged, IVirtualObjectView
        {
            ref UnsafeVirtualArray<T> unsafeArrayRef = ref VirtualObjectManager.TryGetObjectValueRef(ref voView, ArrayHandle, out bool success);
            if (success)
            {
                length = unsafeArrayRef._length;
                return true;
            }
            length = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetElementAt<V>(
            ref V voView,
            int index,
            out T value)
            where V : unmanaged, IVirtualObjectView
        {
            ref UnsafeVirtualArray<T> unsafeArrayRef = ref VirtualObjectManager.TryGetObjectValueRef(ref voView, ArrayHandle, out bool success);
            if (success)
            {
                return unsafeArrayRef.TryGetElementAt(ref voView, index, out value);
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Note: unsafe because as soon as the array grows and gets reallocated, the ref is no longer valid
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T TryGetUnsafeRefElementAt<V>(
            ref V voView,
            int index,
            out bool success)
            where V : unmanaged, IVirtualObjectView
        {
            ref UnsafeVirtualArray<T> unsafeArrayRef = ref VirtualObjectManager.TryGetObjectValueRef(ref voView, ArrayHandle, out success);
            if (success)
            {
                return ref unsafeArrayRef.TryGetUnsafeRefElementAt(ref voView, index, out success);
            }
            return ref *(T*)voView.GetDataPtr();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetElementAt<V>(
            ref V voView,
            int index,
            T value)
            where V : unmanaged, IVirtualObjectView
        {
            ref UnsafeVirtualArray<T> unsafeArrayRef = ref VirtualObjectManager.TryGetObjectValueRef(ref voView, ArrayHandle, out bool success);
            if (success)
            {
                return unsafeArrayRef.TrySetElementAt(ref voView, index, value); ;
            }
            return false;
        }

        public bool TryAsUnsafeArrayView<V>(
            ref V voView,
            out UnsafeArrayView<T> unsafeArray)
            where V : unmanaged, IVirtualObjectView
        {
            ref UnsafeVirtualArray<T> unsafeArrayRef = ref VirtualObjectManager.TryGetObjectValueRef(ref voView, ArrayHandle, out bool success);
            if (success)
            {
                return unsafeArrayRef.TryAsUnsafeArrayView(ref voView, out unsafeArray);
            }
            unsafeArray = default;
            return false;
        }
    }
}