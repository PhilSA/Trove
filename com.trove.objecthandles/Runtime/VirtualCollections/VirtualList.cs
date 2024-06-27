
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Logging;

namespace Trove.ObjectHandles
{
    public unsafe struct UnsafeVirtualList<T>
        where T : unmanaged
    {
        internal int _length;
        internal int _capacity;
        internal VirtualObjectHandle<T> _dataHandle;

        public int Length => _length;
        public int Capacity => _capacity;
        public VirtualObjectHandle<T> DataHandle => _dataHandle;

        public const float GrowFactor = 2f;

        public static UnsafeVirtualList<T> Allocate<V>(
            ref V voView,
            int capacity)
            where V : unmanaged, IVirtualObjectView
        {
            UnsafeVirtualList<T> list = new UnsafeVirtualList<T>();
            list._length = 0;
            list._capacity = capacity;
            list._dataHandle = VirtualObjectManager.AllocateObject(
                ref voView,
                list.GetDataCapacitySizeBytes(),
                out T* _);

            return list;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetDataCapacitySizeBytes()
        {
            return UnsafeUtility.SizeOf<T>() * _capacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetSizeBytes()
        {
            return UnsafeUtility.SizeOf<UnsafeVirtualList<T>>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            this._length = 0;
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
                out T* listDataPtr))
            {
                value = listDataPtr[index];
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Note: unsafe because as soon as the list grows and gets reallocated, the ref is no longer valid
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
                out T* listDataPtr))
            {
                success = true;
                return ref *(listDataPtr + (long)index);
            }

            success = false;
            return ref *(T*)voView.GetDataPtr();
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
                out T* listDataPtr))
            {
                listDataPtr[index] = value;
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
                out T* listDataPtr))
            {
                unsafeArray = new UnsafeArrayView<T>(listDataPtr, this._length);
                return true;
            }

            unsafeArray = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetCapacity<V>(
            ref V voView,
            int newCapacity)
            where V : unmanaged, IVirtualObjectView
        {
            // TODO: if new capacity is smaller, maybe just free superfluous memory

            if (this._capacity != newCapacity && newCapacity > 0)
            {
                if (VirtualObjectManager.TryGetObjectValuePtr(
                    ref voView,
                    this._dataHandle,
                    out T* listDataPtr))
                {
                    VirtualObjectHandle<T> newDataHandle = VirtualObjectManager.AllocateObject(
                        ref voView,
                        newCapacity * UnsafeUtility.SizeOf<T>(),
                        out T* newListDataPtr);

                    UnsafeUtility.MemCpy(newListDataPtr, listDataPtr, this._length * UnsafeUtility.SizeOf<T>());

                    VirtualObjectManager.FreeObject(
                        ref voView,
                        this._dataHandle);

                    this._capacity = newCapacity;
                    return true;
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryResize<V>(
            ref V voView,
            int newLength)
            where V : unmanaged, IVirtualObjectView
        {
            if (newLength > 0 && newLength < this._length)
            {
                if (newLength <= this._capacity || TrySetCapacity(ref voView, (int)math.ceil(newLength * GrowFactor)))
                {
                    this._length = newLength;
                    return true;
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd<V>(
            ref V voView,
            T value)
            where V : unmanaged, IVirtualObjectView
        {
            if (VirtualObjectManager.TryGetObjectValuePtr(
                ref voView,
                this._dataHandle,
                out T* listDataPtr))
            {
                int newLength = this._length + 1;
                if (newLength > this._capacity)
                {
                    TrySetCapacity(ref voView, (int)math.ceil(newLength * GrowFactor));

                    // Re-get ptr after realloc
                    VirtualObjectManager.TryGetObjectValuePtr(
                        ref voView,
                        this._dataHandle,
                        out listDataPtr);
                }

                listDataPtr[this._length] = value;
                this._length = newLength;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryInsertAt<V>(
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
                out T* listDataPtr))
            {
                int newLength = this._length + 1;
                if (newLength > this._capacity)
                {
                    TrySetCapacity(ref voView, (int)math.ceil(newLength * GrowFactor));

                    // Re-get ptr after realloc
                    VirtualObjectManager.TryGetObjectValuePtr(
                        ref voView,
                        this._dataHandle,
                        out listDataPtr);
                }
                T* dataStartPtr = listDataPtr + (long)index;
                T* dataDestinationPtr = dataStartPtr + (long)1;
                int dataSize = (this._length - index) * UnsafeUtility.SizeOf<T>();
                UnsafeUtility.MemCpy(dataDestinationPtr, dataStartPtr, dataSize);
                *dataStartPtr = value;
                this._length = newLength;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemoveAt<V>(
            ref V voView,
            int index)
            where V : unmanaged, IVirtualObjectView
        {
            if (!ObjectManagerUtilities.IndexIsValid(index, _length))
            {
                return false;
            }

            if (VirtualObjectManager.TryGetObjectValuePtr(
                ref voView,
                this._dataHandle,
                out T* listDataPtr))
            {
                if (index < this._length - 1)
                {
                    T* dataDestinationPtr = listDataPtr + (long)index;
                    T* dataStartPtr = dataDestinationPtr + (long)1;
                    int movedDataSize = (this._length - (index + 1)) * UnsafeUtility.SizeOf<T>();
                    UnsafeUtility.MemCpy(dataDestinationPtr, dataStartPtr, movedDataSize);
                }
                this._length -= 1;
                return true;

            }
            return false;
        }
    }

    public unsafe struct VirtualListHandle<T> where T : unmanaged
    {
        internal readonly VirtualObjectHandle<UnsafeVirtualList<T>> ListHandle;

        internal VirtualListHandle(VirtualObjectHandle<UnsafeVirtualList<T>> unsafeListHandle)
        {
            ListHandle = unsafeListHandle;
        }

        public static VirtualListHandle<T> Allocate<V>(
            ref V voView,
            int capacity)
            where V : unmanaged, IVirtualObjectView
        {
            UnsafeVirtualList<T> unsafeList = UnsafeVirtualList<T>.Allocate(ref voView, capacity);
            VirtualObjectHandle<UnsafeVirtualList<T>> unsafeListHandle = VirtualObjectManager.CreateObject(ref voView, unsafeList);
            VirtualListHandle<T> list = new VirtualListHandle<T>(unsafeListHandle);
            return list;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetLengthAndCapacity<V>(
            ref V voView,
            out int length,
            out int capacity)
            where V : unmanaged, IVirtualObjectView
        {
            ref UnsafeVirtualList<T> unsafeListRef = ref VirtualObjectManager.TryGetObjectValueRef(ref voView, ListHandle, out bool success);
            if (success)
            {
                length = unsafeListRef._length;
                capacity = unsafeListRef._capacity;
                return true;
            }
            length = default;
            capacity = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryClear<V>(ref V voView)
            where V : unmanaged, IVirtualObjectView
        {
            ref UnsafeVirtualList<T> unsafeListRef = ref VirtualObjectManager.TryGetObjectValueRef(ref voView, ListHandle, out bool success);
            if(success)
            {
                unsafeListRef.Clear();
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetElementAt<V>(
            ref V voView,
            int index,
            out T value)
            where V : unmanaged, IVirtualObjectView
        {
            ref UnsafeVirtualList<T> unsafeListRef = ref VirtualObjectManager.TryGetObjectValueRef(ref voView, ListHandle, out bool success);
            if (success)
            {
                return unsafeListRef.TryGetElementAt(ref voView, index, out value);
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Note: unsafe because as soon as the list grows and gets reallocated, the ref is no longer valid
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T TryGetUnsafeRefElementAt<V>(
            ref V voView,
            int index,
            out bool success)
            where V : unmanaged, IVirtualObjectView
        {
            ref UnsafeVirtualList<T> unsafeListRef = ref VirtualObjectManager.TryGetObjectValueRef(ref voView, ListHandle, out success);
            if (success)
            {
                return ref unsafeListRef.TryGetUnsafeRefElementAt(ref voView, index, out success);
            }
            return ref *(T*)default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetElementAt<V>(
            ref V voView,
            int index,
            T value)
            where V : unmanaged, IVirtualObjectView
        {
            ref UnsafeVirtualList<T> unsafeListRef = ref VirtualObjectManager.TryGetObjectValueRef(ref voView, ListHandle, out bool success);
            if (success)
            {
                return unsafeListRef.TrySetElementAt(ref voView, index, value); ;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAsUnsafeArrayView<V>(
            ref V voView,
            out UnsafeArrayView<T> unsafeArray)
            where V : unmanaged, IVirtualObjectView
        {
            ref UnsafeVirtualList<T> unsafeListRef = ref VirtualObjectManager.TryGetObjectValueRef(ref voView, ListHandle, out bool success);
            if (success)
            {
                success = unsafeListRef.TryAsUnsafeArrayView(ref voView, out unsafeArray);
                return success;
            }
            unsafeArray = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetCapacity<V>(
            ref V voView,
            int newCapacity)
            where V : unmanaged, IVirtualObjectView
        {
            ref UnsafeVirtualList<T> unsafeListRef = ref VirtualObjectManager.TryGetObjectValueRef(ref voView, ListHandle, out bool success);
            if (success)
            {
                return unsafeListRef.TrySetCapacity(ref voView, newCapacity);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryResize<V>(
            ref V voView,
            int newLength)
            where V : unmanaged, IVirtualObjectView
        {
            ref UnsafeVirtualList<T> unsafeListRef = ref VirtualObjectManager.TryGetObjectValueRef(ref voView, ListHandle, out bool success);
            if (success)
            {
                return unsafeListRef.TryResize(ref voView, newLength);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd<V>(
            ref V voView,
            T value)
            where V : unmanaged, IVirtualObjectView
        {
            ref UnsafeVirtualList<T> unsafeListRef = ref VirtualObjectManager.TryGetObjectValueRef(ref voView, ListHandle, out bool success);
            if (success)
            {
                return unsafeListRef.TryAdd(ref voView, value);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryInsertAt<V>(
            ref V voView,
            int index,
            T value)
            where V : unmanaged, IVirtualObjectView
        {
            ref UnsafeVirtualList<T> unsafeListRef = ref VirtualObjectManager.TryGetObjectValueRef(ref voView, ListHandle, out bool success);
            if (success)
            {
                return unsafeListRef.TryInsertAt(ref voView, index, value);
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryRemoveAt<V>(
            ref V voView,
            int index)
            where V : unmanaged, IVirtualObjectView
        {
            ref UnsafeVirtualList<T> unsafeListRef = ref VirtualObjectManager.TryGetObjectValueRef(ref voView, ListHandle, out bool success);
            if (success)
            {
                return unsafeListRef.TryRemoveAt(ref voView, index);
            }
            return false;
        }
    }
}