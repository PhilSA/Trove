using Unity.Entities;

namespace Trove.Stats
{
    // ------------------------------------------
    // Here you can tweak internal buffer capacities of various Stats buffer elements.
    // ------------------------------------------
    
    [InternalBufferCapacity(8)]
    public partial struct Stat
    { }
    
    [InternalBufferCapacity(8)]
    public partial struct StatModifier<TStatModifier, TStatModifierStack>
        where TStatModifier : unmanaged, IStatsModifier<TStatModifierStack>
        where TStatModifierStack : unmanaged, IStatsModifierStack
    { }
    
    [InternalBufferCapacity(8)]
    public partial struct StatObserver
    { }
}