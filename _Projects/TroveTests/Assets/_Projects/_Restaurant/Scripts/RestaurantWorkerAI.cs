using Unity.Entities;
using Unity.Mathematics;
using System;
using Trove.UtilityAI;

public enum RestaurantWorkerAIAction
{
    Idle,
    Service,
    Cook,
    Clean,
}

[Serializable]
public struct RestaurantWorkerAI : IComponentData
{
    // Store the selected action, so we can see it in the inspector for debugging
    public RestaurantWorkerAIAction SelectedAction;

    // Store references to our action instances
    public ActionReference IdleRef;
    public ActionReference ServiceRef;
    public ActionReference CookRef;
    public ActionReference CleanRef;

    // Store references to our consideration instances
    public ConsiderationReference IdlenessRef;
    public ConsiderationReference CustomerLineupRef;
    public ConsiderationReference PendingOrdersRef;
    public ConsiderationReference KitchenDirtinessRef;
    public ConsiderationReference CleaningSuppliesRef;

    // Characteristics of this worker
    public float ServiceSpeed;
    public float CookingSpeed;
    public float CleaningSpeed;
    public float KitchenDirtyingSpeedWhenCooking;
    public float DecisionInertia;
    public int CustomerLineupConsiderationCeiling;
    public int PendingOrdersConsiderationCeiling;
    public float KitchenDirtinessConsiderationCeiling;

    // State of this worker
    public float TimeSinceMadeDecision;
    public bool ShouldUpdateReasoner;
    public float ServiceProgress;
    public float CookingProgress;
    public bool IsDealingWithCustomer;
    public bool IsCookingOrder;
    public bool HasCleaningSupplies;
}