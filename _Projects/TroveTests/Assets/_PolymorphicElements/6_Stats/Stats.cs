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
    /// - First, a DataLayoutVersion and a ModifierIDCounter
    /// - Then, a sequence of StatTypeID + StartByteIndex, sorted by StartByteIndex in ascending order
    /// - Then, a sequence of variable-size stat data:
    ///     - Stat base value 
    ///     - Stat final value
    ///     - ModifiersCount
    ///     - ObserversCount
    ///     - ObserversStartByteIndex
    ///     - sequence of ModifierTypeID + variable-sized Modifier data (modifiers are polymorphic elements)
    ///     - sequence of StatReferences representing observers of this stat
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

        public byte HasCachedData;
        public int CachedStatStartByteIndex;
        public int CachedDataLayoutVersion;

        public StatReference(Entity onEntity, ushort statTypeID)
        {
            Entity = onEntity;
            StatTypeID = statTypeID;

            HasCachedData = 0;
            CachedStatStartByteIndex = 0;
            CachedDataLayoutVersion = 0;
        }
    }

    public struct StatModifierReference
    {
        public Entity Entity;
        public ushort StatTypeID;
        public int StatModifierID;

        public int CachedStatModifierStartByteIndex;
        public int CachedDataLayoutVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StatValues
    {
        public float BaseValue;
        public float Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct StatModifiersAndObservers
    {
        public ushort ModifiersCount;
        public ushort ObserversCount;
        public int ObserversStartByteIndex;
    }

    public interface IBaseStatModifier<TModifiersStack> where TModifiersStack : unmanaged, IStatModifiersStack
    {
        void Apply(ref TModifiersStack stack);
    }

    public interface IStatModifiersStack
    {
        void Apply(float statBaseValue, ref float statValue);
    }

    public unsafe static class StatsHandler
    {
        private const int SizeOfDataLayoutVersion = sizeof(int);
        private const int SizeOfModifierIDCounter = sizeof(int);
        private const int SizeOfStatTypeID = sizeof(ushort);
        private const int SizeOfStatStartByteIndex = sizeof(int);
        private static int SizeOfStatValues = sizeof(StatValues);
        private static int SizeOfStatModifiersAndObservers = sizeof(StatModifiersAndObservers);

        public static void InitializeStatsData(ref DynamicBuffer<StatsData> statsDataBuffer, in NativeList<StatDefinition> statDefinitions)
        {
            statsDataBuffer.Clear();
            int writeByteIndex = 0;
            int statsDataStartByteIndex = statDefinitions.Length * (SizeOfStatTypeID + SizeOfStatStartByteIndex);

            // Data layout version
            PolymorphicElementsUtility.InternalUse.WriteAny(ref statsDataBuffer, writeByteIndex, (int)0);
            writeByteIndex += SizeOfDataLayoutVersion;

            // Modifier id counter
            PolymorphicElementsUtility.InternalUse.WriteAny(ref statsDataBuffer, writeByteIndex, (int)0);
            writeByteIndex += SizeOfModifierIDCounter;

            // Add sequence of stat type IDs and start byte index
            for (int i = 0; i < statDefinitions.Length; i++)
            {
                // Stat type ID
                PolymorphicElementsUtility.InternalUse.WriteAny(ref statsDataBuffer, writeByteIndex, statDefinitions[i].TypeID);
                writeByteIndex += SizeOfStatTypeID;

                // Stat start byte index
                PolymorphicElementsUtility.InternalUse.WriteAny(ref statsDataBuffer, writeByteIndex, statsDataStartByteIndex + (i * (SizeOfStatValues + SizeOfStatModifiersAndObservers)));
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

                // Modifiers and observers
                StatModifiersAndObservers modifiersAndObservers = new StatModifiersAndObservers
                {
                    ModifiersCount = 0,
                    ObserversCount = 0,
                    ObserversStartByteIndex = writeByteIndex + SizeOfStatModifiersAndObservers,
                };
                PolymorphicElementsUtility.InternalUse.WriteAny(ref statsDataBuffer, writeByteIndex, modifiersAndObservers);
                writeByteIndex += SizeOfStatModifiersAndObservers;
            }
        }

        private static int GetDataLayoutVersion(ref DynamicBuffer<StatsData> statsDataBuffer)
        {
            PolymorphicElementsUtility.InternalUse.ReadAny(ref statsDataBuffer, 0, out _, out int result);
            return result;
        }

        private static int IncrementDataLayoutVersion(ref DynamicBuffer<StatsData> statsDataBuffer, out bool success)
        {
            ref int dataLayoutVersion = ref PolymorphicElementsUtility.InternalUse.ReadAnyAsRef<int, StatsData>(ref statsDataBuffer, 0, out _, out success);
            dataLayoutVersion++;
            return dataLayoutVersion;
        }

        private static int GetAndIncrementModifierIDCounter(ref DynamicBuffer<StatsData> statsDataBuffer, out bool success)
        {
            ref int modIDCounter = ref PolymorphicElementsUtility.InternalUse.ReadAnyAsRef<int, StatsData>(ref statsDataBuffer, SizeOfDataLayoutVersion, out _, out success);
            modIDCounter++;
            return modIDCounter;
        }

        private static void UpdateStatReferenceCachedData(ref DynamicBuffer<StatsData> statsDataBuffer, ref StatReference statReference)
        {
            int dataLayoutVersion = GetDataLayoutVersion(ref statsDataBuffer);
           
            // TODO; and return what?

            // Update cache data
            if (statReference.HasCachedData == 0 || statReference.CachedDataLayoutVersion != dataLayoutVersion)
            {
                // TODO
                statReference.CachedDataLayoutVersion = dataLayoutVersion;
            }

            //return false;
        }

        public static bool GetStatValues(ref BufferLookup<StatsData> statsDataBufferLookup, StatReference statReference, out StatValues statValues)
        {
            if (statsDataBufferLookup.TryGetBuffer(statReference.Entity, out DynamicBuffer<StatsData> statsDataBuffer))
            {
                return GetStatValues(ref statsDataBuffer, statReference, out statValues);
            }

            statValues = default;
            return false;
        }

        public static bool GetStatValues(ref DynamicBuffer<StatsData> statsDataBuffer, StatReference statReference, out StatValues statValues)
        {
            // TODO:
            // Find by cached or by search

            statValues = default;
            return false;
        }

        public static bool SetStatBaseValue(ref BufferLookup<StatsData> statsDataBufferLookup, StatReference statReference, float baseValue)
        {
            if (statsDataBufferLookup.TryGetBuffer(statReference.Entity, out DynamicBuffer<StatsData> statsDataBuffer))
            {
                return SetStatBaseValue(ref statsDataBuffer, statReference, baseValue);
            }

            return false;
        }

        public static bool SetStatBaseValue(ref DynamicBuffer<StatsData> statsDataBuffer, StatReference statReference, float baseValue)
        {
            // TODO:
            // Find by cached or by search

            return false;
        }

        public static bool AddStatBaseValue(ref BufferLookup<StatsData> statsDataBufferLookup, StatReference statReference, float value)
        {
            if (statsDataBufferLookup.TryGetBuffer(statReference.Entity, out DynamicBuffer<StatsData> statsDataBuffer))
            {
                return AddStatBaseValue(ref statsDataBuffer, statReference, value);
            }

            return false;
        }

        public static bool AddStatBaseValue(ref DynamicBuffer<StatsData> statsDataBuffer, StatReference statReference, float value)
        {
            // TODO:
            // Find by cached or by search

            return false;
        }

        public static bool RecalculateStat(ref BufferLookup<StatsData> statsDataBufferLookup, StatReference statReference)
        {
            if (statsDataBufferLookup.TryGetBuffer(statReference.Entity, out DynamicBuffer<StatsData> statsDataBuffer))
            {
                return RecalculateStat(ref statsDataBuffer, statReference);
            }

            return false;
        }

        public static bool RecalculateStat(ref DynamicBuffer<StatsData> statsDataBuffer, StatReference statReference)
        {
            // TODO:
            // Find by cached or by search
            // Apply all modifiers
            // Recalc all local observers
            // Recalc all remote observers

            return false;
        }

        public static bool AddModifier<TModifier, TModifiersStack>(ref BufferLookup<StatsData> statsDataBufferLookup, StatReference affectedStatReference, TModifier modifier, out StatModifierReference modifierReference)
            where TModifier : unmanaged, IBaseStatModifier<TModifiersStack>, IPolymorphicUnionElement
            where TModifiersStack : unmanaged, IStatModifiersStack
        {
            if (statsDataBufferLookup.TryGetBuffer(affectedStatReference.Entity, out DynamicBuffer<StatsData> statsDataBuffer))
            {
                return AddModifier<TModifier, TModifiersStack>(ref statsDataBuffer, affectedStatReference, modifier, out modifierReference);
            }

            modifierReference = default;
            return false;
        }

        public static bool AddModifier<TModifier, TModifiersStack>(ref DynamicBuffer<StatsData> statsDataBuffer, StatReference affectedStatReference, TModifier modifier, out StatModifierReference modifierReference)
            where TModifier : unmanaged, IBaseStatModifier<TModifiersStack>, IPolymorphicUnionElement
            where TModifiersStack : unmanaged, IStatModifiersStack
        {
            // TODO:
            // Find by cached or by search
            // Insert modifier
            // Recalc stat value

            //modifier.InsertElement(ref affectedStatsDataBuffer, 0);

            modifierReference = default;
            return false;
        }

        public static bool OverwriteModifier<TModifier, TModifiersStack>(ref BufferLookup<StatsData> statsDataBufferLookup, ref StatModifierReference modifierReference, TModifier modifier)
            where TModifier : unmanaged, IBaseStatModifier<TModifiersStack>, IPolymorphicUnionElement
            where TModifiersStack : unmanaged, IStatModifiersStack
        {
            if (statsDataBufferLookup.TryGetBuffer(modifierReference.Entity, out DynamicBuffer<StatsData> statsDataBuffer))
            {
                return OverwriteModifier<TModifier, TModifiersStack>(ref statsDataBuffer, ref modifierReference, modifier);
            }

            return false;
        }

        public static bool OverwriteModifier<TModifier, TModifiersStack>(ref DynamicBuffer<StatsData> statsDataBuffer, ref StatModifierReference modifierReference, TModifier modifier)
            where TModifier : unmanaged, IBaseStatModifier<TModifiersStack>, IPolymorphicUnionElement
            where TModifiersStack : unmanaged, IStatModifiersStack
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

        public static bool RemoveModifier(ref BufferLookup<StatsData> statsDataBufferLookup, StatModifierReference modifierReference)
        {
            if (statsDataBufferLookup.TryGetBuffer(modifierReference.Entity, out DynamicBuffer<StatsData> statsDataBuffer))
            {
                return RemoveModifier(ref statsDataBuffer, modifierReference);
            }

            return false;
        }

        public static bool RemoveModifier(ref DynamicBuffer<StatsData> statsDataBuffer, StatModifierReference modifierReference)
        {
            // TODO:
            // Find by cached or by search
            // Remove modifier
            // Recalc stat value

            return false;
        }
    }
}