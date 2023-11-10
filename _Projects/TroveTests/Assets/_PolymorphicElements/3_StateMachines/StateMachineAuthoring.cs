using System.Collections;
using System.Collections.Generic;
using Trove.PolymorphicElements;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class StateMachineAuthoring : MonoBehaviour
{
    class Baker : Baker<StateMachineAuthoring>
    {
        public override void Bake(StateMachineAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

            MyStateMachine sm = new MyStateMachine
            {
                CurrentStateIndex = -1,
                PreviousStateIndex = -1,
                Speed = 1f,
            };

            DynamicBuffer<byte> stateElements = AddBuffer<StateElement>(entity).Reinterpret<byte>();

            // Write states
            { 
                ref MoveState moveState = ref IStateManager.AddElement(ref stateElements, new MoveState
                {
                    Duration = 2f,
                    Movement = math.forward(),
                }, out sm.MoveStateData);

                ref RotateState rotateState = ref IStateManager.AddElement(ref stateElements, new RotateState
                {
                    Duration = 2f,
                    RotationSpeed = new float3(1f),
                }, out sm.RotateStateData);

                ref ScaleState scaleState = ref IStateManager.AddElement(ref stateElements, new ScaleState
                {
                    Duration = 2f,
                    AddedScale = 3f,
                }, out sm.ScaleStateData);

                // Modify state data after adding them
                moveState.NextStateStartIndex = sm.RotateStateData.StartByteIndex;
                rotateState.NextStateStartIndex = sm.ScaleStateData.StartByteIndex;
                scaleState.NextStateStartIndex = sm.MoveStateData.StartByteIndex;

                AddComponent(entity, sm);
            }
        }
    }
}
