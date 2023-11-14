using Unity.Entities;
using Unity.Mathematics;
using System;
using Trove.PolymorphicElements;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.InteropServices;

/*
 * TODO STATS:
 *  - We need to not "search" for stats all the time. Solution ideas:
 *      - Have a stats buffer version. We can store
 *  - Support for Add/Remove stat at runtime
 *  - Strategy for reactively writing stat data to components when it changes?
 *      - pass some kind of TStatChangeHandler to the Handler?
 *      - Pass an optional NativeList of changed statReferences?
 * 
 * TODO POLYMORPH: 
 *  - Revisit my codegen of functions for the manager (use my new Generics support)
 *  - Remove generation of AddElem/IsertElemen from Manager (and review docs)
 *  - Maybe some kind of UnionElement to InitialStruct, so we can author modifiers with Union Elem but bake it to variable size?
 *  
*/

namespace Trove.Stats
{
    /// <summary>
    /// Buffer of bytes representing stats data
    /// 
    /// The layout of the data is as follows:
    /// - DataLayoutVersion 
    /// - ModifierIDCounter
    /// - StatsCount
    /// - sequence of StatTypeID + StartByteIndex, sorted by StartByteIndex in ascending order
    /// - sequence of variable-size stat data:
    ///     - Stat base value 
    ///     - Stat final value
    ///     - ObserversCount
    ///     - sequence of StatReferences representing observers of this stat
    ///     - ModifiersCount
    ///     - sequence of ModifierID + startByteIndex
    ///     - sequence of ModifierTypeID + variable-sized Modifier data (modifiers are polymorphic elements)
    ///     
    /// </summary>
    public struct StatsData : IBufferElementData, IByteBufferElement
    {
        public byte Data;
    }

    public struct StatDefinition
    {
        public ushort TypeID;
        public float StartValue;
    }

    public struct StatReference
    {
        public Entity Entity;
        public ushort StatTypeID;

        internal byte HasCachedData;
        internal int CachedStatStartByteIndex;
        internal int CachedDataLayoutVersion;

        public StatReference(Entity onEntity, ushort statTypeID)
        {
            Entity = onEntity;
            StatTypeID = statTypeID;

            HasCachedData = 0;
            CachedStatStartByteIndex = 0;
            CachedDataLayoutVersion = 0;
        }

        public static bool operator ==(StatReference a, StatReference b)
        {
            return b.Entity == a.Entity && b.StatTypeID == a.StatTypeID;
        }

        public static bool operator !=(StatReference a, StatReference b)
        {
            return b.Entity != a.Entity || b.StatTypeID != a.StatTypeID;
        }
    }

    public struct StatModifierReference
    {
        public Entity Entity;
        public ushort StatTypeID;
        public int ModifierID;

        internal byte HasCachedData;
        internal int CachedStatStartByteIndex;
        internal int CachedStatModifierStartByteIndex;
        internal int CachedDataLayoutVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StatValues
    {
        public float BaseValue;
        public float Value;
    }

    public interface IBaseStatModifier<TModifiersStack> where TModifiersStack : unmanaged, IStatModifiersStack
    {
        public void Apply(ref TModifiersStack stack);
    }

    public interface IStatModifiersStack
    {
        public void Reset();
        public void Apply(float statBaseValue, ref float statValue);
    }

    public interface IModifiersApplier<TModifiersStack> where TModifiersStack : unmanaged, IStatModifiersStack
    {
        public void ApplyModifiers(ref DynamicBuffer<StatsData> statsDataBuffer, ref TModifiersStack modifiersStack, int startByteIndex, int count);
    }

