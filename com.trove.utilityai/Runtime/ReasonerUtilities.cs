using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Logging;

namespace Trove.UtilityAI
{
    public interface IActionSelector
    {
        public bool SelectAction(DynamicBuffer<Action> actionsBuffer, out Action action);
    }

    public static class ActionSelectors
    {
        [BurstCompile]
        public struct None : IActionSelector
        {
            [BurstCompile]
            public bool SelectAction(DynamicBuffer<Action> actionsBuffer, out Action action)
            {
                action = default;
                return true;
            }
        }

        [BurstCompile]
        public struct HighestScoring : IActionSelector
        {
            [BurstCompile]
            public bool SelectAction(DynamicBuffer<Action> actionsBuffer, out Action action)
            {
                action = default;
                action.ScoreMultiplier = 1f;
                action.__internal__latestScoreWithoutMultiplier = -1f;

                bool foundAction = false;
                for (int i = 0; i < actionsBuffer.Length; i++)
                {
                    Action tmpAction = actionsBuffer[i];
                    if (tmpAction.IsEnabled && tmpAction.Score > action.Score)
                    {
                        action = tmpAction;
                        foundAction = true;
                    }
                }

                return foundAction;
            }
        }

        /// <summary>
        /// The "Tolerance" is a percentage, where 0.2f would mean 20%, which would mean selecting a random action
        /// whose score is at least 80% of the highest scoring action's score (100% - 20% = 80%).
        /// </summary>
        [BurstCompile]
        public struct RandomWithinToleranceOfHighestScoring : IActionSelector
        {
            public float Tolerance;
            public NativeList<Action> TmpActions;
            public Random Random;

            public RandomWithinToleranceOfHighestScoring(float tolerance, NativeList<Action> tmpActions, Random random)
            {
                Tolerance = tolerance;
                TmpActions = tmpActions;
                Random = random;
            }

            [BurstCompile]
            public bool SelectAction(DynamicBuffer<Action> actionsBuffer, out Action action)
            {
                bool foundAction = false;
                action = default;
                action.ScoreMultiplier = 1f;
                action.__internal__latestScoreWithoutMultiplier = -1f;

                // Find highest scoring action
                Action highestScoringAction = default;
                highestScoringAction.ScoreMultiplier = 1f;
                highestScoringAction.__internal__latestScoreWithoutMultiplier = -1f;
                bool foundHighestScoringAction = false;
                for (int i = 0; i < actionsBuffer.Length; i++)
                {
                    Action tmpAction = actionsBuffer[i];
                    if (tmpAction.IsEnabled)
                    {
                        if (tmpAction.Score > action.Score)
                        {
                            highestScoringAction = tmpAction;
                            foundHighestScoringAction = true;
                        }
                    }
                }

                if(foundHighestScoringAction)
                {
                    float absoluteTolerance = (1f - Tolerance) * highestScoringAction.Score;
                    float lowestAllowedScore = highestScoringAction.Score - absoluteTolerance;

                    // Build list of enabled actions within tolerance
                    TmpActions.Clear();
                    for (int i = 0; i < actionsBuffer.Length; i++)
                    {
                        Action tmpAction = actionsBuffer[i];
                        if (tmpAction.IsEnabled)
                        {
                            if(tmpAction.Score >= lowestAllowedScore)
                            {
                                TmpActions.Add(tmpAction);
                            }
                        }
                    }

                    // Random in valid actions list
                    int randomIndex = Random.NextInt(0, TmpActions.Length);
                    action = TmpActions[randomIndex];
                    foundAction = true;
                }

                return foundAction;
            }
        }

        [BurstCompile]
        public struct WeightedRandom : IActionSelector
        {
            public NativeList<Action> TmpActions;
            public Random Random;

            public WeightedRandom(NativeList<Action> tmpActions, Random random)
            {
                TmpActions = tmpActions;
                Random = random;
            }

