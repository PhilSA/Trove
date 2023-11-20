using System.Collections;
using System.Collections.Generic;
using Trove.PolymorphicElements;
using Unity.Entities;
using Unity.Rendering;
using Unity.Mathematics;
using UnityEngine;

public class StateMachineAuthoring : MonoBehaviour
{
    class Baker : Baker<StateMachineAuthoring>
    {
        public override void Bake(StateMachineAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

            AddComponent(entity, new MyStateMachine
            {
                Speed = 1f,
                StartStateIndex = 0,
                CurrentStateIndex = -1,
                CurrentStateByteStartIndex = -1,
                PreviousStateIndex = -1,
            });
            DynamicBuffer<byte> stateElements = AddBuffer<StateElement>(entity).Reinterpret<byte>();
            DynamicBuffer<PolymorphicElementMetaData> stateMetaDatas = AddBuffer<StateMetaData>(entity).Reinterpret<PolymorphicElementMetaData>();

            // Write states
            {
                int moveStateIndex = stateMetaDatas.Length;
                PolymorphicElementsUtility.AddElementGetMetaData(ref stateElements, new MoveState
                {
                    TimedState = new TimedState(2f),
                    Movement = math.forward(),
                }, out PolymorphicElementMetaData metaData);
                stateMetaDatas.Add(metaData);

                int rotateStateIndex = stateMetaDatas.Length;
                PolymorphicElementsUtility.AddElementGetMetaData(ref stateElements, new RotateState
                {
                    TimedState = new TimedState(2f),
                    RotationSpeed = new float3(1f),
                }, out metaData);
                stateMetaDatas.Add(metaData);

                int scaleStateIndex = stateMetaDatas.Length;
                PolymorphicElementsUtility.AddElementGetMetaData(ref stateElements, new ScaleState
                {
                    TimedState = new TimedState(2f),
                    AddedScale = 3f,
                }, out metaData);
                stateMetaDatas.Add(metaData);

                int redStateIndex = stateMetaDatas.Length;
                PolymorphicElementsUtility.AddElementGetMetaData(ref stateElements, new ColorState
                {
                    TimedState = new TimedState(0.25f),
                    Color = new float4(100f, 0f, 0, 1f),
                }, out metaData);
                stateMetaDatas.Add(metaData);

                int greenStateIndex = stateMetaDatas.Length;
                PolymorphicElementsUtility.AddElementGetMetaData(ref stateElements, new ColorState
                {
                    TimedState = new TimedState(0.25f),
                    Color = new float4(0f, 100f, 0, 1f),
                }, out metaData);
                stateMetaDatas.Add(metaData);

                int blueStateIndex = stateMetaDatas.Length;
                PolymorphicElementsUtility.AddElementGetMetaData(ref stateElements, new ColorState
                {
                    TimedState = new TimedState(0.25f),
                    Color = new float4(0f, 0f, 100, 1f),
                }, out metaData);
                stateMetaDatas.Add(metaData);

                // Modify state data after adding them
                {
                    // Store next states
                    if (PolymorphicElementsUtility.ReadElementValue(ref stateElements, stateMetaDatas[moveStateIndex].StartByteIndex, out _, out MoveState moveState))
                    {
                        moveState.NextStateIndex = rotateStateIndex;
                        PolymorphicElementsUtility.WriteElementValueNoResize(ref stateElements, stateMetaDatas[moveStateIndex].StartByteIndex, moveState);
                    }
                    if (PolymorphicElementsUtility.ReadElementValue(ref stateElements, stateMetaDatas[rotateStateIndex].StartByteIndex, out _, out RotateState rotateState))
                    {
                        rotateState.NextStateIndex = scaleStateIndex;
                        PolymorphicElementsUtility.WriteElementValueNoResize(ref stateElements, stateMetaDatas[rotateStateIndex].StartByteIndex, rotateState);
                    }
                    if (PolymorphicElementsUtility.ReadElementValue(ref stateElements, stateMetaDatas[scaleStateIndex].StartByteIndex, out _, out ScaleState scaleState))
                    {
                        scaleState.NextStateIndex = moveStateIndex;

                        // Setup substatemachine
                        scaleState.SubStateMachine = new MyStateMachine
                        {
                            Speed = 1f,
                            StartStateIndex = redStateIndex,
                            CurrentStateIndex = -1,
                            CurrentStateByteStartIndex = -1,
                            PreviousStateIndex = -1,
                        };

                        PolymorphicElementsUtility.WriteElementValueNoResize(ref stateElements, stateMetaDatas[scaleStateIndex].StartByteIndex, scaleState);
                    }
                    if (PolymorphicElementsUtility.ReadElementValue(ref stateElements, stateMetaDatas[redStateIndex].StartByteIndex, out _, out ColorState redState))
                    {
                        redState.NextStateIndex = greenStateIndex;
                        PolymorphicElementsUtility.WriteElementValueNoResize(ref stateElements, stateMetaDatas[redStateIndex].StartByteIndex, redState);
                    }
                    if (PolymorphicElementsUtility.ReadElementValue(ref stateElements, stateMetaDatas[greenStateIndex].StartByteIndex, out _, out ColorState greenState))
                    {
                        greenState.NextStateIndex = blueStateIndex;
                        PolymorphicElementsUtility.WriteElementValueNoResize(ref stateElements, stateMetaDatas[greenStateIndex].StartByteIndex, greenState);
                    }
                    if (PolymorphicElementsUtility.ReadElementValue(ref stateElements, stateMetaDatas[blueStateIndex].StartByteIndex, out _, out ColorState blueState))
                    {
                        blueState.NextStateIndex = redStateIndex;
                        PolymorphicElementsUtility.WriteElementValueNoResize(ref stateElements, stateMetaDatas[blueStateIndex].StartByteIndex, blueState);
                    }
                }
            }
        }
    }
}
