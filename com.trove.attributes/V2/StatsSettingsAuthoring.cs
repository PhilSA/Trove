using Unity.Entities;
using UnityEngine;

namespace Trove.Stats
{
    class StatsSettingsAuthoring : MonoBehaviour
    {
        public int BatchRecomputeUpdatesCount = 1;
        public bool EndWithRecomputeImmediate = true;

        class Baker : Baker<StatsSettingsAuthoring>
        {
            public override void Bake(StatsSettingsAuthoring authoring)
            {
                Entity entity = GetEntity(authoring, TransformUsageFlags.None);
                AddComponent(entity, new StatsSettings
                {
                    BatchRecomputeUpdatesCount = authoring.BatchRecomputeUpdatesCount,
                    EndWithRecomputeImmediate = authoring.EndWithRecomputeImmediate,
                });
            }
        }
    }
}