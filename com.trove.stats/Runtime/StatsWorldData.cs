using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Trove.Stats
{
    public struct StatsWorldData<TStatModifierStack>
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        private NativeList<StatChangeEvent> _statChangeEventsList;
        public NativeList<StatChangeEvent> StatChangeEventsList => _statChangeEventsList;
        private NativeList<StatModifierHandle> _modifierTriggerEventsList;
        public NativeList<StatModifierHandle> ModifierTriggerEventsList => _modifierTriggerEventsList;

        internal NativeList<StatHandle> _tmpModifierObservedStatsList;
        internal NativeList<StatObserver> _tmpStatObserversList;
        internal NativeList<StatHandle> _tmpUpdatedStatsList;
        internal NativeList<int> _tmpLastIndexesList;

        internal TStatModifierStack _modifiersStack;

        public StatsWorldData(Allocator allocator)
        {
            _statChangeEventsList = new NativeList<StatChangeEvent>(allocator);
            _modifierTriggerEventsList = new NativeList<StatModifierHandle>(allocator);

            _tmpModifierObservedStatsList = new NativeList<StatHandle>(Allocator.Persistent);
            _tmpStatObserversList = new NativeList<StatObserver>(Allocator.Persistent);
            _tmpUpdatedStatsList = new NativeList<StatHandle>(Allocator.Persistent);
            _tmpLastIndexesList = new NativeList<int>(Allocator.Persistent);

            _modifiersStack = default;
        }

        public void Dispose(JobHandle dep = default)
        {
            if (_statChangeEventsList.IsCreated)
            {
                _statChangeEventsList.Dispose(dep);
            }

            if (_modifierTriggerEventsList.IsCreated)
            {
                _modifierTriggerEventsList.Dispose(dep);
            }

            if (_tmpModifierObservedStatsList.IsCreated)
            {
                _tmpModifierObservedStatsList.Dispose(dep);
            }

            if (_tmpStatObserversList.IsCreated)
            {
                _tmpStatObserversList.Dispose(dep);
            }

            if (_tmpUpdatedStatsList.IsCreated)
            {
                _tmpUpdatedStatsList.Dispose(dep);
            }

            if (_tmpLastIndexesList.IsCreated)
            {
                _tmpLastIndexesList.Dispose(dep);
            }
        }

        public void SetStatModifiersStack(in TStatModifierStack stack)
        {
            _modifiersStack = stack;
        }
    }
}