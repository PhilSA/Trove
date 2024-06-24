using Unity.Entities;

public struct VOStressTest : IComponentData
{
    public int ChangingAttributesCount;
    public int ChangingAttributesChildDepth;
    public int UnchangingAttributesCount;

    public bool HasInitialized;
}
