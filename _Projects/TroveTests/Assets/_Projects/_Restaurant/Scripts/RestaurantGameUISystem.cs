using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using System;

public partial class RestaurantGameUISystem : SystemBase
{
    public RestaurantGameUI GameUI;

    protected override void OnUpdate()
    {
        EntityManager.CompleteAllTrackedJobs();
        
        if(GameUI != null)
        {
            // Restaurant info
            if(SystemAPI.HasSingleton<RestaurantState>())
            {
                RestaurantState restaurantState = SystemAPI.GetSingleton<RestaurantState>();
                GameUI.Label_CustomersInLine.text = restaurantState.CustomersInLine.ToString();
                GameUI.Label_PendingOrders.text = restaurantState.PendingOrdersCount.ToString();
                GameUI.Label_KitchenDirtiness.text = restaurantState.KitchenDirtiness.ToString();
                GameUI.Label_CleaningSupplies.text = restaurantState.AvailableCleaningSupplies.ToString();
                GameUI.Label_CompletedOrders.text = restaurantState.CompletedOrders.ToString();
            }

            // Worker states
            {
                GameUI.IdleBackingList.Clear();
                GameUI.ServiceBackingList.Clear();
                GameUI.CookBackingList.Clear();
                GameUI.CleanBackingList.Clear();

                EntityQuery workersQuery = SystemAPI.QueryBuilder().WithAll<RestaurantWorkerAI>().Build();
                NativeArray<Entity> workerEntities = workersQuery.ToEntityArray(Allocator.Temp);
                NativeArray<RestaurantWorkerAI> workerAIs = workersQuery.ToComponentDataArray<RestaurantWorkerAI>(Allocator.Temp);

                for (int i = 0; i < workerAIs.Length; i++)
                {
                    string workerName = "Worker " + workerEntities[i].Index;
                    switch (workerAIs[i].SelectedAction)
                    {
                        case RestaurantWorkerAIAction.Idle:
                            GameUI.IdleBackingList.Add(workerName);
                            break;
                        case RestaurantWorkerAIAction.Service:
                            GameUI.ServiceBackingList.Add(workerName);
                            break;
                        case RestaurantWorkerAIAction.Cook:
                            GameUI.CookBackingList.Add(workerName);
                            break;
                        case RestaurantWorkerAIAction.Clean:
                            GameUI.CleanBackingList.Add(workerName);
                            break;
                    }
                }

                workerEntities.Dispose();
                workerAIs.Dispose();

                GameUI.List_IdleWorkers.Rebuild();
                GameUI.List_IdleWorkers.MarkDirtyRepaint();
                GameUI.List_ServiceWorkers.Rebuild();
                GameUI.List_ServiceWorkers.MarkDirtyRepaint();
                GameUI.List_CookWorkers.Rebuild();
                GameUI.List_CookWorkers.MarkDirtyRepaint();
                GameUI.List_CleanWorkers.Rebuild();
                GameUI.List_CleanWorkers.MarkDirtyRepaint();
            }
        }
    }
}