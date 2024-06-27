using Unity.Entities;

public struct StatsTester : IComponentData
{
    public Entity StatOwnerPrefab;

    public int ChangingAttributesCount;
    public int ChangingAttributesChildDepth;
    public int UnchangingAttributesCount;
    public bool MakeOtherStatsDependOnFirstStatOfChangingAttributes;

    public bool HasInitialized;
}