            [BurstCompile]
            public bool SelectAction(DynamicBuffer<Action> actionsBuffer, out Action action)
            {
                action = default;
                bool foundAction = false;

                float totalScores = 0f;
                bool hasAnyDisabledActions = false;
                bool hasAnyEnabledActions = false;
                for (int i = 0; i < actionsBuffer.Length; i++)
                {
                    Action tmpAction = actionsBuffer[i];
                    if (tmpAction.IsEnabled)
                    {
                        totalScores += actionsBuffer[i].Score;
                        hasAnyEnabledActions = true;
                    }
                    else
                    {
                        hasAnyDisabledActions = true;
                    }
                }

                if (hasAnyEnabledActions)
                {
                    if (totalScores > 0f)
                    {
                        if (hasAnyDisabledActions)
                        {
                            // Build enabled actions list
                            TmpActions.Clear();
                            for (int i = 0; i < actionsBuffer.Length; i++)
                            {
                                Action tmpAction = actionsBuffer[i];
                                if (tmpAction.IsEnabled)
                                {
                                    TmpActions.Add(tmpAction);
                                }
                            }

                            // Weighted random in enabled list
                            float randomVal = math.clamp(Random.NextFloat(0f, totalScores), 0f, totalScores - math.EPSILON);
                            randomVal *= ((float)TmpActions.Length / totalScores);
                            action = TmpActions[(int)math.floor(randomVal)];
                            foundAction = true;
                        }
                        else
                        {
                            // Weighted random in original list
                            float randomVal = math.clamp(Random.NextFloat(0f, totalScores), 0f, totalScores - math.EPSILON);
                            randomVal *= ((float)actionsBuffer.Length / totalScores);
                            action = actionsBuffer[(int)math.floor(randomVal)];
                            foundAction = true;
                        }
                    }
                    // If all scores are 0, then just pick random
                    else
                    {
                        if (hasAnyDisabledActions)
                        {
                            // Build enabled actions list
                            TmpActions.Clear();
                            for (int i = 0; i < actionsBuffer.Length; i++)
                            {
                                Action tmpAction = actionsBuffer[i];
                                if (tmpAction.IsEnabled)
                                {
                                    TmpActions.Add(tmpAction);
                                }
                            }

                            // Regular random in enabled list
                            int randomIndex = Random.NextInt(0, TmpActions.Length);
                            action = TmpActions[randomIndex];
                            foundAction = true;
                        }
                        else
                        {
                            // Regular random in original list
                            int randomIndex = Random.NextInt(0, actionsBuffer.Length);
                            action = actionsBuffer[randomIndex];
                            foundAction = true;
                        }
                    }
                }

                return foundAction;
            }
        }
    }

    public static class ReasonerUtilities
    {
        public const int IsCreatedBitPosition = 0;
        public const int IsEnabledBitPosition = 1;

