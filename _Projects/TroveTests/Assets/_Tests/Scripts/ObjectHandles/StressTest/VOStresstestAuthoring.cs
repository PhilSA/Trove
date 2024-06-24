using Unity.Entities;
using UnityEngine;

class VOStresstestAuthoring : MonoBehaviour
{
    public bool UseOldSystem;
    public int ChangingAttributesCount = 10000;
    public int ChangingAttributesChildDepth = 2;
    public int UnchangingAttributesCount = 0;

    class Baker : Baker<VOStresstestAuthoring>
    {
        public override void Bake(VOStresstestAuthoring authoring)
        {
            if (authoring.UseOldSystem)
            {
                AddComponent(GetEntity(TransformUsageFlags.None), new AttributesTester
                {
                    ChangingAttributesCount = authoring.ChangingAttributesCount,
                    ChangingAttributesChildDepth = authoring.ChangingAttributesChildDepth,
                    UnchangingAttributesCount = authoring.UnchangingAttributesCount,
                });
            }
            else
            {
                AddComponent(GetEntity(TransformUsageFlags.None), new VOStressTest
                {
                    ChangingAttributesCount = authoring.ChangingAttributesCount,
                    ChangingAttributesChildDepth = authoring.ChangingAttributesChildDepth,
                    UnchangingAttributesCount = authoring.UnchangingAttributesCount,
                });
            }
        }
    }
}
