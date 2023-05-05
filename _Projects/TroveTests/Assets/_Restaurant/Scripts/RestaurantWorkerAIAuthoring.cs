using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Trove.UtilityAI;

[DisallowMultipleComponent]
public class RestaurantWorkerAIAuthoring : MonoBehaviour
{
    // With this field, we can assign the consideration set (the curves of each consideration) we 
    // defined in the inspector. 
    public RestaurantWorkerConsiderationSetData ConsiderationSetData;

    // Fields to determine the characteristics of this worker
    public float ServiceSpeed = 1f;
    public float CookingSpeed = 1f;
    public float CleaningSpeed = 1f;
    public float KitchenDirtyingSpeedWhenCooking = 1f;
    public float DecisionInertia = 0.5f;
    public int CustomerLineupConsiderationCeiling = 5;
    public int PendingOrdersConsiderationCeiling = 5;
    public float KitchenDirtinessConsiderationCeiling = 1f;

    class Baker : Baker<RestaurantWorkerAIAuthoring>
    {
        public override void Bake(RestaurantWorkerAIAuthoring authoring)
        {
            // Create our restaurant worker component, but don't add it just yet (because we must set data in it first)
            RestaurantWorkerAI restaurantWorker = new RestaurantWorkerAI();
            restaurantWorker.ServiceSpeed = authoring.ServiceSpeed;
            restaurantWorker.CookingSpeed = authoring.CookingSpeed;
            restaurantWorker.CleaningSpeed = authoring.CleaningSpeed;
            restaurantWorker.KitchenDirtyingSpeedWhenCooking = authoring.KitchenDirtyingSpeedWhenCooking;
            restaurantWorker.DecisionInertia = authoring.DecisionInertia;
            restaurantWorker.CustomerLineupConsiderationCeiling = authoring.CustomerLineupConsiderationCeiling;
            restaurantWorker.PendingOrdersConsiderationCeiling = authoring.PendingOrdersConsiderationCeiling;
            restaurantWorker.KitchenDirtinessConsiderationCeiling = authoring.KitchenDirtinessConsiderationCeiling;
            restaurantWorker.ShouldUpdateReasoner = true;

            // We bake our consideration set definitions to the entity (these are blob asset references to each consideration curve)
            authoring.ConsiderationSetData.Bake(this, out RestaurantWorkerConsiderationSet considerationSetComponent);

            // When we're ready to start adding actions and considerations, we call BeginBakeReasoner. This will give us
            // access to the components and buffers we need.
            ReasonerUtilities.BeginBakeReasoner(this, out Reasoner reasoner, out DynamicBuffer<Action> actionsBuffer, out DynamicBuffer<Consideration> considerationsBuffer, out DynamicBuffer<ConsiderationInput> considerationInputsBuffer);
            {
                // Add our actions. We specify an action type using our enum, and we store the resulting
                // "ActionReference" in our restaurant worker component.
                ReasonerUtilities.AddAction(new ActionDefinition((int)RestaurantWorkerAIAction.Idle), true, ref reasoner, actionsBuffer, out restaurantWorker.IdleRef);
                ReasonerUtilities.AddAction(new ActionDefinition((int)RestaurantWorkerAIAction.Service), true, ref reasoner, actionsBuffer, out restaurantWorker.ServiceRef);
                ReasonerUtilities.AddAction(new ActionDefinition((int)RestaurantWorkerAIAction.Cook), true, ref reasoner, actionsBuffer, out restaurantWorker.CookRef);
                ReasonerUtilities.AddAction(new ActionDefinition((int)RestaurantWorkerAIAction.Clean), true, ref reasoner, actionsBuffer, out restaurantWorker.CleanRef);

                // Add our considerations to our actions. We use the "ConsiderationDefinition"s from our consideration set
                // in order to specify the type of consideration to add, and we also specify the "ActionReference" that we
                // want to add this consideration to. Finally, we store the resulting "ConsiderationReference" in our 
                // restaurant worker component.
                ReasonerUtilities.AddConsideration(considerationSetComponent.Idleness, ref restaurantWorker.IdleRef, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out restaurantWorker.IdlenessRef);
                ReasonerUtilities.AddConsideration(considerationSetComponent.CustomerLineup, ref restaurantWorker.ServiceRef, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out restaurantWorker.CustomerLineupRef);
                ReasonerUtilities.AddConsideration(considerationSetComponent.PendingOrders, ref restaurantWorker.CookRef, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out restaurantWorker.PendingOrdersRef);
                ReasonerUtilities.AddConsideration(considerationSetComponent.KitchenDirtiness, ref restaurantWorker.CleanRef, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out restaurantWorker.KitchenDirtinessRef);
                ReasonerUtilities.AddConsideration(considerationSetComponent.CleaningSupplies, ref restaurantWorker.CleanRef, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out restaurantWorker.CleaningSuppliesRef);
            } 
            // Once we're finished setting everything up, we end baking for the reasoner
            ReasonerUtilities.EndBakeReasoner(this, reasoner);

            // Add our worker ai component, after it's been filled with all added action/consideration references
            AddComponent(GetEntity(TransformUsageFlags.None), restaurantWorker);

            // Let the baking system know that we depend on that consideration set SriptableObject, so that
            // baking is properly re-triggered when it changes.
            DependsOn(authoring.ConsiderationSetData);
        }
    }
}