        [BurstCompile]
        public unsafe static bool UpdateScoresAndSelectAction<TActionSelector>(
            ref TActionSelector actionSelector,
            ref Reasoner reasoner,
            DynamicBuffer<Action> actionsBuffer,
            DynamicBuffer<Consideration> considerationsBuffer,
            DynamicBuffer<ConsiderationInput> considerationInputsBuffer,
            out Action selectedAction) where TActionSelector : IActionSelector
        {
            bool FinalizeActionScore(double actionScore, double highestConsiderationScoreForAction, int highestActionConsiderationsCount, int actionConsiderationsCount, ref Action action)
            {
                if (Hint.Likely(actionConsiderationsCount > 0 && action.IsEnabled))
                {
                    // Score compensation
                    if (actionConsiderationsCount < highestActionConsiderationsCount)
                    {
                        actionScore *= math.pow(highestConsiderationScoreForAction, highestActionConsiderationsCount - actionConsiderationsCount);
                    }

                    action.__internal__latestScoreWithoutMultiplier = (float)actionScore;
                    return true;
                }

                return false;
            }

            bool success = false;
            selectedAction = default;

            // Check if we have to recompute some considerations data after a change 
            // (highest considerations count for a single action)
            if (Hint.Unlikely(reasoner.__internal__mustRecomputeHighestActionConsiderationsCount == 1))
            {
                reasoner.__internal__mustRecomputeHighestActionConsiderationsCount = 0;
                reasoner.__internal__highestActionConsiderationsCount = 0;

                int currentActionIndex = -1;
                int currentActionConsiderationsCount = 0;
                bool currentActionIsEnabled = false;
                for (int i = 0; i < considerationsBuffer.Length; i++)
                {
                    Consideration tmpConsideration = considerationsBuffer[i];

                    // Changes in affected actions
                    if (tmpConsideration.__internal__actionIndex != currentActionIndex)
                    {
                        reasoner.__internal__highestActionConsiderationsCount = math.max(reasoner.__internal__highestActionConsiderationsCount, currentActionConsiderationsCount);
                        currentActionIndex = tmpConsideration.__internal__actionIndex;
                        currentActionConsiderationsCount = 0;

                        currentActionIsEnabled = false;
                        if (currentActionIndex >= 0 && currentActionIndex < actionsBuffer.Length)
                        {
                            if (actionsBuffer[currentActionIndex].IsEnabled)
                            {
                                currentActionIsEnabled = true;
                            }
                        }
                    }

                    // Increment count if all enabled
                    if (currentActionIsEnabled && tmpConsideration.IsEnabled)
                    {
                        currentActionConsiderationsCount++;
                    }
                }
            }

            // Initialize actions
            for (int i = 0; i < actionsBuffer.Length; i++)
            {
                Action action = actionsBuffer[i];
                action.__internal__latestScoreWithoutMultiplier = 0f; // This must be done so that actions without any considerations will score 0
                actionsBuffer[i] = action;
            }

            // Add consideration scores to action scores
            // Considerations are always sorted by affected action index, so we don't have to read/write action in buffer every time
            int currentAffectedActionIndex = -1;
            int currentAffectedActionConsiderationsCount = 0;
            double currentAffectedActionScore = 0.0;
            double currentAffectedActionHighestConsiderationsScore = 0.0;
            Action currentAffectedAction = default;
            for (int i = considerationsBuffer.Length - 1; i >= 0; i--)
            {
                Consideration consideration = considerationsBuffer[i];
                if (Hint.Likely(consideration.__internal__actionIndex < actionsBuffer.Length))
                {
                    // Handle detecting that we're starting to iterate on considerations that are affecting a new action
                    if (consideration.__internal__actionIndex != currentAffectedActionIndex)
                    {
                        // When we're moving on to another action, write the current action in buffer
                        if (Hint.Likely(currentAffectedActionIndex >= 0 && currentAffectedActionIndex < actionsBuffer.Length))
                        {
                            //FinalizeActionScore(currentAffectedActionScore, currentAffectedActionHighestConsiderationsScore, reasoner.__internal__highestActionConsiderationsCount, currentAffectedActionConsiderationsCount, ref currentAffectedAction);
                            if (Hint.Likely(FinalizeActionScore(currentAffectedActionScore, currentAffectedActionHighestConsiderationsScore, reasoner.__internal__highestActionConsiderationsCount, currentAffectedActionConsiderationsCount, ref currentAffectedAction)))
                            {
                                actionsBuffer[currentAffectedActionIndex] = currentAffectedAction;
                            }
                        }

                        // Get the new current action + index
                        currentAffectedAction = actionsBuffer[consideration.__internal__actionIndex];
                        currentAffectedActionIndex = consideration.__internal__actionIndex;
                        currentAffectedActionScore = currentAffectedAction.IsEnabled ? 1.0 : 0.0;
                        currentAffectedActionConsiderationsCount = 0;
                        currentAffectedActionHighestConsiderationsScore = 0.0;
                    }

                    // Add to action score & data
                    if (Hint.Likely(currentAffectedAction.IsEnabled && consideration.IsEnabled))
                    {
                        double considerationScore = (double)consideration.Definition.Value.Curve.Evaluate(considerationInputsBuffer[i].__internal__input);

                        currentAffectedActionScore *= considerationScore;
                        currentAffectedActionConsiderationsCount++;
                        currentAffectedActionHighestConsiderationsScore = math.max(currentAffectedActionHighestConsiderationsScore, considerationScore);
                    }
                }
                else
                {
                    // Remove consideration if its action index is invalid
                    // (shouldn't happen unless the actions/considerations buffers were tampered with,
                    // without going through the APIs in this class)
                    considerationsBuffer.RemoveAt(i);
                    considerationInputsBuffer.RemoveAt(i);
                    reasoner.__internal__considerationsVersion++;
                }
            }

            // Write last current action to buffer
            if (Hint.Likely(currentAffectedActionIndex >= 0 && currentAffectedActionIndex < actionsBuffer.Length))
            {
                //FinalizeActionScore(currentAffectedActionScore, currentAffectedActionHighestConsiderationsScore, reasoner.__internal__highestActionConsiderationsCount, currentAffectedActionConsiderationsCount, ref currentAffectedAction);
                if (Hint.Likely(FinalizeActionScore(currentAffectedActionScore, currentAffectedActionHighestConsiderationsScore, reasoner.__internal__highestActionConsiderationsCount, currentAffectedActionConsiderationsCount, ref currentAffectedAction)))
                {
                    actionsBuffer[currentAffectedActionIndex] = currentAffectedAction;
                }
            }

            // Select action
            if (actionsBuffer.Length > 0)
            {
                success = actionSelector.SelectAction(actionsBuffer, out selectedAction);
            }

            return success;
        }

