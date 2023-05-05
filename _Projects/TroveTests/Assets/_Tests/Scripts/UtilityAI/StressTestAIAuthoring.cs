using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Trove.UtilityAI;

public class StressTestAIAuthoring : MonoBehaviour
{
    public bool actionsEnabled = false;
    public bool considerationsEnabled = false;
    public int UpdateEveryXTick = 1;
    public StressTestAIConsiderationSetData StressTestAIConsiderationSet;

    class Baker : Baker<StressTestAIAuthoring>
    {
        public override void Bake(StressTestAIAuthoring authoring)
        {
            StressTestAI stressTest = new StressTestAI
            {
                UpdateEveryXTick = authoring.UpdateEveryXTick,
                Random = Unity.Mathematics.Random.CreateFromIndex(0),
            };

            if (authoring.StressTestAIConsiderationSet != null)
            {
                authoring.StressTestAIConsiderationSet.Bake(this, out StressTestAIConsiderationSet considerationSetComponent);

                DependsOn(authoring.StressTestAIConsiderationSet);
                ActionReference actionRef;

                ReasonerUtilities.BeginBakeReasoner(this, out Reasoner reasoner, out DynamicBuffer<Action> actionsBuffer, out DynamicBuffer<Consideration> considerationsBuffer, out DynamicBuffer<ConsiderationInput> considerationInputsBuffer);
                {
                    ReasonerUtilities.AddAction(new ActionDefinition((int)StressTestAIAction.A0), authoring.actionsEnabled, ref reasoner, actionsBuffer, out actionRef);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C0, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A0C0Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C1, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A0C1Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C2, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A0C2Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C3, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A0C3Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C4, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A0C4Ref);

                    ReasonerUtilities.AddAction(new ActionDefinition((int)StressTestAIAction.A1), authoring.actionsEnabled, ref reasoner, actionsBuffer, out actionRef);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C0, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A1C0Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C1, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A1C1Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C2, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A1C2Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C3, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A1C3Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C4, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A1C4Ref);

                    ReasonerUtilities.AddAction(new ActionDefinition((int)StressTestAIAction.A2), authoring.actionsEnabled, ref reasoner, actionsBuffer, out actionRef);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C0, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A2C0Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C1, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A2C1Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C2, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A2C2Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C3, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A2C3Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C4, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A2C4Ref);

                    ReasonerUtilities.AddAction(new ActionDefinition((int)StressTestAIAction.A3), authoring.actionsEnabled, ref reasoner, actionsBuffer, out actionRef);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C0, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A3C0Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C1, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A3C1Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C2, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A3C2Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C3, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A3C3Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C4, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A3C4Ref);

                    ReasonerUtilities.AddAction(new ActionDefinition((int)StressTestAIAction.A4), authoring.actionsEnabled, ref reasoner, actionsBuffer, out actionRef);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C0, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A4C0Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C1, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A4C1Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C2, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A4C2Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C3, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A4C3Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C4, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A4C4Ref);

                    ReasonerUtilities.AddAction(new ActionDefinition((int)StressTestAIAction.A5), authoring.actionsEnabled, ref reasoner, actionsBuffer, out actionRef);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C0, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A5C0Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C1, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A5C1Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C2, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A5C2Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C3, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A5C3Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C4, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A5C4Ref);

                    ReasonerUtilities.AddAction(new ActionDefinition((int)StressTestAIAction.A6), authoring.actionsEnabled, ref reasoner, actionsBuffer, out actionRef);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C0, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A6C0Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C1, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A6C1Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C2, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A6C2Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C3, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A6C3Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C4, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A6C4Ref);

                    ReasonerUtilities.AddAction(new ActionDefinition((int)StressTestAIAction.A7), authoring.actionsEnabled, ref reasoner, actionsBuffer, out actionRef);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C0, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A7C0Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C1, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A7C1Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C2, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A7C2Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C3, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A7C3Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C4, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A7C4Ref);

                    ReasonerUtilities.AddAction(new ActionDefinition((int)StressTestAIAction.A8), authoring.actionsEnabled, ref reasoner, actionsBuffer, out actionRef);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C0, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A8C0Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C1, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A8C1Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C2, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A8C2Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C3, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A8C3Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C4, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A8C4Ref);

                    ReasonerUtilities.AddAction(new ActionDefinition((int)StressTestAIAction.A9), authoring.actionsEnabled, ref reasoner, actionsBuffer, out actionRef);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C0, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A9C0Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C1, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A9C1Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C2, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A9C2Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C3, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A9C3Ref);
                    ReasonerUtilities.AddConsideration(considerationSetComponent.C4, ref actionRef, authoring.considerationsEnabled, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out stressTest.A9C4Ref);
                }
                ReasonerUtilities.EndBakeReasoner(this, reasoner);
            }

            AddComponent(GetEntity(TransformUsageFlags.None), stressTest);
        }
    }
}
