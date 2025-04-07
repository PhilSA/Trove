using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Trove.Stats
{
    public struct StatsWorldData<TStatModifier, TStatModifierStack>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
        where TStatModifierStack : unmanaged, IStatsModifierStack
    {
        private NativeList<StatChangeEvent> _statChangeEventsList;
        public NativeList<StatChangeEvent> StatChangeEventsList => _statChangeEventsList;
        private NativeList<ModifierTriggerEvent<TStatModifier, TStatModifierStack>> _modifierTriggerEventsList;
        public NativeList<ModifierTriggerEvent<TStatModifier, TStatModifierStack>> ModifierTriggerEventsList => _modifierTriggerEventsList;

        internal NativeList<StatHandle> _tmpModifierObservedStatsList;
        internal NativeList<StatObserver> _tmpStatObserversList;
        internal NativeList<StatHandle> _tmpGlobalUpdatedStatsList;
        internal NativeList<StatHandle> _tmpSameEntityUpdatedStatsList;
        internal NativeList<int> _tmpLastIndexesList;

        internal TStatModifierStack _modifiersStack;

        public StatsWorldData(Allocator allocator)
        {
            _statChangeEventsList = new NativeList<StatChangeEvent>(allocator);
            _modifierTriggerEventsList = new NativeList<ModifierTriggerEvent<TStatModifier, TStatModifierStack>>(allocator);

            _tmpModifierObservedStatsList = new NativeList<StatHandle>(Allocator.Persistent);
            _tmpStatObserversList = new NativeList<StatObserver>(Allocator.Persistent);
            _tmpGlobalUpdatedStatsList = new NativeList<StatHandle>(Allocator.Persistent);
            _tmpSameEntityUpdatedStatsList = new NativeList<StatHandle>(Allocator.Persistent);
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

            if (_tmpGlobalUpdatedStatsList.IsCreated)
            {
                _tmpGlobalUpdatedStatsList.Dispose(dep);
            }

            if (_tmpSameEntityUpdatedStatsList.IsCreated)
            {
                _tmpSameEntityUpdatedStatsList.Dispose(dep);
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