        [BurstCompile]
        public static void AddAction(
            ActionDefinition actionDefinition,
            bool isEnabled,
            ref Reasoner reasoner,
            DynamicBuffer<Action> actionsBuffer,
            out ActionReference actionReference)
        {
            actionReference = default;
            if (IncrementIDCounter(ref reasoner.__internal__actionIDCounter, ref reasoner.__internal__actionIDCounterHasLooped, actionsBuffer))
            {
                int newID = reasoner.__internal__actionIDCounter;

                // Create reference to this action
                actionReference = new ActionReference
                {
                    __internal__isCreated = (byte)1,
                    __internal__actionsVersion = reasoner.__internal__actionsVersion,
                    __internal__id = newID,
                    __internal__index = actionsBuffer.Length,
                };

                byte tmpFlags = 0;
                BitUtilities.SetBit(true, ref tmpFlags, IsCreatedBitPosition);
                BitUtilities.SetBit(isEnabled, ref tmpFlags, IsEnabledBitPosition);
                actionsBuffer.Add(new Action
                {
                    Type = actionDefinition.Type,
                    IndexInType = actionDefinition.Index,
                    ScoreMultiplier = actionDefinition.ScoreMultiplier,

                    __internal__flags = tmpFlags,
                    __internal__id = newID,
                    __internal__latestScoreWithoutMultiplier = 1f,
                });

                reasoner.__internal__mustRecomputeHighestActionConsiderationsCount = 1;

                // Adding actions cannot invalidate existing indexes since they're always added at the end.
                // No need to increment actions version.
            }
        }

        [BurstCompile]
        public static bool RemoveAction(
            ref ActionReference actionReference,
            ref Reasoner reasoner,
            DynamicBuffer<Action> actionsBuffer,
            DynamicBuffer<Consideration> considerationsBuffer,
            DynamicBuffer<ConsiderationInput> considerationInputsBuffer)
        {
            if (GetActionIndex(ref actionReference, in reasoner, actionsBuffer, out int actionIndex))
            {
                // Remove all considerations pointing to this action index
                int rangeSize = 0;
                for (int i = 0; i < considerationsBuffer.Length; i++)
                {
                    // Keep track of how many considerations of that id we've iterated through
                    if (considerationsBuffer[i].__internal__actionIndex == actionIndex)
                    {
                        rangeSize++;
                    }

                    // If there's an id change (or if we reached the end of buffer length) and we were already detecting matching considerations,
                    // we've reached the end of that action's considerations.
                    if ((considerationsBuffer[i].__internal__actionIndex != actionIndex || i >= considerationsBuffer.Length - 1) && rangeSize > 0)
                    {
                        considerationsBuffer.RemoveRange(i - rangeSize, rangeSize);
                        considerationInputsBuffer.RemoveRange(i - rangeSize, rangeSize);
                        break;
                    }
                }

                reasoner.__internal__considerationsVersion++;
                reasoner.__internal__mustRecomputeHighestActionConsiderationsCount = 1;

                // Remove action
                actionsBuffer.RemoveAtSwapBack(actionIndex);
                reasoner.__internal__actionsVersion++;

                return true;
            }

            return false;
        }

