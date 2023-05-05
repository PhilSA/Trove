using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using System;

[BurstCompile]
public partial struct RestaurantSystem : ISystem
{
    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        RestaurantSystemJob job = new RestaurantSystemJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
        };
        state.Dependency = job.Schedule(state.Dependency);
    }

    [BurstCompile]
    public partial struct RestaurantSystemJob : IJobEntity
    {
        public float DeltaTime;

        void Execute(in Restaurant restaurant, ref RestaurantState state)
        {
            // Increment customers in line
            state.CustomersCounter += restaurant.NewCustomersSpeed * DeltaTime;
            while (state.CustomersCounter >= 1f)
            {
                state.CustomersInLine++;
                state.CustomersCounter -= 1f;
            }
        }
    }
}