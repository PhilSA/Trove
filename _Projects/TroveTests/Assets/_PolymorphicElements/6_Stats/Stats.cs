using Unity.Entities;
using Unity.Mathematics;
using System;
using Trove.PolymorphicElements;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Runtime.InteropServices;

/*
 * TODO STATS:
 *  - Support for Add/Remove stat at runtime
 *  - Strategy for reactively writing stat data to components when it changes?
 *      - pass some kind of TStatChangeHandler to the Handler?
 *      - Pass an optional NativeList of changed statReferences?
 *      - Just have a change filter job on data buffer and copy everything we need?
 *  - multithreaded stat changes
 *      - Whenever a stat needs recalculation, we add all its observers to a list of Statreferences at the end of the data buffer
 *      - We remove duplicates as we add them, And we only add stats of other entities there. Local stats can be recalcd immediately
 *      - Then a change filter job iterates data buffers and processes single thread recalc of all observers
 *      
 * 
 * TODO POLYMORPH: 
*/

namespace Trove.Stats
{
    /// <summary>
    /// Buffer of bytes representing stats data
    /// 
    /// The layout of the data is as follows:
    /// - DataLayoutVersion 
    /// - ModifierIDCounter
    /// - StatsToRecalculateCount
    /// - StatsToRecalculateByteStartIndex
    /// - StatsCount
    /// - sequence of StatTypeID + StartByteIndex, sorted by StartByteIndex in ascending order
    /// - sequence of variable-size stat data:
    ///     - Stat base value 
    ///     - Stat final value
    ///     - ObserversCount
    ///     - sequence of StatReferences representing observers of this stat
    ///     - ModifiersCount
    ///     - sequence of ModifierID + Size
    ///     - sequence of ModifierTypeID + variable-sized Modifier data (modifiers are polymorphic elements)
    /// - sequence of StatReferences representing stats that need recalculation
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