        [BurstCompile]
        public static bool AddConsideration(
            BlobAssetReference<ConsiderationDefinition> definition,
            ref ActionReference affectedAction,
            bool isEnabled,
            ref Reasoner reasoner,
            DynamicBuffer<Action> actionsBuffer,
            DynamicBuffer<Consideration> considerationsBuffer,
            DynamicBuffer<ConsiderationInput> considerationInputsBuffer,
            out ConsiderationReference considerationReference)
        {
            if (GetActionIndex(ref affectedAction, in reasoner, actionsBuffer, out int actionIndex))
            {
                if (IncrementIDCounter(ref reasoner.__internal__considerationIDCounter, ref reasoner.__internal__considerationIDCounterHasLooped, considerationsBuffer))
                {
                    int newID = reasoner.__internal__considerationIDCounter;

                    byte tmpFlags = 0;
                    BitUtilities.SetBit(true, ref tmpFlags, IsCreatedBitPosition);
                    BitUtilities.SetBit(isEnabled, ref tmpFlags, IsEnabledBitPosition);
                    Consideration consideration = new Consideration
                    {
                        Definition = definition,

                        __internal__flags = tmpFlags,
                        __internal__actionIndex = actionIndex,
                        __internal__id = newID,
                    };

                    // Insert in sorted order
                    int insertedIndex = -1;
                    for (int i = 0; i < considerationsBuffer.Length; i++)
                    {
                        if (considerationsBuffer[i].__internal__actionIndex == actionIndex)
                        {
                            insertedIndex = i;
                            considerationsBuffer.Insert(i, consideration);
                            considerationInputsBuffer.Insert(i, default);
                            reasoner.__internal__considerationsVersion++;
                            break;
                        }
                    }

                    // Simply add if not inserted
                    if (insertedIndex < 0)
                    {
                        insertedIndex = considerationsBuffer.Length;
                        considerationsBuffer.Add(consideration);
                        considerationInputsBuffer.Add(default);
                    }

                    // Create reference to this consideration
                    considerationReference = new ConsiderationReference
                    {
                        __internal__isCreated = (byte)1,
                        __internal__considerationsVersion = reasoner.__internal__considerationsVersion,
                        __internal__id = newID,
                        __internal__index = insertedIndex,
                    };

                    reasoner.__internal__mustRecomputeHighestActionConsiderationsCount = 1;

                    return true;
                }
            }

            considerationReference = default;
            return false;
        }

        [BurstCompile]
        public static void RemoveConsideration(
            ref ConsiderationReference considerationReference,
            ref Reasoner reasoner,
            DynamicBuffer<Consideration> considerationsBuffer,
            DynamicBuffer<ConsiderationInput> considerationInputsBuffer)
        {
            if (GetConsiderationIndex(ref considerationReference, in reasoner, considerationsBuffer, out int considerationIndex))
            {
                // Remove without swapback, to preserve order by affected action index
                considerationsBuffer.RemoveAt(considerationIndex);
                considerationInputsBuffer.RemoveAt(considerationIndex);
                reasoner.__internal__considerationsVersion++;
                reasoner.__internal__mustRecomputeHighestActionConsiderationsCount = 1;
            }
        }

