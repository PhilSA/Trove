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
    public partial struct StatObserver
    { }
}