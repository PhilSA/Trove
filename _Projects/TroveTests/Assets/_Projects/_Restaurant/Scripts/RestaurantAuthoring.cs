using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class RestaurantAuthoring : MonoBehaviour
{
    public Restaurant RestaurantParameters;

    class Baker : Baker<RestaurantAuthoring>
    {
        public override void Bake(RestaurantAuthoring authoring)
        {
            AddComponent(GetEntity(TransformUsageFlags.None), authoring.RestaurantParameters);
            AddComponent(GetEntity(TransformUsageFlags.None), new RestaurantState
            {
                PendingOrdersCount = 0,
                CustomersInLine = 0,
                KitchenDirtiness = 0f,
                AvailableCleaningSupplies = authoring.RestaurantParameters.CleaningSuppliesCount,
            });
        }
    }
}