using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Trove.Stats
{
    public struct StatsWorldData<TStatModifierStack>
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        public NativeList<StatChangeEvent> StatChangeEventsList { get; private set; }
        public NativeList<StatModifierHandle> ModifierTriggerEventsList { get; private set; }

        internal NativeList<StatHandle> _tmpModifierObservedStatsList;
        internal NativeList<StatObserver> _tmpStatObserversList;
        internal NativeList<StatHandle> _tmpUpdatedStatsList;
        internal NativeList<int> _tmpLastIndexesList;

        internal TStatModifierStack _modifiersStack;

        public StatsWorldData(Allocator allocator)
        {
            StatChangeEventsList = new NativeList<StatChangeEvent>(allocator);
            ModifierTriggerEventsList = new NativeList<StatModifierHandle>(allocator);

            _tmpModifierObservedStatsList = new NativeList<StatHandle>(Allocator.Persistent);
            _tmpStatObserversList = new NativeList<StatObserver>(Allocator.Persistent);
            _tmpUpdatedStatsList = new NativeList<StatHandle>(Allocator.Persistent);
            _tmpLastIndexesList = new NativeList<int>(Allocator.Persistent);

            _modifiersStack = default;
        }

        public void Dispose(JobHandle dep = default)
        {
            if (StatChangeEventsList.IsCreated)
            {
                StatChangeEventsList.Dispose(dep);
            }

            if (ModifierTriggerEventsList.IsCreated)
            {
                ModifierTriggerEventsList.Dispose(dep);
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