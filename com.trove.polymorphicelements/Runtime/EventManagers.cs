using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

namespace Trove.PolymorphicElements
{
    public struct EventStream
    {
        private UnsafeStream Stream;

        public EventStream(int bufferCount, Allocator allocator)
        {
            Stream = new UnsafeStream(bufferCount, allocator);
        }

        public JobHandle Dispose(JobHandle dep = default)
        {
            if (Stream.IsCreated)
            {
                return Stream.Dispose(dep);
            }

            return dep;
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

    public unsafe struct EventStreamManager
    {
        private NativeList<EventStream> _eventStreamsList;
        private Allocator _allocator;
        private int _eventStreamReaderIterator;


        public EventStreamManager(ref SystemState state, int initialCapacity = 16)
        {
            _eventStreamsList = new NativeList<EventStream>(initialCapacity, Allocator.Persistent);
            _allocator = state.WorldUpdateAllocator;
            _eventStreamReaderIterator = 0;
        }

        public JobHandle Dispose(JobHandle dep = default)
        {
            for (int i = 0; i < _eventStreamsList.Length; i++)
            {
                dep = JobHandle.CombineDependencies(dep, _eventStreamsList[i].Dispose(dep));
            }

            if (_eventStreamsList.IsCreated)
            {
                dep = _eventStreamsList.Dispose(dep);
            }

            return dep;
        }

        public EventStream.Writer CreateEventStreamWriter(int bufferCount, Allocator allocator = Allocator.Persistent)
        {
            EventStream stream = new EventStream(bufferCount, allocator); 
            _eventStreamsList.Add(stream);
            return stream.AsWriter();
        }

        public void BeginEventStreamReaderIteration()
        {
            _eventStreamReaderIterator = 0;
        }

        public JobHandle DisposeAndClearEventStreams(JobHandle dep = default)
        {
            for (int i = 0; i < _eventStreamsList.Length; i++)
            {
                dep = JobHandle.CombineDependencies(dep, _eventStreamsList.ElementAt(i).Dispose(dep));
            }
            _eventStreamsList.Clear();
            return dep;
        }

        public bool NextEventStreamReader(out EventStream.Reader streamReader)
        {
            if (_eventStreamReaderIterator < _eventStreamsList.Length)
            {
                streamReader = _eventStreamsList.ElementAt(_eventStreamReaderIterator).AsReader();
                _eventStreamReaderIterator++;
                return true;
            }

            streamReader = default;
            return false;
        }
    }
}