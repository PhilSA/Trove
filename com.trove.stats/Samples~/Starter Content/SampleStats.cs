using Trove.Stats;
using Unity.Entities;

public struct SampleStats : IComponentData
{
    public StatHandle Strength;
    public StatHandle Intelligence;
    public StatHandle Dexterity;
}
