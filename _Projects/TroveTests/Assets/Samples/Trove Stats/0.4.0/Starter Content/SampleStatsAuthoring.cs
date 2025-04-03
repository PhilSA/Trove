using Trove.Stats;
using Unity.Entities;
using UnityEngine;

class SampleStatsAuthoring : MonoBehaviour
{
    public float Strength = 10f;
    public float Intelligence = 10f;
    public float Dexterity = 10f;
    
    class SampleStatsAuthoringBaker : Baker<SampleStatsAuthoring>
    {
        public override void Bake(SampleStatsAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.None);

            SampleStats sampleStats = new SampleStats();
            
            // Bake the stats with a starting value, and store their StatHandle
            StatsUtilities.BakeStatsComponents(this, entity, out StatsBaker<SampleStatModifier, SampleStatModifier.Stack> statsBaker);
            statsBaker.CreateStat(authoring.Strength, false, out sampleStats.Strength);
            statsBaker.CreateStat(authoring.Intelligence, false, out sampleStats.Intelligence);
            statsBaker.CreateStat(authoring.Dexterity, false, out sampleStats.Dexterity);
            
            // Add the component storing StatHandles
            AddComponent(entity, sampleStats);
        }
    }
}
