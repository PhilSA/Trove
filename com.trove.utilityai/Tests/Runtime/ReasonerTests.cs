using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Trove.UtilityAI.Tests
{
    public struct TestEntity : IComponentData
    {
        public int ID;
    }

    [TestFixture]
    public class ReasonerTests
    {
        public enum TestActionType
        {
            A,
            B,
            C,
            D,
        }

        private World World => World.DefaultGameObjectInjectionWorld;
        private EntityManager EntityManager => World.EntityManager;

        [SetUp]
        public void SetUp()
        {
        }

        [TearDown]
        public void TearDown()
        {
            EntityQuery testEntitiesQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<TestEntity>().Build(EntityManager);
            EntityManager.DestroyEntity(testEntitiesQuery);
        }

        public Entity CreateTestEntity(int id = 0)
        {
            Entity entity = EntityManager.CreateEntity(typeof(TestEntity));
            EntityManager.AddComponentData(entity, new TestEntity { ID = id });
            return entity;
        }

        public Entity CreateECBTestEntity(ref EntityCommandBuffer ecb, int id = 0)
        {
            Entity entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new TestEntity { ID = id });
            return entity;
        }

        public BlobAssetReference<ConsiderationDefinition> CreateConsiderationDefinition()
        {
            BlobBuilder builder = new BlobBuilder(Allocator.Temp);
            ref ConsiderationDefinition definition = ref builder.ConstructRoot<ConsiderationDefinition>();
            definition = new ConsiderationDefinition { Curve = ParametricCurve.GetDefault(ParametricCurveType.Linear) };
            BlobAssetReference<ConsiderationDefinition> blobReference = builder.CreateBlobAssetReference<ConsiderationDefinition>(Allocator.Persistent);
            builder.Dispose();

            return blobReference;
        }

        public void CreateReasoner(
            out Entity reasonerEntity, 
            out Reasoner reasoner, 
            out DynamicBuffer<Action> actionsBuffer,
            out DynamicBuffer<Consideration> considerationsBuffer,
            out DynamicBuffer<ConsiderationInput> considerationInputsBuffer)
        {
            reasonerEntity = CreateTestEntity();
            ReasonerUtilities.CreateReasoner(World.EntityManager, reasonerEntity);

            reasoner = World.EntityManager.GetComponentData<Reasoner>(reasonerEntity);
            actionsBuffer = World.EntityManager.GetBuffer<Action>(reasonerEntity);
            considerationsBuffer = World.EntityManager.GetBuffer<Consideration>(reasonerEntity);
            considerationInputsBuffer = World.EntityManager.GetBuffer<ConsiderationInput>(reasonerEntity);
        }

        [Test]
        public void FullWorkflow()
        {
            CreateReasoner(out Entity reasonerEntity, out Reasoner reasoner, out DynamicBuffer<Action> actionsBuffer, out DynamicBuffer<Consideration> considerationsBuffer, out DynamicBuffer<ConsiderationInput> considerationInputsBuffer);

            Action selectedAction;
            Unity.Mathematics.Random random = new Unity.Mathematics.Random();

            Action a1;
            ActionReference a1Ref = default;
            Consideration a1c1;
            BlobAssetReference<ConsiderationDefinition> a1c1Def = CreateConsiderationDefinition();
            ConsiderationReference a1c1Ref = default;

            Action a2;
            ActionReference a2Ref = default;
            Consideration a2c1;
            BlobAssetReference<ConsiderationDefinition> a2c1Def = CreateConsiderationDefinition();
            ConsiderationReference a2c1Ref = default;

            Action a3;
            ActionReference a3Ref = default;
            Consideration a3c1;
            BlobAssetReference<ConsiderationDefinition> a3c1Def = CreateConsiderationDefinition();
            ConsiderationReference a3c1Ref = default;
            Consideration a3c2;
            BlobAssetReference<ConsiderationDefinition> a3c2Def = CreateConsiderationDefinition();
            ConsiderationReference a3c2Ref = default;
            Consideration a3c3;
            BlobAssetReference<ConsiderationDefinition> a3c3Def = CreateConsiderationDefinition();
            ConsiderationReference a3c3Ref = default;

            Action a4;
            ActionReference a4Ref = default;

            float a2ScoreMultiplier = 5f;

            // ==================================================
            // CREATION
            // ==================================================

            // Add Action 1
            ReasonerUtilities.AddAction(new ActionDefinition((int)TestActionType.A), true, ref reasoner, actionsBuffer, out a1Ref);

            a1 = actionsBuffer[0];

            // Reasoner
            Assert.AreEqual(1, reasoner.__internal__actionIDCounter);
            Assert.AreEqual(0, reasoner.__internal__actionsVersion);
            Assert.AreEqual(0, reasoner.__internal__considerationIDCounter);
            Assert.AreEqual(0, reasoner.__internal__considerationsVersion);

            // Action 1
            Assert.AreEqual(1, a1.ID);
            Assert.AreEqual(1, a1.__internal__id);
            Assert.AreEqual(true, a1.IsEnabled);
            Assert.AreEqual(0, a1.Type);
            Assert.AreEqual(0, a1.IndexInType);
            Assert.AreEqual(1f, a1.ScoreMultiplier);

            // ActionReference 1
            Assert.AreEqual(1, a1Ref.ID);
            Assert.AreEqual(1, a1Ref.__internal__id);
            Assert.IsTrue(a1Ref.IsCreated);
            Assert.IsTrue(a1Ref.__internal__isCreated == (byte)1);
            Assert.AreEqual(0, a1Ref.__internal__actionsVersion);
            Assert.AreEqual(0, a1Ref.__internal__index);

            // Add Action 2
            ReasonerUtilities.AddAction(new ActionDefinition((int)TestActionType.B, 2, a2ScoreMultiplier), true, ref reasoner, actionsBuffer, out a2Ref);

            a2 = actionsBuffer[1];

            // Reasoner
            Assert.AreEqual(2, reasoner.__internal__actionIDCounter);
            Assert.AreEqual(0, reasoner.__internal__actionsVersion);
            Assert.AreEqual(0, reasoner.__internal__considerationIDCounter);
            Assert.AreEqual(0, reasoner.__internal__considerationsVersion);

            // Action 2
            Assert.AreEqual(2, a2.ID);
            Assert.AreEqual(2, a2.__internal__id);
            Assert.AreEqual(true, a2.IsEnabled);
            Assert.AreEqual(1, a2.Type);
            Assert.AreEqual(2, a2.IndexInType);
            Assert.AreEqual(a2ScoreMultiplier, a2.ScoreMultiplier);

            // ActionReference 2
            Assert.AreEqual(2, a2Ref.ID);
            Assert.AreEqual(2, a2Ref.__internal__id);
            Assert.IsTrue(a2Ref.IsCreated);
            Assert.IsTrue(a2Ref.__internal__isCreated == (byte)1);
            Assert.AreEqual(0, a2Ref.__internal__actionsVersion);
            Assert.AreEqual(1, a2Ref.__internal__index);

            // Add Consideration 1 on Action 1
            ReasonerUtilities.AddConsideration(a1c1Def, ref a1Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a1c1Ref);

            a1 = actionsBuffer[0];
            a1c1 = considerationsBuffer[0];

            // Reasoner
            Assert.AreEqual(2, reasoner.__internal__actionIDCounter);
            Assert.AreEqual(0, reasoner.__internal__actionsVersion);
            Assert.AreEqual(1, reasoner.__internal__considerationIDCounter);
            Assert.AreEqual(0, reasoner.__internal__considerationsVersion);

            // Action 1
            Assert.AreEqual(1, a1.ID);
            Assert.AreEqual(1, a1.__internal__id);
            Assert.AreEqual(true, a1.IsEnabled);

            // Consideration 1 of Action 1
            Assert.AreEqual(1, a1c1.ID);
            Assert.AreEqual(1, a1c1.__internal__id);
            Assert.AreEqual(true, a1c1.IsEnabled);
            Assert.AreEqual(0, a1c1.__internal__actionIndex);

            // ConsiderationReference 1 of Action 1
            Assert.AreEqual(1, a1c1Ref.ID);
            Assert.AreEqual(1, a1c1Ref.__internal__id);
            Assert.AreEqual(1, a1c1Ref.__internal__isCreated);
            Assert.AreEqual(0, a1c1Ref.__internal__index);
            Assert.AreEqual(0, a1c1Ref.__internal__considerationsVersion);

            // Add Action 3
            ReasonerUtilities.AddAction(new ActionDefinition((int)TestActionType.C), true, ref reasoner, actionsBuffer, out a3Ref);

            // Reasoner
            Assert.AreEqual(3, reasoner.__internal__actionIDCounter);
            Assert.AreEqual(0, reasoner.__internal__actionsVersion);
            Assert.AreEqual(1, reasoner.__internal__considerationIDCounter);
            Assert.AreEqual(0, reasoner.__internal__considerationsVersion);

            // ActionReference 3
            Assert.AreEqual(3, a3Ref.ID);
            Assert.AreEqual(3, a3Ref.__internal__id);
            Assert.IsTrue(a3Ref.IsCreated);
            Assert.IsTrue(a3Ref.__internal__isCreated == (byte)1);
            Assert.AreEqual(0, a3Ref.__internal__actionsVersion);
            Assert.AreEqual(2, a3Ref.__internal__index);

            // Add Considerations 1 to Action 3
            ReasonerUtilities.AddConsideration(a3c1Def, ref a3Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a3c1Ref);

            a3c1 = considerationsBuffer[1];

            // Consideration 1 of Action 3
            Assert.AreEqual(2, a3c1.ID);
            Assert.AreEqual(2, a3c1.__internal__id);
            Assert.AreEqual(true, a3c1.IsEnabled);
            Assert.AreEqual(2, a3c1.__internal__actionIndex);

            // ConsiderationReference 1 of Action 3
            Assert.AreEqual(2, a3c1Ref.ID);
            Assert.AreEqual(2, a3c1Ref.__internal__id);
            Assert.AreEqual(1, a3c1Ref.__internal__isCreated);
            Assert.AreEqual(1, a3c1Ref.__internal__index);
            Assert.AreEqual(0, a3c1Ref.__internal__considerationsVersion);

            // Add Considerations 2 to Action 3
            ReasonerUtilities.AddConsideration(a3c2Def, ref a3Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a3c2Ref);

            a3 = actionsBuffer[2];
            a3c1 = considerationsBuffer[2]; // a3c2 got inserted before a3c1
            a3c2 = considerationsBuffer[1];

            // Reasoner
            Assert.AreEqual(3, reasoner.__internal__actionIDCounter);
            Assert.AreEqual(0, reasoner.__internal__actionsVersion);
            Assert.AreEqual(3, reasoner.__internal__considerationIDCounter);
            Assert.AreEqual(1, reasoner.__internal__considerationsVersion); // A consideratio insert cause the indexes to shift

            // Action 3
            Assert.AreEqual(3, a3.ID);
            Assert.AreEqual(3, a3.__internal__id);
            Assert.AreEqual(true, a3.IsEnabled);

            // Consideration 2 of Action 3
            Assert.AreEqual(3, a3c2.ID);
            Assert.AreEqual(3, a3c2.__internal__id);
            Assert.AreEqual(true, a3c2.IsEnabled);
            Assert.AreEqual(2, a3c2.__internal__actionIndex);

            // ConsiderationReference 1 of Action 3 
            Assert.AreEqual(2, a3c1Ref.ID);
            Assert.AreEqual(2, a3c1Ref.__internal__id);
            Assert.AreEqual(1, a3c1Ref.__internal__isCreated);
            Assert.AreEqual(1, a3c1Ref.__internal__index); // Index didn't update yet because we haven't used it yet
            Assert.AreEqual(0, a3c1Ref.__internal__considerationsVersion); // Version isn't up to date yet because we haven't used it yet

            ReasonerUtilities.SetConsiderationInput(ref a3c1Ref, 0.0f, in reasoner, considerationsBuffer, considerationInputsBuffer);

            // ConsiderationReference 1 of Action 3 
            Assert.AreEqual(2, a3c1Ref.ID);
            Assert.AreEqual(2, a3c1Ref.__internal__id);
            Assert.AreEqual(1, a3c1Ref.__internal__isCreated);
            Assert.AreEqual(2, a3c1Ref.__internal__index); // Index updated
            Assert.AreEqual(1, a3c1Ref.__internal__considerationsVersion); // Version updated

            // Add Action 4
            ReasonerUtilities.AddAction(new ActionDefinition((int)TestActionType.D), true, ref reasoner, actionsBuffer, out a4Ref);

            // Add Consideration 1 of Action 2
            ReasonerUtilities.AddConsideration(a2c1Def, ref a2Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a2c1Ref);

            // Add Consideration 3 of Action 3
            ReasonerUtilities.AddConsideration(a3c3Def, ref a3Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a3c3Ref);

            // Reasoner
            Assert.AreEqual(4, reasoner.__internal__actionIDCounter);
            Assert.AreEqual(0, reasoner.__internal__actionsVersion);
            Assert.AreEqual(5, reasoner.__internal__considerationIDCounter);
            Assert.AreEqual(2, reasoner.__internal__considerationsVersion);


            // ==================================================
            // SCORES 1
            // ==================================================

            // Calculate scores
            ActionSelectors.HighestScoring actionSelector = new ActionSelectors.HighestScoring();
            ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelector, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out selectedAction);

            // Selected Action (all 0, no input)
            Assert.AreEqual(0f, selectedAction.__internal__latestScoreWithoutMultiplier);
            Assert.AreEqual(0f, selectedAction.Score);
            Assert.AreEqual(0f, selectedAction.ScoreWithoutMultiplier);

            // Set inputs 0.5f to all considerations
            ReasonerUtilities.SetConsiderationInput(ref a1c1Ref, 0.5f, in reasoner, considerationsBuffer, considerationInputsBuffer);
            ReasonerUtilities.SetConsiderationInput(ref a2c1Ref, 0.5f, in reasoner, considerationsBuffer, considerationInputsBuffer);
            ReasonerUtilities.SetConsiderationInput(ref a3c1Ref, 0.5f, in reasoner, considerationsBuffer, considerationInputsBuffer);
            ReasonerUtilities.SetConsiderationInput(ref a3c2Ref, 0.5f, in reasoner, considerationsBuffer, considerationInputsBuffer);
            ReasonerUtilities.SetConsiderationInput(ref a3c3Ref, 0.5f, in reasoner, considerationsBuffer, considerationInputsBuffer);

            // Calculate scores
            ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelector, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out selectedAction);

            a3 = actionsBuffer[2];

            // Selected Action (Action 2, because of multiplier)
            Assert.AreEqual(1, selectedAction.Type);
            Assert.AreEqual(2, selectedAction.ID);
            Assert.AreEqual(0.125f, selectedAction.__internal__latestScoreWithoutMultiplier); // considerations counts compensation (a2 has 1 consideration, but compensates for a3's 3 considerations)
            Assert.AreEqual(0.125f * 5f, selectedAction.Score); // Action 2 has a ScoreMultiplier of 5f
            Assert.AreEqual(0.125f, selectedAction.ScoreWithoutMultiplier);

            // Action 3
            Assert.AreEqual(2, a3.Type);
            Assert.AreEqual(3, a3.ID);
            Assert.AreEqual(0.125f, a3.__internal__latestScoreWithoutMultiplier);
            Assert.AreEqual(0.125f, a3.Score); 
            Assert.AreEqual(0.125f, a3.ScoreWithoutMultiplier);


            // ==================================================
            // SET ACTION DATA
            // ==================================================

            // Reasoner
            Assert.AreEqual(4, reasoner.__internal__actionIDCounter);
            Assert.AreEqual(0, reasoner.__internal__actionsVersion);
            Assert.AreEqual(5, reasoner.__internal__considerationIDCounter);
            Assert.AreEqual(2, reasoner.__internal__considerationsVersion);

            // Change Action 1 data
            ReasonerUtilities.SetActionData(ref a1Ref, new ActionDefinition((int)TestActionType.B, 3, 1.5f), false, ref reasoner, actionsBuffer);

            a1 = actionsBuffer[0];
            a1c1 = considerationsBuffer[0];

            // Reasoner (check disable didn't cause a version bump)
            Assert.AreEqual(4, reasoner.__internal__actionIDCounter);
            Assert.AreEqual(0, reasoner.__internal__actionsVersion);
            Assert.AreEqual(5, reasoner.__internal__considerationIDCounter);
            Assert.AreEqual(2, reasoner.__internal__considerationsVersion); // Version didn't get bumped, because it's just disabling considerations of the action

            // Action 1
            Assert.AreEqual(1, a1.Type); // New Type applied
            Assert.AreEqual(3, a1.IndexInType); // New IndexInType applied
            Assert.AreEqual(1, a1.ID); // It's Action 1
            Assert.AreEqual(0.125f * 1.5f, a1.Score); // Multiplier applied
            Assert.AreEqual(0.125f, a1.ScoreWithoutMultiplier);

            // Consideration 1 of Action 1
            Assert.AreEqual(1, a1c1.ID);
            Assert.AreEqual(1, a1c1.__internal__id);
            Assert.IsTrue(a1c1.IsEnabled); // Disabling an action must not disable its considerations, because then we might overwrite the considerations we enabled/disabled manually

            // Change Action 2 data (diminish multiplier)
            ReasonerUtilities.SetActionData(ref a2Ref, new ActionDefinition((int)TestActionType.B, 0, 0.5f), true, ref reasoner, actionsBuffer);

            a1 = actionsBuffer[0];
            a2 = actionsBuffer[1];
            a3 = actionsBuffer[2];

            // Action 2
            Assert.AreEqual(2, a2.ID); // It's Action 2
            Assert.AreEqual(0.125f * 0.5f, a2.Score); // Multiplier applied
            Assert.AreEqual(0.125f, a2.ScoreWithoutMultiplier);

            // Calculate scores
            ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelector, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out selectedAction);

            a1 = actionsBuffer[0];
            a2 = actionsBuffer[1];
            a3 = actionsBuffer[2];

            // With the multiplier changes on Action 2 and Action 1 disabled, Action 3 should win
            Assert.AreEqual(3, selectedAction.ID); // It's Action 3

            // ==================================================
            // SET CONSIDERATION DATA
            // ==================================================

            // Change consideration data of Consideration 1 of Action 3
            ReasonerUtilities.SetConsiderationData(ref a3c1Ref, a3c1Def, false, ref reasoner, considerationsBuffer);

            a3c1 = considerationsBuffer[3];

            // Consideration 1 of Action 3
            Assert.AreEqual(2, a3c1.ID);
            Assert.AreEqual(2, a3c1.__internal__id);
            Assert.IsFalse(a3c1.IsEnabled);
            Assert.AreEqual(false, a3c1.IsEnabled);

            // Calculate scores
            ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelector, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out selectedAction);
            
            a2 = actionsBuffer[1];

            // Action 3 wins again
            Assert.AreEqual(3, selectedAction.ID); // It's Action 3
            Assert.AreEqual(0.25f, selectedAction.__internal__latestScoreWithoutMultiplier); // only 2 considerations count now

            // Action 2
            Assert.AreEqual(0.25f * 0.5f, a2.Score);
            Assert.AreEqual(0.25f, a2.__internal__latestScoreWithoutMultiplier);


            // ==================================================
            // DISABLING ACTIONS
            // ==================================================

            // Disable Action 3, so only Action 2 and 4 are left
            ReasonerUtilities.SetActionEnabled(ref a3Ref, false, ref reasoner, actionsBuffer);

            // Calculate scores
            ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelector, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out selectedAction);
            
            // It's Action 2
            Assert.AreEqual(2, selectedAction.ID);

            // Disable Action 2, so there's only Action 4 left
            ReasonerUtilities.SetActionEnabled(ref a2Ref, false, ref reasoner, actionsBuffer);

            // Calculate scores
            bool result = ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelector, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out selectedAction);

            // Action 4
            Assert.IsTrue(result);
            Assert.AreEqual(4, selectedAction.ID);

            // Disable Action 4, so there's none left
            ReasonerUtilities.SetActionEnabled(ref a4Ref, false, ref reasoner, actionsBuffer);

            // Calculate scores
            result = ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelector, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out selectedAction);

            // Action 4
            Assert.IsFalse(result);
            Assert.IsFalse(selectedAction.IsEnabled);


            // ==================================================
            // ENABLING ACTIONS
            // ==================================================

            // Enable Action 4
            ReasonerUtilities.SetActionEnabled(ref a4Ref, true, ref reasoner, actionsBuffer);

            // Calculate scores
            result = ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelector, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out selectedAction);

            // Action 4
            Assert.IsTrue(result);
            Assert.IsTrue(selectedAction.IsEnabled);
            Assert.AreEqual(0f, selectedAction.Score); // No considerations 

            // Reset Action and Consideration datas
            ReasonerUtilities.SetActionData(ref a1Ref, new ActionDefinition((int)TestActionType.A, 0, 1f), true, ref reasoner, actionsBuffer);
            ReasonerUtilities.SetActionData(ref a2Ref, new ActionDefinition((int)TestActionType.B, 0, 2f), false, ref reasoner, actionsBuffer);
            ReasonerUtilities.SetActionData(ref a3Ref, new ActionDefinition((int)TestActionType.C, 0, 1f), false, ref reasoner, actionsBuffer);
            ReasonerUtilities.SetActionData(ref a4Ref, new ActionDefinition((int)TestActionType.D, 0, 1f), false, ref reasoner, actionsBuffer);
            ReasonerUtilities.SetConsiderationData(ref a3c1Ref, a3c1Def, true, ref reasoner, considerationsBuffer);

            // ==================================================
            // DISABLING CONSIDERATIONS
            // ==================================================

            // Calculate scores
            result = ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelector, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out selectedAction);

            a1 = actionsBuffer[0];
            a2 = actionsBuffer[1];
            a3 = actionsBuffer[2];
            a4 = actionsBuffer[3];

            Assert.AreEqual(1, reasoner.__internal__highestActionConsiderationsCount);

            // Action 1 has one consideration
            Assert.IsTrue(a1.IsEnabled);
            Assert.IsFalse(a2.IsEnabled);
            Assert.IsFalse(a3.IsEnabled);
            Assert.IsFalse(a4.IsEnabled);
            Assert.AreEqual(0.5f, a1.Score); // No compensation since it's the only enabled cons+action

            // Disable Action 1's consideration
            ReasonerUtilities.SetConsiderationEnabled(ref a1c1Ref, false, ref reasoner, considerationsBuffer);

            // Calculate scores
            result = ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelector, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out selectedAction);

            a1 = actionsBuffer[0];
            a1c1 = considerationsBuffer[0];

            Assert.AreEqual(0, reasoner.__internal__highestActionConsiderationsCount);

            // Action 1 has zero consideration
            Assert.AreEqual(0f, a1.Score);

            // Consideration 1 of Action 1
            Assert.AreEqual(1, a1c1.ID);
            Assert.AreEqual(1, a1c1.__internal__id);
            Assert.IsFalse(a1c1.IsEnabled);
            Assert.AreEqual(false, a1c1.IsEnabled);

            // ==================================================
            // ENABLING CONSIDERATIONS
            // ==================================================

            // Enable Action 4's consideration
            ReasonerUtilities.SetConsiderationEnabled(ref a1c1Ref, true, ref reasoner, considerationsBuffer);

            // Calculate scores
            result = ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelector, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out selectedAction);

            a1 = actionsBuffer[0];

            // Action 4 has one consideration
            Assert.AreEqual(0.5f, a1.Score);


            // ==================================================
            // REMOVING CONSIDERATIONS
            // ==================================================

            // Remove consideration 1 of Action 1
            ReasonerUtilities.RemoveConsideration(ref a1c1Ref, ref reasoner, considerationsBuffer, considerationInputsBuffer);

            // Set input unsuccessful on removed consideration
            result = ReasonerUtilities.SetConsiderationInput(ref a1c1Ref, 0.5f, in reasoner, considerationsBuffer, considerationInputsBuffer);

            Assert.IsFalse(result);

            // Calculate scores
            result = ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelector, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out selectedAction);

            a1 = actionsBuffer[0];

            // Action 1 has zero consideration
            Assert.AreEqual(0f, a1.Score);

            // first consideration in buffer is no longer a1c1
            Assert.AreNotEqual(0, considerationsBuffer[0].ID);

            // Re-Remove consideration 1 of Action 1
            ReasonerUtilities.RemoveConsideration(ref a1c1Ref, ref reasoner, considerationsBuffer, considerationInputsBuffer);


            // ==================================================
            // REMOVING ACTIONS
            // ==================================================

            // Remove Action 1
            ReasonerUtilities.RemoveAction(ref a1Ref, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer);

            // first action in buffer is no longer a1
            Assert.AreNotEqual(0, actionsBuffer[0].ID);

            // Re-Remove Action 1
            ReasonerUtilities.RemoveAction(ref a1Ref, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer);
        }

        [Test]
        public void ConsiderationVersions()
        {
            CreateReasoner(out Entity reasonerEntity, out Reasoner reasoner, out DynamicBuffer<Action> actionsBuffer, out DynamicBuffer<Consideration> considerationsBuffer, out DynamicBuffer<ConsiderationInput> considerationInputsBuffer);

            Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(0);

            ActionReference a1Ref = default;
            BlobAssetReference<ConsiderationDefinition> a1c1Def = CreateConsiderationDefinition();
            ConsiderationReference a1c1Ref = default;
            BlobAssetReference<ConsiderationDefinition> a1c2Def = CreateConsiderationDefinition();
            ConsiderationReference a1c2Ref = default;
            BlobAssetReference<ConsiderationDefinition> a1c3Def = CreateConsiderationDefinition();
            ConsiderationReference a1c3Ref = default;
            BlobAssetReference<ConsiderationDefinition> a1c4Def = CreateConsiderationDefinition();
            ConsiderationReference a1c4Ref = default;

            ReasonerUtilities.AddAction(new ActionDefinition((int)TestActionType.A), true, ref reasoner, actionsBuffer, out a1Ref);

            // Add a1c1 - a1c1 will point to index 0 and ID 0
            ReasonerUtilities.AddConsideration(a1c1Def, ref a1Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a1c1Ref);
            Assert.AreEqual(0, a1c1Ref.__internal__index);
            Assert.AreEqual(1, a1c1Ref.__internal__id);

            // Add a1c2 - a1c2 will point to index 0 and ID 1
            ReasonerUtilities.AddConsideration(a1c2Def, ref a1Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a1c2Ref);
            Assert.AreEqual(0, a1c1Ref.__internal__index);
            Assert.AreEqual(1, a1c1Ref.__internal__id);
            Assert.AreEqual(0, a1c2Ref.__internal__index);
            Assert.AreEqual(2, a1c2Ref.__internal__id);

            // Set inputs of a1c1 - it will update its index
            ReasonerUtilities.SetConsiderationInput(ref a1c1Ref, 1f, in reasoner, considerationsBuffer, considerationInputsBuffer);
            Assert.AreEqual(1, a1c1Ref.__internal__index);
            Assert.AreEqual(1, a1c1Ref.__internal__id);
            Assert.AreEqual(0, a1c2Ref.__internal__index);
            Assert.AreEqual(2, a1c2Ref.__internal__id);

            // Remove both considerations
            ReasonerUtilities.RemoveConsideration(ref a1c1Ref, ref reasoner, considerationsBuffer, considerationInputsBuffer);
            ReasonerUtilities.RemoveConsideration(ref a1c2Ref, ref reasoner, considerationsBuffer, considerationInputsBuffer);

            // Add two new considerations
            ReasonerUtilities.AddConsideration(a1c3Def, ref a1Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a1c3Ref);
            ReasonerUtilities.AddConsideration(a1c4Def, ref a1Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a1c4Ref);
            Assert.AreEqual(2, considerationsBuffer.Length);

            // Verify that set inputs to a1c1 is unsuccessful, even though there is a new consideration at that index
            Assert.IsFalse(ReasonerUtilities.SetConsiderationInput(ref a1c1Ref, 0.6f, in reasoner, considerationsBuffer, considerationInputsBuffer));
            Assert.IsFalse(considerationInputsBuffer[1].__internal__input.IsRoughlyEqual(0.6f));
        }

        [Test]
        public void ActionVersions()
        {
            CreateReasoner(out Entity reasonerEntity, out Reasoner reasoner, out DynamicBuffer<Action> actionsBuffer, out DynamicBuffer<Consideration> considerationsBuffer, out DynamicBuffer<ConsiderationInput> considerationInputsBuffer);

            Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(0);

            ActionReference a1Ref = default;
            ActionReference a2Ref = default;
            ActionReference a3Ref = default;
            ActionReference a4Ref = default;

            ReasonerUtilities.AddAction(new ActionDefinition((int)TestActionType.A), true, ref reasoner, actionsBuffer, out a1Ref);
            Assert.AreEqual(0, a1Ref.__internal__index);
            Assert.AreEqual(1, a1Ref.__internal__id);

            ReasonerUtilities.AddAction(new ActionDefinition((int)TestActionType.A), true, ref reasoner, actionsBuffer, out a2Ref);
            Assert.AreEqual(0, a1Ref.__internal__index);
            Assert.AreEqual(1, a1Ref.__internal__id);
            Assert.AreEqual(1, a2Ref.__internal__index);
            Assert.AreEqual(2, a2Ref.__internal__id);

            ReasonerUtilities.AddAction(new ActionDefinition((int)TestActionType.A), true, ref reasoner, actionsBuffer, out a3Ref);
            Assert.AreEqual(0, a1Ref.__internal__index);
            Assert.AreEqual(1, a1Ref.__internal__id);
            Assert.AreEqual(1, a2Ref.__internal__index);
            Assert.AreEqual(2, a2Ref.__internal__id);
            Assert.AreEqual(2, a3Ref.__internal__index);
            Assert.AreEqual(3, a3Ref.__internal__id);

            // Remove a3
            ReasonerUtilities.RemoveAction(ref a3Ref, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer);

            ReasonerUtilities.AddAction(new ActionDefinition((int)TestActionType.A), true, ref reasoner, actionsBuffer, out a4Ref);
            Assert.AreEqual(0, a1Ref.__internal__index);
            Assert.AreEqual(1, a1Ref.__internal__id);
            Assert.AreEqual(1, a2Ref.__internal__index);
            Assert.AreEqual(2, a2Ref.__internal__id);
            Assert.AreEqual(2, a3Ref.__internal__index);
            Assert.AreEqual(3, a3Ref.__internal__id);
            Assert.AreEqual(2, a4Ref.__internal__index);
            Assert.AreEqual(4, a4Ref.__internal__id);

            // Verify that set scoreMultiplier to a3 is unsuccessful, even though there is a new action at that index
            Assert.IsFalse(ReasonerUtilities.SetActionScoreMultiplier(ref a3Ref, 2f, in reasoner, actionsBuffer));
            Assert.IsFalse(actionsBuffer[2].ScoreMultiplier.IsRoughlyEqual(2f));
        }

        [Test]
        public void ActionWithoutConsiderations()
        {
            CreateReasoner(out Entity reasonerEntity, out Reasoner reasoner, out DynamicBuffer<Action> actionsBuffer, out DynamicBuffer<Consideration> considerationsBuffer, out DynamicBuffer<ConsiderationInput> considerationInputsBuffer);

            Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(0);

            ActionReference a1Ref = default;
            BlobAssetReference<ConsiderationDefinition> a1c1Def = CreateConsiderationDefinition();
            ConsiderationReference a1c1Ref = default;
            BlobAssetReference<ConsiderationDefinition> a1c2Def = CreateConsiderationDefinition();
            ConsiderationReference a1c2Ref = default;

            ActionReference a2Ref = default;

            // Create a1 and a2
            ReasonerUtilities.AddAction(new ActionDefinition((int)TestActionType.A), true, ref reasoner, actionsBuffer, out a1Ref);
            ReasonerUtilities.AddAction(new ActionDefinition((int)TestActionType.A), true, ref reasoner, actionsBuffer, out a2Ref);

            // Add considerations only to a1
            ReasonerUtilities.AddConsideration(a1c1Def, ref a1Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a1c1Ref);
            ReasonerUtilities.AddConsideration(a1c2Def, ref a1Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a1c2Ref);

            // Set inputs
            ReasonerUtilities.SetConsiderationInput(ref a1c1Ref, 1f, in reasoner, considerationsBuffer, considerationInputsBuffer);
            ReasonerUtilities.SetConsiderationInput(ref a1c2Ref, 1f, in reasoner, considerationsBuffer, considerationInputsBuffer);

            // Compute
            ActionSelectors.None actionSelector = new ActionSelectors.None();
            ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelector, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out Action selectedAction);

            Assert.AreEqual(1f, actionsBuffer[0].Score);
            Assert.AreEqual(0f, actionsBuffer[1].Score);
        }

        [Test]
        public void ScoreCompensation()
        {
            CreateReasoner(out Entity reasonerEntity, out Reasoner reasoner, out DynamicBuffer<Action> actionsBuffer, out DynamicBuffer<Consideration> considerationsBuffer, out DynamicBuffer<ConsiderationInput> considerationInputsBuffer);

            Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(0);

            ActionReference a1Ref = default;
            BlobAssetReference<ConsiderationDefinition> a1c1Def = CreateConsiderationDefinition();
            ConsiderationReference a1c1Ref = default;
            BlobAssetReference<ConsiderationDefinition> a1c2Def = CreateConsiderationDefinition();
            ConsiderationReference a1c2Ref = default;
            BlobAssetReference<ConsiderationDefinition> a1c3Def = CreateConsiderationDefinition();
            ConsiderationReference a1c3Ref = default;
            BlobAssetReference<ConsiderationDefinition> a1c4Def = CreateConsiderationDefinition();
            ConsiderationReference a1c4Ref = default;

            ActionReference a2Ref = default;
            BlobAssetReference<ConsiderationDefinition> a2c1Def = CreateConsiderationDefinition();
            ConsiderationReference a2c1Ref = default;
            BlobAssetReference<ConsiderationDefinition> a2c2Def = CreateConsiderationDefinition();
            ConsiderationReference a2c2Ref = default;
            BlobAssetReference<ConsiderationDefinition> a2c3Def = CreateConsiderationDefinition();
            ConsiderationReference a2c3Ref = default;
            BlobAssetReference<ConsiderationDefinition> a2c4Def = CreateConsiderationDefinition();
            ConsiderationReference a2c4Ref = default;

            // Create a1 and a2
            ReasonerUtilities.AddAction(new ActionDefinition((int)TestActionType.A), true, ref reasoner, actionsBuffer, out a1Ref);
            ReasonerUtilities.AddAction(new ActionDefinition((int)TestActionType.A), true, ref reasoner, actionsBuffer, out a2Ref);

            // Add 4 considerations to a1, and 1 consideration to a2
            ReasonerUtilities.AddConsideration(a1c1Def, ref a1Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a1c1Ref);
            ReasonerUtilities.AddConsideration(a1c2Def, ref a1Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a1c2Ref);
            ReasonerUtilities.AddConsideration(a1c3Def, ref a1Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a1c3Ref);
            ReasonerUtilities.AddConsideration(a1c4Def, ref a1Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a1c4Ref);
            ReasonerUtilities.AddConsideration(a2c1Def, ref a2Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a2c1Ref);

            // Set same input to all considerations
            ReasonerUtilities.SetConsiderationInput(ref a1c1Ref, 1f, in reasoner, considerationsBuffer, considerationInputsBuffer);
            ReasonerUtilities.SetConsiderationInput(ref a1c2Ref, 1f, in reasoner, considerationsBuffer, considerationInputsBuffer);
            ReasonerUtilities.SetConsiderationInput(ref a1c3Ref, 1f, in reasoner, considerationsBuffer, considerationInputsBuffer);
            ReasonerUtilities.SetConsiderationInput(ref a1c4Ref, 1f, in reasoner, considerationsBuffer, considerationInputsBuffer);
            ReasonerUtilities.SetConsiderationInput(ref a2c1Ref, 1f, in reasoner, considerationsBuffer, considerationInputsBuffer);

            // Compute
            ActionSelectors.None actionSelectorNone = new ActionSelectors.None();
            ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelectorNone, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out Action selectedAction);

            // Validate that both actions have equal scores
            Assert.AreEqual(actionsBuffer[0].Score, actionsBuffer[1].Score);

            // Add disabled actions to a2
            ReasonerUtilities.AddConsideration(a2c2Def, ref a2Ref, false, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a2c2Ref);
            ReasonerUtilities.AddConsideration(a2c3Def, ref a2Ref, false, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a2c3Ref);
            ReasonerUtilities.AddConsideration(a2c4Def, ref a2Ref, false, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a2c4Ref);

            // Set different inputs on those compared to a1
            ReasonerUtilities.SetConsiderationInput(ref a2c2Ref, 0.5f, in reasoner, considerationsBuffer, considerationInputsBuffer);
            ReasonerUtilities.SetConsiderationInput(ref a2c3Ref, 0.5f, in reasoner, considerationsBuffer, considerationInputsBuffer);
            ReasonerUtilities.SetConsiderationInput(ref a2c4Ref, 0.5f, in reasoner, considerationsBuffer, considerationInputsBuffer);

            // Compute
            ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelectorNone, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out selectedAction);

            // Validate that both actions have equal scores
            Assert.AreEqual(actionsBuffer[0].Score, actionsBuffer[1].Score);

            // Now enable those a2 considerations 
            ReasonerUtilities.SetConsiderationEnabled(ref a2c2Ref, true, ref reasoner, considerationsBuffer);
            ReasonerUtilities.SetConsiderationEnabled(ref a2c3Ref, true, ref reasoner, considerationsBuffer);
            ReasonerUtilities.SetConsiderationEnabled(ref a2c4Ref, true, ref reasoner, considerationsBuffer);

            // Compute
            ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelectorNone, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out selectedAction);

            // And validate different scores
            Assert.AreNotEqual(actionsBuffer[0].Score, actionsBuffer[1].Score);
        }

        [Test]
        public void ActionSelectors()
        {
            CreateReasoner(out Entity reasonerEntity, out Reasoner reasoner, out DynamicBuffer<Action> actionsBuffer, out DynamicBuffer<Consideration> considerationsBuffer, out DynamicBuffer<ConsiderationInput> considerationInputsBuffer);

            Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex(0);

            ActionReference a1Ref = default;
            BlobAssetReference<ConsiderationDefinition> a1c1Def = CreateConsiderationDefinition();
            ConsiderationReference a1c1Ref = default;

            ActionReference a2Ref = default;
            BlobAssetReference<ConsiderationDefinition> a2c1Def = CreateConsiderationDefinition();
            ConsiderationReference a2c1Ref = default;

            ActionReference a3Ref = default;
            BlobAssetReference<ConsiderationDefinition> a3c1Def = CreateConsiderationDefinition();
            ConsiderationReference a3c1Ref = default;

            // A1 score should be 1.0f
            ReasonerUtilities.AddAction(new ActionDefinition((int)TestActionType.A), true, ref reasoner, actionsBuffer, out a1Ref);
            ReasonerUtilities.AddConsideration(a1c1Def, ref a1Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a1c1Ref);
            ReasonerUtilities.SetConsiderationInput(ref a1c1Ref, 0.9f, in reasoner, considerationsBuffer, considerationInputsBuffer);

            // A2 score should be 0.9f
            ReasonerUtilities.AddAction(new ActionDefinition((int)TestActionType.B), true, ref reasoner, actionsBuffer, out a2Ref);
            ReasonerUtilities.AddConsideration(a2c1Def, ref a2Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a2c1Ref);
            ReasonerUtilities.SetConsiderationInput(ref a2c1Ref, 0.9f, in reasoner, considerationsBuffer, considerationInputsBuffer);

            // A3 score should be 0.5f
            ReasonerUtilities.AddAction(new ActionDefinition((int)TestActionType.C), true, ref reasoner, actionsBuffer, out a3Ref);
            ReasonerUtilities.AddConsideration(a3c1Def, ref a3Ref, true, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out a3c1Ref);
            ReasonerUtilities.SetConsiderationInput(ref a3c1Ref, 0.9f, in reasoner, considerationsBuffer, considerationInputsBuffer);

            // NoActionSelector
            ActionSelectors.None actionSelectorNone = new ActionSelectors.None();
            ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelectorNone, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out Action selectedAction);
            Assert.IsFalse(selectedAction.IsCreated);

            // HighestScoringActionSelector
            ActionSelectors.HighestScoring actionSelectorHighest = new ActionSelectors.HighestScoring();
            ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelectorHighest, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out selectedAction);
            Assert.IsTrue(selectedAction.IsCreated);
            Assert.AreEqual(0, selectedAction.Type);

            // RandomWithinToleranceOfHighestScoringActionSelector
            uint prevRandomState = random.state;
            NativeList<Action> tmpActions = new NativeList<Action>(Allocator.Temp);
            ActionSelectors.RandomWithinToleranceOfHighestScoring actionSelectorRandomTol = new ActionSelectors.RandomWithinToleranceOfHighestScoring(0.15f, tmpActions, random);
            for (int i = 0; i < 50; i++)
            {
                ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelectorRandomTol, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out selectedAction);
                Assert.IsTrue(selectedAction.IsCreated);
                Assert.IsTrue(selectedAction.Score >= 0.89f);
            }
            random = actionSelectorRandomTol.Random;
            Assert.AreNotEqual(prevRandomState, random.state);

            // WeightedRandomActionSelector
            prevRandomState = random.state;
            ActionSelectors.WeightedRandom actionSelectorWeightedRandom = new ActionSelectors.WeightedRandom(tmpActions, random);
            for (int i = 0; i < 50; i++)
            {
                ReasonerUtilities.UpdateScoresAndSelectAction(ref actionSelectorWeightedRandom, ref reasoner, actionsBuffer, considerationsBuffer, considerationInputsBuffer, out selectedAction);
                Assert.IsTrue(selectedAction.IsCreated);
                Assert.IsTrue(selectedAction.Score >= 0.49f);
            }
            random = actionSelectorWeightedRandom.Random;
            Assert.AreNotEqual(prevRandomState, random.state);
            tmpActions.Dispose();
        }
    }
}