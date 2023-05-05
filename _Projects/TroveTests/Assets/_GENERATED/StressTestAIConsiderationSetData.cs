using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using System;
using System.Collections.Generic;
using Trove;
using Trove.UtilityAI;
using Action = Trove.UtilityAI.Action;

[Serializable]
public struct StressTestAIConsiderationSet : IComponentData
{
	public BlobAssetReference<ConsiderationDefinition> C0;
	public BlobAssetReference<ConsiderationDefinition> C1;
	public BlobAssetReference<ConsiderationDefinition> C2;
	public BlobAssetReference<ConsiderationDefinition> C3;
	public BlobAssetReference<ConsiderationDefinition> C4;
}

[CreateAssetMenu(menuName = "Trove/UtilityAI/ConsiderationSets/StressTestAIConsiderationSetData", fileName = "StressTestAIConsiderationSetData")]
public class StressTestAIConsiderationSetData : ScriptableObject
{
	[Header("Consideration Definitions")]
	public ConsiderationDefinitionAuthoring C0  = ConsiderationDefinitionAuthoring.GetDefault(0f, 1f);
	public ConsiderationDefinitionAuthoring C1  = ConsiderationDefinitionAuthoring.GetDefault(0f, 1f);
	public ConsiderationDefinitionAuthoring C2  = ConsiderationDefinitionAuthoring.GetDefault(0f, 1f);
	public ConsiderationDefinitionAuthoring C3  = ConsiderationDefinitionAuthoring.GetDefault(0f, 1f);
	public ConsiderationDefinitionAuthoring C4  = ConsiderationDefinitionAuthoring.GetDefault(0f, 1f);
	
	public void Bake(IBaker baker, out StressTestAIConsiderationSet considerationSetComponent)
	{
		considerationSetComponent = new StressTestAIConsiderationSet();
		considerationSetComponent.C0 = C0.ToConsiderationDefinition(baker);
		considerationSetComponent.C1 = C1.ToConsiderationDefinition(baker);
		considerationSetComponent.C2 = C2.ToConsiderationDefinition(baker);
		considerationSetComponent.C3 = C3.ToConsiderationDefinition(baker);
		considerationSetComponent.C4 = C4.ToConsiderationDefinition(baker);
		baker.AddComponent(baker.GetEntity(TransformUsageFlags.None), considerationSetComponent);
	}
	
}
