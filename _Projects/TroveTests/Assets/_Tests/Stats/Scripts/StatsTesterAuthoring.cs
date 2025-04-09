using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

class StatsTesterAuthoring : MonoBehaviour
{
    public GameObject StatOwnerPrefab;

    public int ChangingAttributesCount;
    public int ChangingAttributesChildDepth;
    public int UnchangingAttributesCount;
    public bool MakeLocalStatsDependOnEachOther;
    
    public int SimpleAddModifiersAdded;

    class Baker : Baker<StatsTesterAuthoring>
    {
        public override void Bake(StatsTesterAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.None);
            AddComponent(entity, new StatsTester
            {
                StatOwnerPrefab = GetEntity(authoring.StatOwnerPrefab, TransformUsageFlags.None),

                ChangingAttributesCount = authoring.ChangingAttributesCount,
                ChangingAttributesChildDepth = authoring.ChangingAttributesChildDepth,
                UnchangingAttributesCount = authoring.UnchangingAttributesCount,
                MakeLocalStatsDependOnEachOther = authoring.MakeLocalStatsDependOnEachOther,
                
                SimpleAddModifiersAdded = authoring.SimpleAddModifiersAdded,
            });
        }
    }
}
