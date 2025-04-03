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
public struct RestaurantWorkerConsiderationSet : IComponentData
{
	public BlobAssetReference<ConsiderationDefinition> CustomerLineup;
	public BlobAssetReference<ConsiderationDefinition> PendingOrders;
	public BlobAssetReference<ConsiderationDefinition> KitchenDirtiness;
	public BlobAssetReference<ConsiderationDefinition> CleaningSupplies;
	public BlobAssetReference<ConsiderationDefinition> Idleness;
}

[CreateAssetMenu(menuName = "Trove/UtilityAI/ConsiderationSets/RestaurantWorkerConsiderationSetData", fileName = "RestaurantWorkerConsiderationSetData")]
public class RestaurantWorkerConsiderationSetData : ScriptableObject
{
	[Header("Consideration Definitions")]
	public ConsiderationDefinitionAuthoring CustomerLineup  = ConsiderationDefinitionAuthoring.GetDefault(0f, 1f);
	public ConsiderationDefinitionAuthoring PendingOrders  = ConsiderationDefinitionAuthoring.GetDefault(0f, 1f);
	public ConsiderationDefinitionAuthoring KitchenDirtiness  = ConsiderationDefinitionAuthoring.GetDefault(0f, 1f);
	public ConsiderationDefinitionAuthoring CleaningSupplies  = ConsiderationDefinitionAuthoring.GetDefault(0f, 1f);
	public ConsiderationDefinitionAuthoring Idleness  = ConsiderationDefinitionAuthoring.GetDefault(0f, 1f);
	
	public void Bake(IBaker baker, out RestaurantWorkerConsiderationSet considerationSetComponent)
	{
		considerationSetComponent = new RestaurantWorkerConsiderationSet();
		considerationSetComponent.CustomerLineup = CustomerLineup.ToConsiderationDefinition(baker);
		considerationSetComponent.PendingOrders = PendingOrders.ToConsiderationDefinition(baker);
		considerationSetComponent.KitchenDirtiness = KitchenDirtiness.ToConsiderationDefinition(baker);
		considerationSetComponent.CleaningSupplies = CleaningSupplies.ToConsiderationDefinition(baker);
		considerationSetComponent.Idleness = Idleness.ToConsiderationDefinition(baker);
		baker.AddComponent(baker.GetEntity(TransformUsageFlags.None), considerationSetComponent);
	}
	
}