        /// <summary>
        /// Note: Enabling/Disabling an action automatically takes care of enabling/disabling its associated considerations
        /// </summary>
        [BurstCompile]
        public static bool SetActionData(
            ref ActionReference actionReference,
            ActionDefinition actionDefinition,
            bool isEnabled,
            ref Reasoner reasoner,
            DynamicBuffer<Action> actionsBuffer)
        {
            if (GetActionIndex(ref actionReference, in reasoner, actionsBuffer, out int actionIndex))
            {
                Action action = actionsBuffer[actionIndex];
                bool enabledStatusChanged = action.IsEnabled != isEnabled;
                action.IsEnabled = isEnabled;
                action.Type = actionDefinition.Type;
                action.IndexInType = actionDefinition.Index;
                action.ScoreMultiplier = actionDefinition.ScoreMultiplier;
                actionsBuffer[actionIndex] = action;

                if (enabledStatusChanged)
                {
                    reasoner.__internal__mustRecomputeHighestActionConsiderationsCount = 1;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Note: Enabling/Disabling an action automatically takes care of enabling/disabling its associated considerations
        /// </summary>
        [BurstCompile]
        public static bool SetActionEnabled(
            ref ActionReference actionReference,
            bool isEnabled,
            ref Reasoner reasoner,
            DynamicBuffer<Action> actionsBuffer)
        {
            if (GetActionIndex(ref actionReference, in reasoner, actionsBuffer, out int actionIndex))
            {
                Action action = actionsBuffer[actionIndex];
                bool enabledStatusChanged = action.IsEnabled != isEnabled;
                action.IsEnabled = isEnabled;
                actionsBuffer[actionIndex] = action;

                if (enabledStatusChanged)
                {
                    reasoner.__internal__mustRecomputeHighestActionConsiderationsCount = 1;
                }

                return true;
            }

            return false;
        }

        [BurstCompile]
        public static bool SetActionScoreMultiplier(
            ref ActionReference actionReference,
            float scoreMultiplier,
            in Reasoner reasoner,
            DynamicBuffer<Action> actionsBuffer)
        {
            if (GetActionIndex(ref actionReference, in reasoner, actionsBuffer, out int actionIndex))
            {
                Action action = actionsBuffer[actionIndex];
                action.ScoreMultiplier = scoreMultiplier;
                actionsBuffer[actionIndex] = action;
                return true;
            }

            return false;
        }

        [BurstCompile]
        public static bool SetConsiderationInput(
            ref ConsiderationReference considerationReference,
            float normalizedInput,
            in Reasoner reasoner,
            DynamicBuffer<Consideration> considerationsBuffer,
            DynamicBuffer<ConsiderationInput> considerationInputsBuffer)
        {
            if (GetConsiderationIndex(ref considerationReference, in reasoner, considerationsBuffer, out int considerationIndex))
            {
                considerationInputsBuffer[considerationIndex] = new ConsiderationInput { __internal__input = normalizedInput };
                return true;
            }

            return false;
        }

        [BurstCompile]
        public static bool SetConsiderationData(
            ref ConsiderationReference considerationReference,
            BlobAssetReference<ConsiderationDefinition> definition,
            bool isEnabled,
            ref Reasoner reasoner,
            DynamicBuffer<Consideration> considerationsBuffer)
        {
            if (GetConsiderationIndex(ref considerationReference, in reasoner, considerationsBuffer, out int considerationIndex))
            {
                Consideration consideration = considerationsBuffer[considerationIndex];
                bool enabledStatusChanged = consideration.IsEnabled != isEnabled;
                consideration.IsEnabled = isEnabled;
                consideration.Definition = definition;
                considerationsBuffer[considerationIndex] = consideration;

                if (enabledStatusChanged)
                {
                    reasoner.__internal__mustRecomputeHighestActionConsiderationsCount = 1;
                }

                return true;
            }

            return false;
        }

        [BurstCompile]
        public static bool SetConsiderationEnabled(
            ref ConsiderationReference considerationReference,
            bool isEnabled,
            ref Reasoner reasoner,
            DynamicBuffer<Consideration> considerationsBuffer)
        {
            if (GetConsiderationIndex(ref considerationReference, in reasoner, considerationsBuffer, out int considerationIndex))
            {
                Consideration consideration = considerationsBuffer[considerationIndex];
                bool enabledStatusChanged = consideration.IsEnabled != isEnabled;
                consideration.IsEnabled = isEnabled;
                considerationsBuffer[considerationIndex] = consideration;

                if (enabledStatusChanged)
                {
                    reasoner.__internal__mustRecomputeHighestActionConsiderationsCount = 1;
                }

                return true;
            }

            return false;
        }

        [BurstCompile]
        public static bool FindNextEnabledAction(
            int startingIndex, 
            DynamicBuffer<Action> actionsBuffer, 
            out int enabledActionIndex)
        {
            int currentIndex = startingIndex;
            int iteratedCount = 0;

            while(iteratedCount < actionsBuffer.Length)
            {
                if (currentIndex >= actionsBuffer.Length)
                {
                    currentIndex = 0;
                }

                if(actionsBuffer[currentIndex].IsEnabled)
                {
                    enabledActionIndex = currentIndex;
                    return true;
                }

                currentIndex++;
                iteratedCount++;
            }

            enabledActionIndex = -1;
            return false;
        }

        [BurstCompile]
        private static void SetConsiderationsEnabledForAction( 
            int actionIndex,
            bool isEnabled,
            ref Reasoner reasoner,
            DynamicBuffer<Consideration> considerationsBuffer)
        {
            for (int i = 0; i < considerationsBuffer.Length; i++)
            {
                Consideration consideration = considerationsBuffer[i];
                if (consideration.__internal__actionIndex == actionIndex)
                {
                    consideration.IsEnabled = isEnabled;
                    considerationsBuffer[i] = consideration;
                }
            }

            reasoner.__internal__mustRecomputeHighestActionConsiderationsCount = 1;
        }

        [BurstCompile]
        public static bool GetActionIndex(
            ref ActionReference actionReference,
            in Reasoner reasoner,
            DynamicBuffer<Action> actionsBuffer,
            out int actionIndex)
        {
            if (actionReference.IsCreated)
            {
                if (actionReference.__internal__actionsVersion == reasoner.__internal__actionsVersion)
                {
                    actionIndex = actionReference.__internal__index;
                    return true;
                }

                // Update version
                actionReference.__internal__actionsVersion = reasoner.__internal__actionsVersion;

                // Find by ID
                for (int i = 0; i < actionsBuffer.Length; i++)
                {
                    Action tmpAction = actionsBuffer[i];
                    if (tmpAction.__internal__id == actionReference.__internal__id)
                    {
                        actionReference.__internal__index = i;
                        actionIndex = i;
                        return true;
                    }
                }
            }

            actionIndex = -1;
            return false;
        }

        [BurstCompile]
        public static bool GetConsiderationIndex(
            ref ConsiderationReference considerationReference,
            in Reasoner reasoner,
            DynamicBuffer<Consideration> considerationsBuffer,
            out int considerationIndex)
        {
            if (considerationReference.IsCreated)
            {
                if (considerationReference.__internal__considerationsVersion == reasoner.__internal__considerationsVersion)
                {
                    considerationIndex = considerationReference.__internal__index;
                    return true;
                }

                // Update version
                considerationReference.__internal__considerationsVersion = reasoner.__internal__considerationsVersion;

                // Find by ID 
                for (int i = 0; i < considerationsBuffer.Length; i++)
                {
                    Consideration tmpConsideration = considerationsBuffer[i];
                    if (tmpConsideration.__internal__id == considerationReference.__internal__id)
                    {
                        //consideration = tmpConsideration;
                        considerationReference.__internal__index = i;
                        considerationIndex = i;
                        return true;
                    }
                }
            }

            //consideration = default;
            considerationIndex = -1;
            return false;
        }

        private static bool IncrementIDCounter<T>(ref int idCounter, ref byte counterHasLooped, DynamicBuffer<T> idElementBuffer) where T : unmanaged, IBufferElementData, IIDElement
        {
            // Increment ID counter
            idCounter++;

            // Detect having looped around our ID counter values (which means the new ID could already be in use)
            if (Hint.Unlikely(idCounter == 0))
            {
                counterHasLooped = 1;
            }

            // Handle validating that the new ID isn't in use if we've had a loop around, and increment ID accordingly
            if (Hint.Unlikely(counterHasLooped == 1))
            {
                int indexOfExistingID = -1;
                NativeArray<int> existingIDs = new NativeArray<int>(idElementBuffer.Length, Allocator.Temp);
                for (int i = 0; i < idElementBuffer.Length; i++)
                {
                    int tmpID = idElementBuffer[i].ID;
                    if (tmpID == idCounter)
                    {
                        indexOfExistingID = i;
                    }
                    existingIDs[i] = tmpID;
                }

                // The new incremented ID was not found, so we can go with this one
                if (indexOfExistingID == -1)
                {
                    return true;
                }
                // TODO: we can probably do better, although the real solution would be to avoid this situation completely with pooling.
                // Build a sorted array of existing IDs, and find a new ID that isn't in there
                else
                {
                    existingIDs.Sort();
                    for (int i = 0; i < existingIDs.Length; i++)
                    {
                        int checkedIndex = indexOfExistingID + i;
                        if (checkedIndex >= existingIDs.Length)
                        {
                            checkedIndex -= existingIDs.Length;
                        }

                        if (existingIDs[checkedIndex] == idCounter)
                        {
                            idCounter++;
                        }
                        else
                        {
                            // our incremented ID counter doesn't correspond to next ID in sorted existing IDs. This means that ID is available
                            return true;
                        }
                    }

                    // If we haven't found an ID by now, it means all possible IDs are already in use
                    Log.Error("Failed to find a valid ID. All possible IDs are already in use");
                    return false;
                }
            }
            // If the counter hasn't looped, the ID is guaranteed to be valid
            else
            {
                return true;
            }
        }

        public static void BeginBakeReasoner(IBaker baker, out Reasoner reasoner, out DynamicBuffer<Action> actionsBuffer, out DynamicBuffer<Consideration> considerationsBuffer, out DynamicBuffer<ConsiderationInput> considerationInputsBuffer)
        {
            reasoner = new Reasoner();
            actionsBuffer = baker.AddBuffer<Action>();
            considerationsBuffer = baker.AddBuffer<Consideration>();
            considerationInputsBuffer = baker.AddBuffer<ConsiderationInput>();
        }

        public static void EndBakeReasoner(IBaker baker, Reasoner reasoner)
        {
            baker.AddComponent(baker.GetEntity(TransformUsageFlags.None), reasoner);
        }

        public static void CreateReasoner(EntityManager entityManager, Entity onEntity)
        {
            entityManager.AddComponentData(onEntity, new Reasoner());
            entityManager.AddBuffer<Action>(onEntity);
            entityManager.AddBuffer<Consideration>(onEntity);
            entityManager.AddBuffer<ConsiderationInput>(onEntity);
        }

        public static void CreateReasoner(EntityCommandBuffer ecb, Entity onEntity)
        {
            ecb.AddComponent(onEntity, new Reasoner());
            ecb.AddBuffer<Action>(onEntity);
            ecb.AddBuffer<Consideration>(onEntity);
            ecb.AddBuffer<ConsiderationInput>(onEntity);
        }

        public static void CreateReasoner(EntityCommandBuffer.ParallelWriter ecb, int sortKey, Entity onEntity)
        {
            ecb.AddComponent(sortKey, onEntity, new Reasoner());
            ecb.AddBuffer<Action>(sortKey, onEntity);
            ecb.AddBuffer<Consideration>(sortKey, onEntity);
            ecb.AddBuffer<ConsiderationInput>(sortKey, onEntity);
        }
    }
}