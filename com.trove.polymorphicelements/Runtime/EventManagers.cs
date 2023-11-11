using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

namespace Trove.PolymorphicElements
{
    public unsafe struct EventList : IPolymorphicList
    {
        private UnsafeList<byte> List;

        public int Length => List.Length;

        public byte* Ptr => List.Ptr;

        public EventList(int initialCapacity, Allocator allocator)
        {
            List = new UnsafeList<byte>(initialCapacity, allocator);
        }

        public void Dispose(JobHandle dep = default)
        {
            if (List.IsCreated)
            {
                List.Dispose(dep);
            }
        }

        public void Resize(int newLength)
        {
            List.Resize(newLength);
        }
    }
    public struct EventStream
    {
        private UnsafeStream Stream;

        public EventStream(int bufferCount, Allocator allocator)
        {
            Stream =new UnsafeStream(bufferCount, allocator);
        }

        public void Dispose(JobHandle dep = default)
        {
            if (Stream.IsCreated)
            {
                Stream.Dispose();
            }
        }

        public Writer AsWriter()
        {
            return new Writer
            {
                StreamWriter = Stream.AsWriter(),
            };
        }

        public Reader AsReader()
        {
            return new Reader
            {
                StreamReader = Stream.AsReader(),
            };
        }

        public struct Writer : IPolymorphicStreamWriter
        {
            internal UnsafeStream.Writer StreamWriter;

            public int ForEachCount => StreamWriter.ForEachCount;
            public void BeginForEachIndex(int i) => StreamWriter.BeginForEachIndex(i);
            public void EndForEachIndex() => StreamWriter.EndForEachIndex();

            public void Write<T>(T t) where T : unmanaged
            {
                StreamWriter.Write<T>(t);
            }
        }

        public struct Reader : IPolymorphicStreamReader
        {
            internal UnsafeStream.Reader StreamReader;

            public int ForEachCount => StreamReader.ForEachCount;
            public int RemainingItemCount => StreamReader.RemainingItemCount;

            public void BeginForEachIndex(int i) => StreamReader.BeginForEachIndex(i);
            public void EndForEachIndex() => StreamReader.EndForEachIndex();

            public T Read<T>() where T : unmanaged
            {
                return StreamReader.Read<T>();
            }
        }
    }

    public struct EventListManager
    {
        private UnsafeList<EventList> _eventCollections;
        private Allocator _allocator;
        private int _eventListIterator;

        public EventListManager(ref SystemState state, int initialCapacity = 16)
        {
            _eventCollections = new UnsafeList<EventList>(initialCapacity, Allocator.Persistent);
            _allocator = state.WorldUpdateAllocator;
            _eventListIterator = 0;
        }

        public void Dispose(JobHandle dep = default)
        {
            for (int i = 0; i < _eventCollections.Length; i++)
            {
                _eventCollections[i].Dispose(dep);
            }

            if (_eventCollections.IsCreated)
            {
                _eventCollections.Dispose();
            }
        }

        public EventList CreateEventList(int initialByteCapacity = 1000)
        {
            EventList list = new EventList(initialByteCapacity, _allocator);
            _eventCollections.Add(list);
            return list;
        }

        public void BeginEventListIteration()
        {
            _eventListIterator = 0;
        }

        public void DisposeAndClearEventLists()
        {
            for (int i = 0; i < _eventCollections.Length; i++)
            {
                _eventCollections[i].Dispose();
            }
            _eventCollections.Clear();
        }

        public bool NextEventList(out EventList eventList)
        {
            if (_eventListIterator < _eventCollections.Length)
            {
                eventList = _eventCollections[_eventListIterator];
                _eventListIterator++;
                return true;
            }

            eventList = default;
            return false;
        }
    }

    public unsafe struct EventStreamManager
    {
        [NativeDisableUnsafePtrRestriction]
        private UnsafeList<EventStream> _eventCollections;
        private Allocator _allocator;
        private int _eventStreamReaderIterator;


        public EventStreamManager(ref SystemState state, int initialCapacity = 16)
        {
            _eventCollections = new UnsafeList<EventStream>(initialCapacity, Allocator.Persistent);
            _allocator = state.WorldUpdateAllocator;
            _eventStreamReaderIterator = 0;
        }

        public void Dispose(JobHandle dep = default)
        {
            for (int i = 0; i < _eventCollections.Length; i++)
            {
                _eventCollections[i].Dispose(dep);
            }

            if (_eventCollections.IsCreated)
            {
                _eventCollections.Dispose(dep);
            }
        }

        public EventStream.Writer CreateEventStreamWriter(int bufferCount)
        {
            EventStream stream = new EventStream(bufferCount, _allocator);
            _eventCollections.Add(stream);
            return stream.AsWriter();
        }

        public void BeginEventStreamReaderIteration()
        {
            _eventStreamReaderIterator = 0;
        }

        public void DisposeAndClearEventStreams(JobHandle dep = default)
        {
            for (int i = 0; i < _eventCollections.Length; i++)
            {
                _eventCollections.ElementAt(i).Dispose(dep);
            }
            _eventCollections.Clear();
        }

        public bool NextEventStreamReader(out EventStream.Reader streamReader)
        {
            if (_eventStreamReaderIterator < _eventCollections.Length)
            {
                streamReader = _eventCollections.ElementAt(_eventStreamReaderIterator).AsReader();
                _eventStreamReaderIterator++;
                return true;
            }

            streamReader = default;
            return false;
        }

        public string GetDebugLength()
        {
            return $" Collections length is {_eventCollections.Length}";
        }
    }
}