    public unsafe struct BaseStatsHandler<TModifier, TModifierStack, TModifiersApplier>
            where TModifier : unmanaged, IBaseStatModifier<TModifierStack>, IPolymorphicUnionElement
            where TModifierStack : unmanaged, IStatModifiersStack
            where TModifiersApplier : unmanaged, IModifiersApplier<TModifierStack>
    {
        // Global data
        private const int SizeOfDataLayoutVersion = sizeof(int);
        private const int SizeOfModifierIDCounter = sizeof(int);
        private const int SizeOfStatsCount = sizeof(ushort);

        // Stats map
        private const int SizeOfStatTypeID = sizeof(ushort);
        private const int SizeOfStatStartByteIndex = sizeof(int);

        // Stat datas
        private static int SizeOfStatValues = sizeof(StatValues);
        private const int SizeOfObserversCount = sizeof(int);
        private static int SizeOfObserver = sizeof(StatReference);
        private const int SizeOfModifiersCount = sizeof(int);
        private const int SizeOfModifierID = sizeof(int);
        private const int SizeOfModifierStartByteIndex = sizeof(int);
        // And finally, variable-sized modifiers

        private const int StartByteIndexOfStatsCount = SizeOfDataLayoutVersion + SizeOfModifierIDCounter;

        public void InitializeStatsData(ref DynamicBuffer<StatsData> statsDataBuffer, in NativeList<StatDefinition> statDefinitions)
        {
            statsDataBuffer.Clear();
            int writeByteIndex = 0;

            // TODO; filter out duplicate stats?

            // Data layout version
            PolymorphicElementsUtility.InternalUse.WriteAny(ref statsDataBuffer, writeByteIndex, (int)0);
            writeByteIndex += SizeOfDataLayoutVersion;

            // Modifier id counter
            PolymorphicElementsUtility.InternalUse.WriteAny(ref statsDataBuffer, writeByteIndex, (int)0);
            writeByteIndex += SizeOfModifierIDCounter;

            // Stats count
            PolymorphicElementsUtility.InternalUse.WriteAny(ref statsDataBuffer, writeByteIndex, (ushort)statDefinitions.Length);
            writeByteIndex += SizeOfStatsCount;

            // Add sequence of stat type IDs and start byte index
            int statsDataStartByteIndex = writeByteIndex + (statDefinitions.Length * (SizeOfStatTypeID + SizeOfStatStartByteIndex));
            for (int i = 0; i < statDefinitions.Length; i++)
            {
                // Stat type ID
                PolymorphicElementsUtility.InternalUse.WriteAny(ref statsDataBuffer, writeByteIndex, statDefinitions[i].TypeID);
                writeByteIndex += SizeOfStatTypeID;

                // Stat start byte index
                PolymorphicElementsUtility.InternalUse.WriteAny(ref statsDataBuffer, writeByteIndex, statsDataStartByteIndex + (i * (SizeOfStatValues + SizeOfObserversCount + SizeOfModifiersCount)));
                writeByteIndex += SizeOfStatStartByteIndex;
            }

            // Add sequence of stat base datas
            for (int i = 0; i < statDefinitions.Length; i++)
            {
                StatDefinition statDefinition = statDefinitions[i];

                // Values
                StatValues statValues = new StatValues
                {
                    BaseValue = statDefinition.StartValue,
                    Value = statDefinition.StartValue,
                };
                PolymorphicElementsUtility.InternalUse.WriteAny(ref statsDataBuffer, writeByteIndex, statValues);
                writeByteIndex += SizeOfStatValues;

                // Observers count
                PolymorphicElementsUtility.InternalUse.WriteAny(ref statsDataBuffer, writeByteIndex, (int)0);
                writeByteIndex += SizeOfObserversCount;

                // Modifiers count
                PolymorphicElementsUtility.InternalUse.WriteAny(ref statsDataBuffer, writeByteIndex, (int)0);
                writeByteIndex += SizeOfModifiersCount;
            }
        }

        public int GetDataLayoutVersion(ref DynamicBuffer<StatsData> statsDataBuffer)
        {
            PolymorphicElementsUtility.InternalUse.ReadAny(ref statsDataBuffer, 0, out _, out int result);
            return result;
        }

        public bool IncrementDataLayoutVersion(ref DynamicBuffer<StatsData> statsDataBuffer)
        {
            ref int dataLayoutVersion = ref PolymorphicElementsUtility.InternalUse.ReadAnyAsRef<int, StatsData>(ref statsDataBuffer, 0, out _, out bool success);
            if (success)
            {
                dataLayoutVersion++;
                return true;
            }

            return false;
        }

        private bool GetAndIncrementModifierIDCounter(ref DynamicBuffer<StatsData> statsDataBuffer, out int modifierID)
        {
            ref int modIDCounter = ref PolymorphicElementsUtility.InternalUse.ReadAnyAsRef<int, StatsData>(ref statsDataBuffer, SizeOfDataLayoutVersion, out _, out bool success);
            if (success)
            {
                modIDCounter++;
                modifierID = modIDCounter;
                return true;
            }

            modifierID = 0;
            return false;
        }

        private bool FindStatStartByteIndex(ref DynamicBuffer<StatsData> statsDataBuffer, ushort statTypeID, out int statStartByteIndex)
        {
            int readByteIndex = StartByteIndexOfStatsCount;

            // Read stats count
            if (PolymorphicElementsUtility.InternalUse.ReadAny(ref statsDataBuffer, readByteIndex, out readByteIndex, out ushort statsCount))
            {
                // For each stat
                for (int i = 0; i < statsCount; i++)
                {
                    // Read stat type ID
                    if (PolymorphicElementsUtility.InternalUse.ReadAny(ref statsDataBuffer, readByteIndex, out readByteIndex, out ushort otherStatTypeId))
                    {
                        // If it's the same as the id we're looking for...
                        if (otherStatTypeId == statTypeID)
                        {
                            // Read start byte index
                            if (PolymorphicElementsUtility.InternalUse.ReadAny(ref statsDataBuffer, readByteIndex, out readByteIndex, out statStartByteIndex))
                            {
                                return true;
                            }
                            else
                            {
                                // Couldn't read the start byte index of the matched stat. Not supposed to happen
                                return false;
                            }
                        }
                        readByteIndex += SizeOfStatTypeID;
                    }
                    else
                    {
                        // Couldn't read the stat type ID. Not supposed to happen
                        statStartByteIndex = default;
                        return false;
                    }
                }
            }

            // The stat doesn't exist
            statStartByteIndex = default;
            return false;
        }

        private bool FindObserversCountAndStartByteIndex(ref DynamicBuffer<StatsData> statsDataBuffer, int statStartByteIndex, out int observersCount, out int observersStartByteIndex)
        {
            int readByteIndex = statStartByteIndex;

            // Skip stat values
            readByteIndex += SizeOfStatValues;

            // Read observers count and outputted next byte index
            if (PolymorphicElementsUtility.InternalUse.ReadAny(ref statsDataBuffer, readByteIndex, out observersStartByteIndex, out observersCount))
            {
                return true;
            }

            // Couldnt read modifiers & observers info
            observersStartByteIndex = default;
            return false;
        }

        private bool FindModifiersCountAndStartByteIndexes(
            ref DynamicBuffer<StatsData> statsDataBuffer,
            int statStartByteIndex,
            out int observersCount,
            out int observersStartByteIndex,
            out int modifiersCount,
            out int modifiersMapStartByteIndex,
            out int modifiersStartByteIndex)
        {
            if (FindObserversCountAndStartByteIndex(ref statsDataBuffer, statStartByteIndex, out observersCount, out observersStartByteIndex))
            {
                // Skip observers
                int readByteIndex = observersStartByteIndex + (observersCount * SizeOfObserver);

                // Read modifiers count
                if (PolymorphicElementsUtility.InternalUse.ReadAny(ref statsDataBuffer, readByteIndex, out modifiersMapStartByteIndex, out modifiersCount))
                {
                    modifiersStartByteIndex = modifiersMapStartByteIndex + (modifiersCount * (SizeOfModifierID + SizeOfModifierStartByteIndex));
                    return true;
                }
            }

            // Couldnt read modifiers & observers info
            modifiersCount = default;
            modifiersMapStartByteIndex = default;
            modifiersStartByteIndex = default;
            return false;
        }

        private bool FindModifierStartByteIndex(ref DynamicBuffer<StatsData> statsDataBuffer, int statStartByteIndex, int modifierID, out int modifierStartByteIndex)
        {
            if (FindModifiersCountAndStartByteIndexes(ref statsDataBuffer, statStartByteIndex, out int observersCount, out int observersStartByteIndex, out int modifiersCount, out int modifiersMapStartByteIndex, out int modifiersStartByteIndex))
            {
                int readByteIndex = modifiersMapStartByteIndex;

                // For each modifier in ID map
                for (int i = 0; i < modifiersCount; i++)
                {
                    // Read modifier ID
                    if (PolymorphicElementsUtility.InternalUse.ReadAny(ref statsDataBuffer, readByteIndex, out readByteIndex, out int otherModifierID))
                    {
                        // If it's the one we're looking for
                        if (otherModifierID == modifierID)
                        {
                            // Read start byte index
                            if (PolymorphicElementsUtility.InternalUse.ReadAny(ref statsDataBuffer, readByteIndex, out readByteIndex, out modifierStartByteIndex))
                            {
                                return true;
                            }
                            else
                            {
                                // Couldn't read the start byte index of the matched modifier. Not supposed to happen
                                return false;
                            }
                        }
                    }
                    else
                    {
                        // Couldn't read modifier ID
                        modifierStartByteIndex = default;
                        return false;
                    }
                }
            }

            // Couldnt read modifiers & observers info
            modifierStartByteIndex = default;
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="statsDataBuffer"></param>
        /// <param name="statReference"></param>
        /// <returns>false if the reference couldn't be solved</returns>
        private bool UpdateStatReferenceCachedData(ref DynamicBuffer<StatsData> statsDataBuffer, ref StatReference statReference)
        {
            int dataLayoutVersion = GetDataLayoutVersion(ref statsDataBuffer);

            // Update cache data
            if (statReference.CachedDataLayoutVersion != dataLayoutVersion || statReference.HasCachedData == 0)
            {
                if (FindStatStartByteIndex(ref statsDataBuffer, statReference.StatTypeID, out int statStartByteIndex))
                {
                    statReference.HasCachedData = 1;
                    statReference.CachedStatStartByteIndex = statStartByteIndex;
                    statReference.CachedDataLayoutVersion = dataLayoutVersion;
                }
                else
                {
                    // Couldn't find stat
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="statsDataBuffer"></param>
        /// <param name="modifierReference"></param>
        /// <returns>false if the reference couldn't be solved</returns>
        private bool UpdateModifierReferenceCachedData(ref DynamicBuffer<StatsData> statsDataBuffer, ref StatModifierReference modifierReference)
        {
            int dataLayoutVersion = GetDataLayoutVersion(ref statsDataBuffer);

            // Update cache data
            if (modifierReference.CachedDataLayoutVersion != dataLayoutVersion || modifierReference.HasCachedData == 0)
            {
                if (FindStatStartByteIndex(ref statsDataBuffer, modifierReference.StatTypeID, out int statStartByteIndex))
                {
                    if (FindModifierStartByteIndex(ref statsDataBuffer, modifierReference.StatTypeID, modifierReference.ModifierID, out int modifierStartByteIndex))
                    {
                        modifierReference.HasCachedData = 1;
                        modifierReference.CachedStatStartByteIndex = statStartByteIndex;
                        modifierReference.CachedStatModifierStartByteIndex = modifierStartByteIndex;
                        modifierReference.CachedDataLayoutVersion = dataLayoutVersion;
                    }
                    else
                    {
                        // Couldn't find modifier
                        return false;
                    }
                }
                else
                {
                    // Couldn't find stat
                    return false;
                }
            }

            return true;
        }

        public bool GetStatValues(ref BufferLookup<StatsData> statsDataBufferLookup, ref StatReference statReference, out StatValues statValues)
        {
            if (statsDataBufferLookup.TryGetBuffer(statReference.Entity, out DynamicBuffer<StatsData> statsDataBuffer))
            {
                return GetStatValues(ref statsDataBuffer, ref statReference, out statValues);
            }

            statValues = default;
            return false;
        }

        public bool GetStatValues(ref DynamicBuffer<StatsData> statsDataBuffer, ref StatReference statReference, out StatValues statValues)
        {
            if (UpdateStatReferenceCachedData(ref statsDataBuffer, ref statReference))
            {
                int readByteIndex = statReference.CachedStatStartByteIndex;
                if (PolymorphicElementsUtility.InternalUse.ReadAny(ref statsDataBuffer, readByteIndex, out readByteIndex, out statValues))
                {
                    return true;
                }
            }

            statValues = default;
            return false;
        }

        public bool SetStatBaseValue(ref BufferLookup<StatsData> statsDataBufferLookup, ref StatReference statReference, float baseValue)
        {
            if (statsDataBufferLookup.TryGetBuffer(statReference.Entity, out DynamicBuffer<StatsData> statsDataBuffer))
            {
                return SetStatBaseValue(ref statsDataBuffer, ref statReference, baseValue);
            }

            return false;
        }

        public bool SetStatBaseValue(ref DynamicBuffer<StatsData> statsDataBuffer, ref StatReference statReference, float baseValue)
        {
            if (UpdateStatReferenceCachedData(ref statsDataBuffer, ref statReference))
            {
                int readByteIndex = statReference.CachedStatStartByteIndex;
                ref StatValues statValues = ref PolymorphicElementsUtility.InternalUse.ReadAnyAsRef<StatValues, StatsData>(ref statsDataBuffer, readByteIndex, out _, out bool success);
                if (success)
                {
                    statValues.BaseValue = baseValue;
                    return true;
                }
            }

            return false;
        }

        public bool AddStatBaseValue(ref BufferLookup<StatsData> statsDataBufferLookup, ref StatReference statReference, float value)
        {
            if (statsDataBufferLookup.TryGetBuffer(statReference.Entity, out DynamicBuffer<StatsData> statsDataBuffer))
            {
                return AddStatBaseValue(ref statsDataBuffer, ref statReference, value);
            }

            return false;
        }

        public bool AddStatBaseValue(ref DynamicBuffer<StatsData> statsDataBuffer, ref StatReference statReference, float value)
        {
            if (UpdateStatReferenceCachedData(ref statsDataBuffer, ref statReference))
            {
                int readByteIndex = statReference.CachedStatStartByteIndex;
                ref StatValues statValues = ref PolymorphicElementsUtility.InternalUse.ReadAnyAsRef<StatValues, StatsData>(ref statsDataBuffer, readByteIndex, out _, out bool success);
                if (success)
                {
                    statValues.BaseValue += value;
                    return true;
                }
            }

            return false;
        }

        public bool RecalculateStat(ref BufferLookup<StatsData> statsDataBufferLookup, ref StatReference statReference)
        {
            if (statsDataBufferLookup.TryGetBuffer(statReference.Entity, out DynamicBuffer<StatsData> statsDataBuffer))
            {
                return RecalculateStat(ref statsDataBufferLookup, ref statsDataBuffer, ref statReference);
            }

            return false;
        }

        public bool RecalculateStat(ref BufferLookup<StatsData> statsDataBufferLookup, ref DynamicBuffer<StatsData> statsDataBuffer, ref StatReference statReference)
        {
            if (UpdateStatReferenceCachedData(ref statsDataBuffer, ref statReference))
            {
                // Create modifiers stack
                TModifierStack modifierStack = new TModifierStack();
                modifierStack.Reset();

                // Get modifiers count and start indexes so we know which modifiers to apply
                if (FindModifiersCountAndStartByteIndexes(ref statsDataBuffer, statReference.CachedStatStartByteIndex, out int observersCount, out int observersStartByteIndex, out int modifiersCount, out int modifiersMapStartByteIndex, out int modifiersStartByteIndex))
                {
                    // Apply all modifiers to stack
                    TModifiersApplier modifiersApplier = new TModifiersApplier();
                    modifiersApplier.ApplyModifiers(ref statsDataBuffer, ref modifierStack, modifiersStartByteIndex, modifiersCount);

                    // Apply stack to stat values
                    int readByteIndex = statReference.CachedStatStartByteIndex;
                    ref StatValues statValues = ref PolymorphicElementsUtility.InternalUse.ReadAnyAsRef<StatValues, StatsData>(ref statsDataBuffer, readByteIndex, out _, out bool success);
                    if (success)
                    {
                        float prevValue = statValues.Value;
                        statValues.Value = statValues.BaseValue;
                        modifierStack.Apply(statValues.BaseValue, ref statValues.Value);

                        // Recalculate observers if value changed
                        if (statValues.Value != prevValue)
                        {
                            readByteIndex = observersStartByteIndex;
                            for (int i = 0; i < observersCount; i++)
                            {
                                if (PolymorphicElementsUtility.InternalUse.ReadAny(ref statsDataBuffer, readByteIndex, out readByteIndex, out StatReference observerOfStat))
                                {
                                    // Local observer
                                    if (observerOfStat.Entity == statReference.Entity)
                                    {
                                        RecalculateStat(ref statsDataBufferLookup, ref statsDataBuffer, ref observerOfStat);
                                    }
                                    // Remote observer
                                    else
                                    {
                                        RecalculateStat(ref statsDataBufferLookup, ref observerOfStat);
                                    }
                                }
                            }
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsStatAInStatBsObserversChain(ref BufferLookup<StatsData> statsDataBufferLookup, ref DynamicBuffer<StatsData> statADataBuffer, ref StatReference statA, ref StatReference statB)
        {
            if (statsDataBufferLookup.TryGetBuffer(statB.Entity, out DynamicBuffer<StatsData> statBDataBuffer))
            {
                return IsStatAInStatBsObserversChain(ref statsDataBufferLookup, ref statADataBuffer, ref statBDataBuffer, ref statA, ref statB);
            }

            return false;
        }

        private bool IsStatAInStatBsObserversChain(ref BufferLookup<StatsData> statsDataBufferLookup, ref DynamicBuffer<StatsData> statADataBuffer, ref DynamicBuffer<StatsData> statBDataBuffer, ref StatReference statA, ref StatReference statB)
        {
            // Assume statA is up to date
            if (UpdateStatReferenceCachedData(ref statADataBuffer, ref statB))
            {
                // Check if stat A is in observers of stat B
                if (FindObserversCountAndStartByteIndex(ref statADataBuffer, statB.CachedStatStartByteIndex, out int observersCount, out int observersStartByteIndex))
                {
                    int readByteIndex = observersStartByteIndex;
                    for (int i = 0; i < observersCount; i++)
                    {
                        if (PolymorphicElementsUtility.InternalUse.ReadAny(ref statADataBuffer, readByteIndex, out readByteIndex, out StatReference observerOfStatB))
                        {
                            if (observerOfStatB == statA)
                            {
                                return true;
                            }
                            // Check if statA is an observer of the observer of statB
                            else
                            {
                                // Observer is local to B
                                if (observerOfStatB.Entity == statB.Entity)
                                {
                                    if (IsStatAInStatBsObserversChain(ref statsDataBufferLookup, ref statADataBuffer, ref statBDataBuffer, ref statA, ref observerOfStatB))
                                    {
                                        return true;
                                    }
                                }
                                // Observer is local to A
                                else if (observerOfStatB.Entity == statA.Entity)
                                {
                                    if (IsStatAInStatBsObserversChain(ref statsDataBufferLookup, ref statADataBuffer, ref statADataBuffer, ref statA, ref observerOfStatB))
                                    {
                                        return true;
                                    }
                                }
                                // Observer is other entity
                                else
                                {
                                    if (IsStatAInStatBsObserversChain(ref statsDataBufferLookup, ref statADataBuffer, ref statA, ref observerOfStatB))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        public bool AddModifier(ref BufferLookup<StatsData> statsDataBufferLookup, StatReference affectedStatReference, TModifier modifier, out StatModifierReference modifierReference)
        {
            if (statsDataBufferLookup.TryGetBuffer(affectedStatReference.Entity, out DynamicBuffer<StatsData> statsDataBuffer))
            {
                return AddModifier(ref statsDataBuffer, affectedStatReference, modifier, out modifierReference);
            }

            modifierReference = default;
            return false;
        }

        public bool AddModifier(ref DynamicBuffer<StatsData> statsDataBuffer, StatReference affectedStatReference, TModifier modifier, out StatModifierReference modifierReference)
        {
            // TODO:
            // Only add if !IsStatAInStatBsObserversChain
            // Find by cached or by search
            // Insert modifier
            // Recalc stat value

            //modifier.InsertElement(ref affectedStatsDataBuffer, 0);

            modifierReference = default;
            return false;
        }

        public bool OverwriteModifier(ref BufferLookup<StatsData> statsDataBufferLookup, ref StatModifierReference modifierReference, TModifier modifier)
        {
            if (statsDataBufferLookup.TryGetBuffer(modifierReference.Entity, out DynamicBuffer<StatsData> statsDataBuffer))
            {
                return OverwriteModifier(ref statsDataBuffer, ref modifierReference, modifier);
            }

            return false;
        }

        public bool OverwriteModifier(ref DynamicBuffer<StatsData> statsDataBuffer, ref StatModifierReference modifierReference, TModifier modifier)
        {
            // TODO:
            // To prevent errors, do a resize and then a write at index. So it doesn't matter if the new one is a different size.
            // But we might need a WriteAt() function generated on partial structs

            // Find by cached or by search
            // Insert modifier
            // Recalc stat value

            //modifier.InsertElement(ref affectedStatsDataBuffer, 0);

            return false;
        }

        public bool RemoveModifier(ref BufferLookup<StatsData> statsDataBufferLookup, StatModifierReference modifierReference)
        {
            if (statsDataBufferLookup.TryGetBuffer(modifierReference.Entity, out DynamicBuffer<StatsData> statsDataBuffer))
            {
                return RemoveModifier(ref statsDataBuffer, modifierReference);
            }

            return false;
        }

        public bool RemoveModifier(ref DynamicBuffer<StatsData> statsDataBuffer, StatModifierReference modifierReference)
        {
            // TODO:
            // Find by cached or by search
            // Remove modifier
            // Recalc stat value

            return false;
        }
    }
}