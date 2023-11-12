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

    public unsafe struct EventStreamManager
    {
        private NativeList<EventStream> _eventCollections;
        private Allocator _allocator;
        private int _eventStreamReaderIterator;


        public EventStreamManager(ref SystemState state, int initialCapacity = 16)
        {
            _eventCollections = new NativeList<EventStream>(initialCapacity, Allocator.Persistent);
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
    }
}