    public interface IBaseStatModifier<TModifiersStack, TStatReferenceList> 
            where TModifiersStack : unmanaged, IStatModifiersStack
            where TStatReferenceList : unmanaged, INativeList<StatReference>
    {
        public void AddObservedStatReferences(ref TStatReferenceList statReferencesList);
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

    public unsafe struct BaseStatsHandler<TModifier, TModifierStack, TModifiersApplier, TStatReferenceList>
            where TModifier : unmanaged, IBaseStatModifier<TModifierStack, TStatReferenceList>, IPolymorphicUnionElement
            where TModifierStack : unmanaged, IStatModifiersStack
            where TModifiersApplier : unmanaged, IModifiersApplier<TModifierStack>
            where TStatReferenceList : unmanaged, INativeList<StatReference>
    {
        // Global data
        private const int SizeOfDataLayoutVersion = sizeof(int);
        private const int SizeOfModifierIDCounter = sizeof(int);
        private const int SizeOfStatsToRecalculateCount = sizeof(int);
        private const int SizeOfStatsToRecalculateByteStartIndex = sizeof(int);
        private const int SizeOfStatsCount = sizeof(ushort);

        // Stats Metadatas
        // ===========================
        private const int SizeOfStatTypeID = sizeof(ushort);
        private const int SizeOfStatStartByteIndex = sizeof(int);
        // ===========================

        // Stat datas
        // ===========================
        private static int SizeOfStatValues = sizeof(StatValues);
        private const int SizeOfObserversCount = sizeof(int);
        private static int SizeOfObserver = sizeof(StatReference);
        private const int SizeOfModifiersCount = sizeof(int);
        private const int SizeOfModifierID = sizeof(int);
        private const int SizeOfModifierSize = sizeof(int);
        // Then, variable-sized modifiers
        // ===========================

        private const int StartByteIndexOfStatsToRecalculateCount = SizeOfDataLayoutVersion + SizeOfModifierIDCounter;
        private const int StartByteIndexOfStatsCount = StartByteIndexOfStatsToRecalculateCount + SizeOfStatsToRecalculateCount + SizeOfStatsToRecalculateByteStartIndex;

        private struct ModifierMetaData
        {
            public int ModifierID;
            public int ModifierSize;
        }

        public void InitializeStatsData(ref DynamicBuffer<StatsData> statsDataBuffer, in NativeList<StatDefinition> statDefinitions)
        {
            statsDataBuffer.Clear();
            int writeByteIndex = 0;

            // Filter out duplicate stat types
            for (int i = statDefinitions.Length - 1; i >= 0; i--)
            {
                for (int j = 0; j < i; j++)
                {
                    if(statDefinitions[i].TypeID == statDefinitions[j].TypeID)
                    {
                        statDefinitions.RemoveAt(i);
                    }
                }
            }

            // DataLayoutVersion
            PolymorphicElementsUtility.InternalUse.WriteAny(ref statsDataBuffer, writeByteIndex, (int)0);
            writeByteIndex += SizeOfDataLayoutVersion;

            // ModifierIDCounter
            PolymorphicElementsUtility.InternalUse.WriteAny(ref statsDataBuffer, writeByteIndex, (int)0);
            writeByteIndex += SizeOfModifierIDCounter;

            // StatsToRecalculateCount
            PolymorphicElementsUtility.InternalUse.WriteAny(ref statsDataBuffer, writeByteIndex, (int)0);
            writeByteIndex += SizeOfStatsToRecalculateCount;

            // StatsToRecalculateByteStartIndex
            PolymorphicElementsUtility.InternalUse.WriteAny(ref statsDataBuffer, writeByteIndex, (int)0);
            writeByteIndex += SizeOfStatsToRecalculateByteStartIndex;

            // StatsCount
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

        private bool FindObserversCountAndStartByteIndex(ref DynamicBuffer<StatsData> statsDataBuffer, int statStartByteIndex, out int observersCount, out int observerDatasStartByteIndex)
        {
            int readByteIndex = statStartByteIndex;

            // Skip stat values
            readByteIndex += SizeOfStatValues;

            // Read observers count and outputted next byte index
            if (PolymorphicElementsUtility.InternalUse.ReadAny(ref statsDataBuffer, readByteIndex, out observerDatasStartByteIndex, out observersCount))
            {
                return true;
            }

            // Couldnt read modifiers & observers info
            observerDatasStartByteIndex = default;
            return false;
        }

        private bool FindModifiersByteIndexDatas(
            ref DynamicBuffer<StatsData> statsDataBuffer,
            int statStartByteIndex,
            out int observersCount,
            out int observerDatasStartByteIndex,
            out int modifiersStartByteIndex,
            out int modifiersCount,
            out int modifiersMapStartByteIndex,
            out int modifierDatasStartByteIndex)
        {
            if (FindObserversCountAndStartByteIndex(ref statsDataBuffer, statStartByteIndex, out observersCount, out observerDatasStartByteIndex))
            {
                // Skip observers
                modifiersStartByteIndex = observerDatasStartByteIndex + (observersCount * SizeOfObserver);

                // Read modifiers count
                if (PolymorphicElementsUtility.InternalUse.ReadAny(ref statsDataBuffer, modifiersStartByteIndex, out modifiersMapStartByteIndex, out modifiersCount))
                {
                    modifierDatasStartByteIndex = modifiersMapStartByteIndex + (modifiersCount * (SizeOfModifierID + SizeOfModifierSize));
                    return true;
                }
            }

            // Couldnt read modifiers & observers info
            modifiersStartByteIndex = default;
            modifiersCount = default;
            modifiersMapStartByteIndex = default;
            modifierDatasStartByteIndex = default;
            return false;
        }

        private bool FindModifierStartByteIndex(ref DynamicBuffer<StatsData> statsDataBuffer, int statStartByteIndex, int modifierID, out int modifierStartByteIndex)
        {
            if (FindModifiersByteIndexDatas(
                ref statsDataBuffer, 
                statStartByteIndex, 
                out int observersCount, 
                out int observerDatasStartByteIndex, 
                out int modifiersStartByteIndex, 
                out int modifiersCount, 
                out int modifiersMapStartByteIndex, 
                out int modifierDatasStartByteIndex))
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
                            // Read start byte index offset from map end
                            if (PolymorphicElementsUtility.InternalUse.ReadAny(ref statsDataBuffer, readByteIndex, out readByteIndex, out int modifiersStartByteIndexOffsetFromModifiersStart))
                            {
                                modifierStartByteIndex = modifiersStartByteIndex + modifiersStartByteIndexOffsetFromModifiersStart;
                                return true;
                            }
                            else
                            {
                                // Couldn't read the start byte index of the matched modifier. Not supposed to happen
                                modifierStartByteIndex = default;
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
                return SetStatBaseValue(ref statsDataBufferLookup, ref statsDataBuffer, ref statReference, baseValue);
            }

            return false;
        }

        public bool SetStatBaseValue(ref BufferLookup<StatsData> statsDataBufferLookup, ref DynamicBuffer<StatsData> statsDataBuffer, ref StatReference statReference, float baseValue)
        {
            if (UpdateStatReferenceCachedData(ref statsDataBuffer, ref statReference))
            {
                int readByteIndex = statReference.CachedStatStartByteIndex;
                ref StatValues statValues = ref PolymorphicElementsUtility.InternalUse.ReadAnyAsRef<StatValues, StatsData>(ref statsDataBuffer, readByteIndex, out _, out bool success);
                if (success)
                {
                    if (baseValue != statValues.BaseValue)
                    {
                        statValues.BaseValue = baseValue;
                        RecalculateStat(ref statsDataBufferLookup, ref statsDataBuffer, ref statReference);
                    }
                    return true;
                }
            }

            return false;
        }

        public bool AddStatBaseValue(ref BufferLookup<StatsData> statsDataBufferLookup, ref StatReference statReference, float value)
        {
            if (statsDataBufferLookup.TryGetBuffer(statReference.Entity, out DynamicBuffer<StatsData> statsDataBuffer))
            {
                return AddStatBaseValue(ref statsDataBufferLookup, ref statsDataBuffer, ref statReference, value);
            }

            return false;
        }

        public bool AddStatBaseValue(ref BufferLookup<StatsData> statsDataBufferLookup, ref DynamicBuffer<StatsData> statsDataBuffer, ref StatReference statReference, float value)
        {
            if (value != 0f && UpdateStatReferenceCachedData(ref statsDataBuffer, ref statReference))
            {
                int readByteIndex = statReference.CachedStatStartByteIndex;
                ref StatValues statValues = ref PolymorphicElementsUtility.InternalUse.ReadAnyAsRef<StatValues, StatsData>(ref statsDataBuffer, readByteIndex, out _, out bool success);
                if (success)
                {
                    statValues.BaseValue += value;
                    RecalculateStat(ref statsDataBufferLookup, ref statsDataBuffer, ref statReference);
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
                if (FindModifiersByteIndexDatas(
                    ref statsDataBuffer, 
                    statReference.CachedStatStartByteIndex, 
                    out int observersCount, 
                    out int observerDatasStartByteIndex, 
                    out int modifiersStartByteIndex, 
                    out int modifiersCount, 
                    out int modifiersMapStartByteIndex, 
                    out int modifierDatasStartByteIndex))
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
                            readByteIndex = observerDatasStartByteIndex;
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
            if (UpdateStatReferenceCachedData(ref statADataBuffer, ref statA) && UpdateStatReferenceCachedData(ref statBDataBuffer, ref statB))
            {
                // Check if stat A is in observers of stat B
                if (FindObserversCountAndStartByteIndex(ref statADataBuffer, statB.CachedStatStartByteIndex, out int observersCount, out int observerDatasStartByteIndex))
                {
                    int readByteIndex = observerDatasStartByteIndex;
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

        public bool AddModifier(ref BufferLookup<StatsData> statsDataBufferLookup, ref StatReference affectedStatReference, TModifier modifier, out StatModifierReference modifierReference)
        {
            if (statsDataBufferLookup.TryGetBuffer(affectedStatReference.Entity, out DynamicBuffer<StatsData> statsDataBuffer))
            {
                return AddModifier(ref statsDataBufferLookup, ref statsDataBuffer, ref affectedStatReference, modifier, out modifierReference);
            }

            modifierReference = default;
            return false;
        }

        public bool AddModifier(ref BufferLookup<StatsData> statsDataBufferLookup, ref DynamicBuffer<StatsData> statsDataBuffer, ref StatReference affectedStatReference, TModifier modifier, out StatModifierReference modifierReference)
        {
            if (UpdateStatReferenceCachedData(ref statsDataBuffer, ref affectedStatReference))
            {
                // Get observed StatReferences
                TStatReferenceList observedStatReferences = new TStatReferenceList();
                modifier.AddObservedStatReferences(ref observedStatReferences);

                // Prevent infinite observers loops
                bool allObservedStatsValid = true;
                bool wouldAddingModifierCauseInfiniteObserversLoop = false;
                for (int i = 0; i < observedStatReferences.Length; i++)
                {
                    StatReference observedStatReference = observedStatReferences[i];

                    if (UpdateStatReferenceCachedData(ref statsDataBuffer, ref observedStatReference))
                    {
                        // We're checking if "affectedStat" can become an observer of "observedStat".
                        // We must validata that "observedStat" doesn't observe "affectedStat" somewhere down the observers chain.
                        if (observedStatReference.Entity == affectedStatReference.Entity)
                        {
                            // Local entity stat
                            if (IsStatAInStatBsObserversChain(ref statsDataBufferLookup, ref statsDataBuffer, ref statsDataBuffer, ref observedStatReference, ref affectedStatReference))
                            {
                                wouldAddingModifierCauseInfiniteObserversLoop = true;
                                break;
                            }
                        }
                        else
                        {
                            // Remote entity stat
                            if (IsStatAInStatBsObserversChain(ref statsDataBufferLookup, ref statsDataBuffer, ref observedStatReference, ref affectedStatReference))
                            {
                                wouldAddingModifierCauseInfiniteObserversLoop = true;
                                break;
                            }
                        }
                        observedStatReferences[i] = observedStatReference;
                    }
                    else
                    {
                        allObservedStatsValid = false;
                        break;
                    }
                }

                if (allObservedStatsValid && !wouldAddingModifierCauseInfiniteObserversLoop)
                {
                    // Get new modifier ID
                    if (GetAndIncrementModifierIDCounter(ref statsDataBuffer, out int modifierID))
                    {
                        if (FindModifiersByteIndexDatas(
                            ref statsDataBuffer, 
                            affectedStatReference.CachedStatStartByteIndex, 
                            out int observersCount, 
                            out int observerDatasStartByteIndex, 
                            out int modifiersStartByteIndex, 
                            out int modifiersCount, 
                            out int modifiersMapStartByteIndex, 
                            out int modifierDatasStartByteIndex))
                        {
                            // Get modifier byte index insert point (at the end of existing modifiers)
                            int modifierInsertByteIndex = modifierDatasStartByteIndex;
                            int readByteIndex = modifiersMapStartByteIndex;
                            for (int i = 0; i < modifiersCount; i++)
                            {
                                // Skip modifier ID
                                readByteIndex += SizeOfModifierID;

                                // Read modifier size
                                if (PolymorphicElementsUtility.InternalUse.ReadAny(ref statsDataBuffer, readByteIndex, out readByteIndex, out int modifierSize))
                                {
                                    // Add size to index that started at modifier datas
                                    modifierInsertByteIndex += modifierSize;
                                }
                                else
                                {
                                    // Error
                                    modifierReference = default;
                                    return false;
                                }
                            }
                            int endOfModifierIDsAndSizesByteIndex = readByteIndex;

                            // Insert new modifier data at the end of modifier datas
                            PolymorphicElementMetaData modifierMetaData = modifier.InsertElementVariableSized(ref statsDataBuffer, modifierInsertByteIndex);
                            if (modifierMetaData.IsValid())
                            {
                                // Increment version
                                IncrementDataLayoutVersion(ref statsDataBuffer);

                                // Update modifiers count
                                {
                                    ref int tmpModifiersCountRef = ref PolymorphicElementsUtility.InternalUse.ReadAnyAsRef<int, StatsData>(ref statsDataBuffer, modifiersStartByteIndex, out _, out bool success);
                                    if (success)
                                    {
                                        tmpModifiersCountRef++;
                                    }
                                    else
                                    {
                                        // Error
                                        modifierReference = default;
                                        return false;
                                    }
                                }

                                // Add modifier metaData
                                int modifierTotalSize = modifier.GetVariableElementTotalSizeWithID();
                                if (!PolymorphicElementsUtility.InternalUse.InsertAny(ref statsDataBuffer, endOfModifierIDsAndSizesByteIndex, new ModifierMetaData
                                {
                                    ModifierID = modifierID,
                                    ModifierSize = modifierTotalSize,
                                }))
                                {
                                    // Error
                                    modifierReference = default;
                                    return false;
                                }

                                // Update stat start indexes of every stat after our modifier (add modifier metadata + modifier size)
                                UpdateStatStartByteIndexesAfterIndex(ref statsDataBuffer, affectedStatReference.CachedStatStartByteIndex, sizeof(ModifierMetaData) + modifierTotalSize);

                                // Add affected stat as observer of modifier observed stats
                                for (int i = 0; i < observedStatReferences.Length; i++)
                                {
                                    StatReference observedStatReference = observedStatReferences[i];

                                    // Local entity stat
                                    if (observedStatReference.Entity == affectedStatReference.Entity)
                                    {
                                        AddObserver(ref statsDataBufferLookup, ref statsDataBuffer, ref statsDataBuffer, ref affectedStatReference, ref observedStatReference);
                                    }
                                    // Remote entity stat
                                    else
                                    {
                                        AddObserver(ref statsDataBufferLookup, ref statsDataBuffer, ref affectedStatReference, ref observedStatReference);
                                    }
                                }

                                // Recalculate stat value, since modifiers will likely change it
                                RecalculateStat(ref statsDataBufferLookup, ref statsDataBuffer, ref affectedStatReference);
                            }
                        }
                    }
                }
            }

            modifierReference = default;
            return false;
        }

        public bool RemoveModifier(ref BufferLookup<StatsData> statsDataBufferLookup, ref StatModifierReference modifierReference)
        {
            if (statsDataBufferLookup.TryGetBuffer(modifierReference.Entity, out DynamicBuffer<StatsData> statsDataBuffer))
            {
                return RemoveModifier(ref statsDataBufferLookup, ref statsDataBuffer, ref modifierReference);
            }

            return false;
        }

        public bool RemoveModifier(ref BufferLookup<StatsData> statsDataBufferLookup, ref DynamicBuffer<StatsData> statsDataBuffer, ref StatModifierReference modifierReference)
        {
            // TODO:
            // Find by cached or by search
            // Remove modifier
            // Recalc stat value

            return false;
        }

        private bool AddObserver(ref BufferLookup<StatsData> statsDataBufferLookup, ref DynamicBuffer<StatsData> observerStatsDataBuffer, ref StatReference observerStatReference, ref StatReference observedStatReference)
        {
            if (statsDataBufferLookup.TryGetBuffer(observedStatReference.Entity, out DynamicBuffer<StatsData> observedStatsDataBuffer))
            {
                return AddObserver(ref statsDataBufferLookup, ref observerStatsDataBuffer, ref observedStatsDataBuffer, ref observerStatReference, ref observedStatReference);
            }

            return false;
        }

        private bool AddObserver(ref BufferLookup<StatsData> statsDataBufferLookup, ref DynamicBuffer<StatsData> observerStatsDataBuffer, ref DynamicBuffer<StatsData> observedStatsDataBuffer, ref StatReference observerStatReference, ref StatReference observedStatReferencee)
        {
            if (UpdateStatReferenceCachedData(ref observerStatsDataBuffer, ref observerStatReference) && UpdateStatReferenceCachedData(ref observedStatsDataBuffer, ref observedStatReference))
            {
                if (FindObserversCountAndStartByteIndex(ref statsDataBuffer, statStartByteIndex, out observersCount, out observerDatasStartByteIndex))
                {
                    // TODO
                    // Modify start byte indexes on observed entity with size of added observer
                }
            }

            return false;
        }

        private bool UpdateStatStartByteIndexesAfterIndex(ref DynamicBuffer<StatsData> statsDataBuffer, int afterIndex, int changeAmount)
        {
            if (PolymorphicElementsUtility.InternalUse.ReadAny(ref statsDataBuffer, StartByteIndexOfStatsCount, out int readByteIndex, out int statsCount))
            {
                for (int i = 0; i < statsCount; i++)
                {
                    // Skip stat TypeID
                    readByteIndex += SizeOfStatTypeID;

                    // Read stat startByteIndex
                    ref int tmpStatStartByteIndexRef = ref PolymorphicElementsUtility.InternalUse.ReadAnyAsRef<int, StatsData>(ref statsDataBuffer, readByteIndex, out readByteIndex, out bool success);
                    if (success)
                    {
                        if (tmpStatStartByteIndexRef > afterIndex)
                        {
                            // Add size of inserted elements
                            tmpStatStartByteIndexRef += changeAmount;
                        }
                    }
                    else
                    {
                        // Error
                        return false;
                    }
                }
            }
            else
            {
                // Error
                return false;
            }

            return true;
        }

        // TODO: can be used to determine a max amount of modifiers affecting a stat
        public bool GetModifiersCountOnStat(ref DynamicBuffer<StatsData> statsDataBuffer, ref StatReference statReference, ushort modifierType)
        {
            return false;
        }

        // TODO: can be used to determine a max amount of modifiers of certain type affecting a stat
        public bool GetModifiersCountOfTypeOnStat(ref DynamicBuffer<StatsData> statsDataBuffer, ref StatReference statReference, ushort modifierType)
        {
            return false;
        }
    }
}