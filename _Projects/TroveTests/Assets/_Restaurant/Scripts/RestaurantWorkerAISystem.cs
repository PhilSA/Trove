using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Trove.UtilityAI;

[BurstCompile]
public partial struct RestaurantWorkerAISystem : ISystem
{
    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        if (SystemAPI.TryGetSingletonEntity<Restaurant>(out Entity restaurantSingleton))
        {
            RestaurantWorkerAIUpdateJob updateJob = new RestaurantWorkerAIUpdateJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                RestaurantSingleton = restaurantSingleton,
                RestaurantLookup = SystemAPI.GetComponentLookup<Restaurant>(true),
                RestaurantStateLookup = SystemAPI.GetComponentLookup<RestaurantState>(false),
            };
            state.Dependency = updateJob.Schedule(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct RestaurantWorkerAIUpdateJob : IJobEntity
    {
        public float DeltaTime;
        public Entity RestaurantSingleton;
        [ReadOnly]
        public ComponentLookup<Restaurant> RestaurantLookup;
        public ComponentLookup<RestaurantState> RestaurantStateLookup;

        void Execute(Entity entity, ref RestaurantWorkerAI restaurantWorker, ref Reasoner reasoner, ref DynamicBuffer<Action> actionsBuffer, ref DynamicBuffer<Consideration> considerationsBuffer, ref DynamicBuffer<ConsiderationInput> considerationInputsBuffer)
        {
            // Get restaurant data
            Restaurant restaurant = RestaurantLookup[RestaurantSingleton];
            RestaurantState restaurantState = RestaurantStateLookup[RestaurantSingleton];

            if (restaurantWorker.ShouldUpdateReasoner && restaurantWorker.TimeSinceMadeDecision > restaurantWorker.DecisionInertia)
            {
                // Set our consideration inputs based on restaurant state and worker characteristics
                ReasonerUtilities.SetConsiderationInput(ref restaurantWorker.CustomerLineupRef, math.saturate((float)restaurantState.CustomersInLine / (float)restaurantWorker.CustomerLineupConsiderationCeiling), in reasoner, considerationsBuffer, considerationInputsBuffer);
                ReasonerUtilities.SetConsiderationInput(ref restaurantWorker.PendingOrdersRef, math.saturate((float)restaurantState.PendingOrdersCount / (float)restaurantWorker.PendingOrdersConsiderationCeiling), in reasoner, considerationsBuffer, considerationInputsBuffer);
                ReasonerUtilities.SetConsiderationInput(ref restaurantWorker.KitchenDirtinessRef, math.saturate((float)restaurantState.KitchenDirtiness / restaurantWorker.KitchenDirtinessConsiderationCeiling), in reasoner, considerationsBuffer, considerationInputsBuffer);
                ReasonerUtilities.SetConsiderationInput(ref restaurantWorker.CleaningSuppliesRef, (restaurantState.AvailableCleaningSupplies > 0 ? 1f : 0f), in reasoner, considerationsBuffer, considerationInputsBuffer);

                // Create an action selector (determines how we pick the "best" action). There are various types of
                // selectors to choose from, and you can also create your own
                ActionSelectors.HighestScoring actionSelector = new ActionSelectors.HighestScoring();

                // Update the AI and select an action
                if (ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelector, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out Action selectedAction))
                {
                    // Handle switching actions
                    if (selectedAction.Score > 0f) // Don't bother switching actions if the new one scored 0
                    {
                        RestaurantWorkerAIAction previousAction = restaurantWorker.SelectedAction;
                        restaurantWorker.SelectedAction = (RestaurantWorkerAIAction)selectedAction.Type;
                        restaurantWorker.ShouldUpdateReasoner = false;
                        restaurantWorker.TimeSinceMadeDecision = 0f;

                        // What happens when an action is ended
                        switch (previousAction)
                        {
                            case RestaurantWorkerAIAction.Clean:
                                restaurantWorker.HasCleaningSupplies = false;
                                restaurantState.AvailableCleaningSupplies++;
                                break;
                        }

                        // What happens when a new action is started
                        switch (restaurantWorker.SelectedAction)
                        {
                            case RestaurantWorkerAIAction.Service:
                                restaurantWorker.ServiceProgress = 0f;
                                restaurantWorker.IsDealingWithCustomer = true;
                                restaurantState.CustomersInLine--;
                                break;
                            case RestaurantWorkerAIAction.Cook:
                                restaurantWorker.CookingProgress = 0f;
                                restaurantWorker.IsCookingOrder = true;
                                restaurantState.PendingOrdersCount--;
                                break;
                            case RestaurantWorkerAIAction.Clean:
                                restaurantWorker.HasCleaningSupplies = true;
                                restaurantState.AvailableCleaningSupplies--;
                                break;
                        }
                    }
                }
            }

            // Handle our AI update
            {
                switch (restaurantWorker.SelectedAction)
                {
                    case RestaurantWorkerAIAction.Idle:
                        restaurantWorker.ShouldUpdateReasoner = true;
                        break;
                    case RestaurantWorkerAIAction.Service:
                        // Handle what happens when we must do customer service
                        if (restaurantWorker.IsDealingWithCustomer)
                        {
                            restaurantWorker.ServiceProgress += restaurantWorker.ServiceSpeed * DeltaTime;
                            if (restaurantWorker.ServiceProgress >= restaurant.ReferenceTimeToTakeCustomerOrder)
                            {
                                // A customer has been served and a pending order is created
                                restaurantState.PendingOrdersCount += 1;
                                restaurantWorker.ShouldUpdateReasoner = true;
                                restaurantWorker.IsDealingWithCustomer = false;
                            }
                        }
                        else
                        {
                            restaurantWorker.ShouldUpdateReasoner = true;
                        }
                        break;
                    case RestaurantWorkerAIAction.Cook:
                        // Handle what happens when we must cook
                        if (restaurantWorker.IsCookingOrder)
                        {
                            restaurantWorker.CookingProgress += restaurantWorker.CookingSpeed * DeltaTime;

                            // Cooking creates dirtiness in the kitchen
                            restaurantState.KitchenDirtiness += restaurantWorker.KitchenDirtyingSpeedWhenCooking * DeltaTime;

                            if (restaurantWorker.CookingProgress >= restaurant.ReferenceTimeToCookOrder)
                            {
                                // A pending order has beed fullfilled
                                restaurantState.CompletedOrders++;
                                restaurantWorker.ShouldUpdateReasoner = true;
                                restaurantWorker.IsCookingOrder = false;
                            }
                        }
                        else
                        {
                            restaurantWorker.ShouldUpdateReasoner = true;
                        }
                        break;
                    case RestaurantWorkerAIAction.Clean:
                        // Handle what happens when we must clean
                        if (restaurantState.KitchenDirtiness > 0f && restaurantWorker.HasCleaningSupplies)
                        {
                            restaurantState.KitchenDirtiness -= restaurantWorker.CleaningSpeed * DeltaTime;
                            restaurantState.KitchenDirtiness = math.clamp(restaurantState.KitchenDirtiness, 0f, float.MaxValue);
                        }
                        // Cleaning is not as much of a priority as the other tasks, so we always update the reasoner in that state
                        // after at least some time has elapsed since we've started cleaning
                        restaurantWorker.ShouldUpdateReasoner = true;
                        break;
                }

                restaurantWorker.TimeSinceMadeDecision += DeltaTime;
            }

            // Write changes to the restaurant state
            RestaurantStateLookup[RestaurantSingleton] = restaurantState;
        }
    }
}