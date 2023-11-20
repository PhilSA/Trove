//using Unity.Entities;
//using Unity.Mathematics;
//using System;
//using Trove.PolymorphicElements;
//using Unity.Collections;
//using Unity.Logging;
//using Unity.Collections.LowLevel.Unsafe;
//using System.Runtime.InteropServices;

///*
// * TODO STATS:
// *  - Support for Add/Remove stat at runtime
// *  - Strategy for reactively writing stat data to components when it changes?
// *      - pass some kind of TStatChangeHandler to the Handler?
// *      - Pass an optional NativeList of changed statReferences?
// *      - Just have a change filter job on data buffer and copy everything we need?
// *  - multithreaded stat changes
// *      - Whenever a stat needs recalculation, we add all its observers to a list of Statreferences at the end of the data buffer
// *      - We remove duplicates as we add them, And we only add stats of other entities there. Local stats can be recalcd immediately
// *      - Then a change filter job iterates data buffers and processes single thread recalc of all observers
// *      
// * 
// * TODO POLYMORPH: 
//*/

//namespace Trove.Stats
//{

//    public interface IGetByteSize
//    {
//        int GetByteSize();
//    }

//    /// <summary>
//    /// Buffer of bytes representing stats data
//    /// 
//    /// The layout of the data is as follows:
//    /// - DataLayoutVersion 
//    /// - ModifierIDCounter
//    /// - StatsToRecalculateCount
//    /// - StatsToRecalculateByteStartIndex
//    /// - StatsCount
//    /// - sequence of StatTypeID + StartByteIndex, sorted by StartByteIndex in ascending order
//    /// - sequence of variable-size stat data:
//    ///     - Stat base value 
//    ///     - Stat final value
//    ///     - ObserversCount
//    ///     - sequence of StatReferences representing observers of this stat
//    ///     - ModifiersCount
//    ///     - sequence of ModifierID + Size
//    ///     - sequence of ModifierTypeID + variable-sized Modifier data (modifiers are polymorphic elements)
//    /// - sequence of StatReferences representing stats that need recalculation
//    ///     
//    /// </summary>
//    public struct StatsDataElement : IBufferElementData, IByteBufferElement
//    {
//        public byte Data;
//    }

//    public struct StatDefinition
//    {
//        public ushort TypeID;
//        public float StartValue;
//    }

//    public unsafe struct StatReference : IGetByteSize
//    {
//        public Entity Entity;
//        public ushort StatTypeID;

//        internal byte HasCachedData;
//        internal int CachedStatTypesVersion;
//        internal int CachedStatStartByteIndexStartByteIndex;
//        internal int CachedStatDatasVersion;
//        internal int CachedStatDataStartByteIndex;

//        public StatReference(Entity onEntity, ushort statTypeID)
//        {
//            Entity = onEntity;
//            StatTypeID = statTypeID;

//            HasCachedData = 0;
//            CachedStatTypesVersion = 0;
//            CachedStatStartByteIndexStartByteIndex = 0;
//            CachedStatDatasVersion = 0;
//            CachedStatDataStartByteIndex = 0;
//        }

//        public int GetByteSize()
//        {
//            return sizeof(StatReference);
//        }

//        public static bool operator ==(StatReference a, StatReference b)
//        {
//            return b.Entity == a.Entity && b.StatTypeID == a.StatTypeID;
//        }

//        public static bool operator !=(StatReference a, StatReference b)
//        {
//            return b.Entity != a.Entity || b.StatTypeID != a.StatTypeID;
//        }
//    }

//    public struct StatModifierReference
//    {
//        public Entity Entity;
//        public ushort StatTypeID;
//        public int ModifierID;

//        internal byte HasCachedData;
//        internal int CachedStatTypesVersion;
//        internal int CachedStatStartByteIndexStartByteIndex;
//        internal int CachedStatDatasVersion;
//        internal int CachedStatDataStartByteIndex;
//        internal int CachedStatModifierStartByteIndex;
//    }

//    [StructLayout(LayoutKind.Sequential)]
//    public unsafe struct StatValues : IGetByteSize
//    {
//        public float BaseValue;
//        public float Value;

//        public int GetByteSize()
//        {
//            return sizeof (StatValues);
//        }
//    }

//    public interface IBaseStatModifier<TModifiersStack, TStatReferenceList> 
//            where TModifiersStack : unmanaged, IStatModifiersStack
//            where TStatReferenceList : unmanaged, INativeList<StatReference>
//    {
//        public void AddObservedStatReferences(ref TStatReferenceList statReferencesList);
//        public void Apply(ref TModifiersStack stack);
//    }

//    public interface IStatModifiersStack
//    {
//        public void Reset();
//        public void Apply(float statBaseValue, ref float statValue);
//    }

//    public interface IModifiersApplier<TModifiersStack> where TModifiersStack : unmanaged, IStatModifiersStack
//    {
//        public void ApplyModifiers(ref DynamicBuffer<StatsDataElement> statsDataBuffer, ref TModifiersStack modifiersStack, int startByteIndex, int count);
//    }

//    public unsafe struct BaseStatsHandler<TModifier, TModifierStack, TModifiersApplier, TStatReferenceList>
//            where TModifier : unmanaged, IBaseStatModifier<TModifierStack, TStatReferenceList>, IPolymorphicUnionElement
//            where TModifierStack : unmanaged, IStatModifiersStack
//            where TModifiersApplier : unmanaged, IModifiersApplier<TModifierStack>
//            where TStatReferenceList : unmanaged, INativeList<StatReference>
//    {

//        /*
//         * - Header
//         * - List<StatMetaData>
//         * - List<StatData>
//         *      - Header
//         *      - List<ModifierMetaData>
//         *      - List<ModifierData>
//         *      - List<Observer>
//         * - List<StatToRecalculate>
//         * - List<ChangedStat>
//        */


//        [StructLayout(LayoutKind.Sequential)]
//        private struct StatsBufferListPolymorphic
//        {
//            public int LengthBytes;
//            public int CapacityBytes;
//        }

//        [StructLayout(LayoutKind.Sequential)]
//        private struct StatsBufferList<T> where T : unmanaged
//        {
//            public int ElementCount;
//            public int CapacityBytes;

//            public int LengthBytes => ElementCount * sizeof(T);

//            public void SetCapacityBytes(ref DynamicBuffer<StatsDataElement> statsDataBuffer, int listStartByteIndex, int newCapacityBytes)
//            {
//                if (newCapacityBytes >= LengthBytes)
//                {
//                    int endOfListCapacityIndex = listStartByteIndex + CapacityBytes;
//                    int addedCapacity = CapacityBytes; // double capacity
//                    statsDataBuffer.ResizeUninitialized(statsDataBuffer.Length + addedCapacity);

//                    // Move memory of rest of the buffer
//                    int movedMemorySize = statsDataBuffer.Length - endOfListCapacityIndex;
//                    if (movedMemorySize > 0)
//                    {
//                        byte* endOfListCapacityPtr = (byte*)statsDataBuffer.GetUnsafePtr() + (long)endOfListCapacityIndex;
//                        byte* newEndOfListCapacityPtr = (byte*)statsDataBuffer.GetUnsafePtr() + (long)endOfListCapacityIndex;
//                        UnsafeUtility.MemCpy(newEndOfListCapacityPtr, endOfListCapacityPtr, movedMemorySize);
//                    }
//                }
//            }

//            public void Add(ref DynamicBuffer<StatsDataElement> statsDataBuffer, int listStartByteIndex, T t)
//            {
//                int bytesCapacity = CapacityBytes;

//                if (Length + sizeof(T) > bytesCapacity)
//                {
//                }
//            }

//            public void RemoveAt(ref DynamicBuffer<StatsDataElement> statsDataBuffer, int i)
//            {

//            }
//        }

//        [StructLayout(LayoutKind.Sequential)]
//        private struct StatsBufferBucketsList<T> where T : unmanaged, IGetByteSize
//        {
//            public struct Bucket
//            {
//                public int SizeBytes;
//                public int CapacityBytes;
//            }

//            public StatsBufferList<Bucket> Buckets;

//            public void Add(ref DynamicBuffer<StatsDataElement> statsDataBuffer, T t)
//            {

//            }

//            public void RemoveAt(ref DynamicBuffer<StatsDataElement> statsDataBuffer, int i)
//            {

//            }
//        }

//        [StructLayout(LayoutKind.Sequential)]
//        private struct StatBufferData
//        {
//            [StructLayout(LayoutKind.Sequential)]
//            public struct StatMetaData
//            {
//                public float Value;
//                public ushort TypeID;
//                public int StatDataIndex;
//            }

//            [StructLayout(LayoutKind.Sequential)]
//            public struct ModifierMetaData
//            {
//                public int ID;
//                public int Size;
//            }

//            [StructLayout(LayoutKind.Sequential)]
//            public struct StatData : IGetByteSize
//            {
//                public StatValues StatValues;
//                public int ObserversCount;
//                public int ObserversStartByteIndex;
//                public StatsBufferList<ModifierMetaData> ModifierMetaDatas;
//                StatsBufferListPolymorphic ModifierDatas;
//                public StatsBufferList<StatReference> Observers;

//                public int GetByteSize()
//                {
//                    return sizeof(StatData) + ModifierDatas.LengthBytes;
//                }
//            }

//            [StructLayout(LayoutKind.Sequential)]
//            public struct ChangedStat
//            {
//                public ushort TypeID;
//                public float Value;
//            }

//            public int StatTypesVersion;
//            public int StatDatasVersion;
//            public int ModifierIDCounter;
//            public int StatsToRecalculateCount;
//            public int StatsToRecalculateStartByteIndex;
//            public int ChangedStatsCount;
//            public int ChangedStatsStartByteIndex;

//            public StatsBufferList<StatMetaData> StatMetaDatas;
//            public StatsBufferBucketsList<StatData> StatDatas;
//            public StatsBufferList<StatReference> StatsToRecalculate;
//            public StatsBufferList<ChangedStat> ChangedStats;
//        }

//        // Global data
//        private const int SizeOf_StatTypesVersion = sizeof(int);
//        private const int SizeOf_StatDatasVersion = sizeof(int);
//        private const int SizeOf_ModifierIDCounter = sizeof(int);
//        private const int SizeOf_StatsToRecalculateCount = sizeof(int);
//        private const int SizeOf_StatsToRecalculateStartByteIndex = sizeof(int);
//        private const int SizeOf_ChangedStatsCount = sizeof(int);
//        private const int SizeOf_ChangedStatsStartByteIndex = sizeof(int);
//        private const int SizeOf_StatsCount = sizeof(ushort);

//        // Stats Metadatas
//        private const int SizeOf_StatTypeID = sizeof(ushort);
//        private const int SizeOf_StatStartByteIndex = sizeof(int);
//        private const int SizeOf_StatMetaData = SizeOf_StatTypeID + SizeOf_StatStartByteIndex;

//        // Stat datas
//        private static int SizeOf_StatValues = sizeof(StatValues);
//        private const int SizeOf_ObserversCount = sizeof(int);
//        private const int SizeOf_ObserversStartByteIndex = sizeof(int);
//        private const int SizeOf_ModifiersCount = sizeof(int);
//        private const int SizeOf_ModifierID = sizeof(int);
//        private const int SizeOf_ModifierSize = sizeof(int);
//        private const int SizeOf_ModifierMetaData = SizeOf_ModifierID + SizeOf_ModifierSize;
//        // Here there would be variable-sized modifier datas
//        private static int SizeOf_Observer = sizeof(StatReference);

//        // Deferred stats to recalculate 
//        private static int SizeOf_StatsToRecalculate = sizeof(StatReference);
//        private static int SizeOf_ChangedStat = sizeof(ChangedStat);


//        // Start Byte Indexes for global data
//        private const int StartByteIndexOf_StatTypesVersion = 0;
//        private const int StartByteIndexOf_StatDatasVersion = StartByteIndexOf_StatTypesVersion + SizeOf_StatTypesVersion;
//        private const int StartByteIndexOf_ModifierIDCounter = StartByteIndexOf_StatDatasVersion + SizeOf_StatDatasVersion;
//        private const int StartByteIndexOf_StatsToRecalculateCount = StartByteIndexOf_ModifierIDCounter + SizeOf_ModifierIDCounter;
//        private const int StartByteIndexOf_StatsToRecalculateStartByteIndex = StartByteIndexOf_StatsToRecalculateCount + SizeOf_StatsToRecalculateCount;
//        private const int StartByteIndexOf_ChangedStatsCount = StartByteIndexOf_StatsToRecalculateStartByteIndex + SizeOf_StatsToRecalculateStartByteIndex;
//        private const int StartByteIndexOf_ChangedStatsStartByteIndex = StartByteIndexOf_ChangedStatsCount + SizeOf_ChangedStatsCount;
//        private const int StartByteIndexOf_StatsCount = StartByteIndexOf_ChangedStatsStartByteIndex + SizeOf_ChangedStatsStartByteIndex;
//        private const int StartByteIndexOf_StatsMetaDatas = StartByteIndexOf_StatsCount + StartByteIndexOf_StatsCount;

//        // Stat byte offsets
//        private const int ByteOffsetOf_StatValues = 0;
//        private static int ByteOffsetOf_ObserversCount = SizeOf_StatValues;
//        private static int ByteOffsetOf_ObserversStartByteIndex = ByteOffsetOf_ObserversCount + SizeOf_ObserversCount;
//        private static int ByteOffsetOf_ModifiersCount = ByteOffsetOf_ObserversStartByteIndex + SizeOf_ObserversStartByteIndex;
//        private static int ByteOffsetOf_ModifierMetaDatas = ByteOffsetOf_ModifiersCount + SizeOf_ModifiersCount;

//        // Capacities
//        private const int Capacity_StatMetaDatas = SizeOf_StatMetaData * 20;
//        private static int Capacity_StatData = ByteOffsetOf_ModifierMetaDatas + (SizeOf_ModifierMetaData * 10) + (20 * 10) + (SizeOf_Observer * 10);

//        private struct ModifierMetaData
//        {
//            public int ModifierID;
//            public int ModifierSize;
//        }

//        public void InitializeStatsData(ref DynamicBuffer<StatsDataElement> statsDataBuffer, in NativeList<StatDefinition> statDefinitions)
//        {
//            statsDataBuffer.Clear();
//            statsDataBuffer.EnsureCapacity(initialBytesCapacity);

//            // Filter out duplicate stat types
//            for (int i = statDefinitions.Length - 1; i >= 0; i--)
//            {
//                for (int j = 0; j < i; j++)
//                {
//                    if(statDefinitions[i].TypeID == statDefinitions[j].TypeID)
//                    {
//                        statDefinitions.RemoveAt(i);
//                    }
//                }
//            }

//            // StatTypesVersion
//            ByteCollectionUtility.Add(ref statsDataBuffer, (int)0);

//            // StatDatasVersion
//            ByteCollectionUtility.Add(ref statsDataBuffer, (int)0);

//            // ModifierIDCounter
//            ByteCollectionUtility.Add(ref statsDataBuffer, (int)0);

//            // StatsToRecalculateCount
//            ByteCollectionUtility.Add(ref statsDataBuffer, (int)0);

//            // StatsToRecalculateStartByteIndex
//            ByteCollectionUtility.Add(ref statsDataBuffer, (int)0);

//            // ChangedStatsCount
//            ByteCollectionUtility.Add(ref statsDataBuffer, (int)0);

//            // ChangedStatsStartByteIndex
//            ByteCollectionUtility.Add(ref statsDataBuffer, (int)0);

//            // StatsCount
//            ByteCollectionUtility.Add(ref statsDataBuffer, (ushort)statDefinitions.Length);

//            // Add sequence of stat type IDs and start byte index
//            int statsDataStartByteIndex = StartByteIndexOf_StatsMetaDatas + (statDefinitions.Length * SizeOf_StatMetaData);
//            for (int i = 0; i < statDefinitions.Length; i++)
//            {
//                // Stat type ID
//                ByteCollectionUtility.Add(ref statsDataBuffer, statDefinitions[i].TypeID);

//                // Stat start byte index
//                ByteCollectionUtility.Add(ref statsDataBuffer, statsDataStartByteIndex + (i * (SizeOf_StatValues + SizeOf_ObserversCount + SizeOf_ModifiersCount)));
//            }

//            // Add sequence of stat base datas
//            for (int i = 0; i < statDefinitions.Length; i++)
//            {
//                StatDefinition statDefinition = statDefinitions[i];

//                // Values
//                StatValues statValues = new StatValues
//                {
//                    BaseValue = statDefinition.StartValue,
//                    Value = statDefinition.StartValue,
//                };
//                ByteCollectionUtility.Add(ref statsDataBuffer, statValues);

//                // Observers count
//                ByteCollectionUtility.Add(ref statsDataBuffer, (int)0);

//                // Observers startByteIndex
//                ByteCollectionUtility.Add(ref statsDataBuffer, (int)0);

//                // Modifiers count
//                ByteCollectionUtility.Add(ref statsDataBuffer, (int)0);
//            }
//        }

//        public int GetStatTypesVersion(ref DynamicBuffer<StatsDataElement> statsDataBuffer)
//        {
//            if (ByteCollectionUtility.Read(ref statsDataBuffer, StartByteIndexOf_StatTypesVersion, out _, out int result))
//            {
//                return result;
//            }
//            else
//            {
//                Log.Error("Corrupted buffer: Failed to read StatTypesVersion");
//                return 0;
//            }
//        }

//        public int GetStatDatasVersion(ref DynamicBuffer<StatsDataElement> statsDataBuffer)
//        {
//            if(ByteCollectionUtility.Read(ref statsDataBuffer, StartByteIndexOf_StatDatasVersion, out _, out int result))
//            {
//                return result;
//            }
//            else
//            {
//                Log.Error("Corrupted buffer: Failed to read StatDatasVersion");
//                return 0;
//            }
//        }

//        public bool IncrementStatTypesVersion(ref DynamicBuffer<StatsDataElement> statsDataBuffer)
//        {
//            ref int version = ref ByteCollectionUtility.ReadAsRef<int, StatsDataElement>(ref statsDataBuffer, StartByteIndexOf_StatTypesVersion, out _, out bool success);
//            if (success)
//            {
//                version++;
//                return true;
//            }

//            Log.Error("Corrupted buffer: Failed to read increment StatTypesVersion");
//            return false;
//        }

//        public bool IncrementStatDatasVersion(ref DynamicBuffer<StatsDataElement> statsDataBuffer)
//        {
//            ref int version = ref ByteCollectionUtility.ReadAsRef<int, StatsDataElement>(ref statsDataBuffer, StartByteIndexOf_StatDatasVersion, out _, out bool success);
//            if (success)
//            {
//                version++;
//                return true;
//            }

//            Log.Error("Corrupted buffer: Failed to read increment StatDatasVersion");
//            return false;
//        }

//        private bool GetAndIncrementModifierIDCounter(ref DynamicBuffer<StatsDataElement> statsDataBuffer, out int modifierID)
//        {
//            ref int modIDCounter = ref ByteCollectionUtility.ReadAsRef<int, StatsDataElement>(ref statsDataBuffer, SizeOf_StatTypesVersion, out _, out bool success);
//            if (success)
//            {
//                modIDCounter++;
//                modifierID = modIDCounter;
//                return true;
//            }

//            Log.Error("Corrupted buffer: Failed to read increment ModifierIDCounter");
//            modifierID = 0;
//            return false;
//        }

//        private bool FindStatMetaDataAndDataStartByteIndex(ref DynamicBuffer<StatsDataElement> statsDataBuffer, ushort statTypeID, out int statStartByteIndexStartByteIndex, out int statDataStartByteIndex)
//        {
//            int readByteIndex = StartByteIndexOf_StatsCount;

//            // Read stats count
//            if (ByteCollectionUtility.Read(ref statsDataBuffer, readByteIndex, out readByteIndex, out ushort statsCount))
//            {
//                // For each stat
//                for (int i = 0; i < statsCount; i++)
//                {
//                    // Read stat type ID
//                    if (ByteCollectionUtility.Read(ref statsDataBuffer, readByteIndex, out readByteIndex, out ushort otherStatTypeId))
//                    {
//                        // If it's the same as the id we're looking for...
//                        if (otherStatTypeId == statTypeID)
//                        {
//                            statStartByteIndexStartByteIndex = readByteIndex;

//                            // Read start byte index
//                            if (ByteCollectionUtility.Read(ref statsDataBuffer, readByteIndex, out readByteIndex, out statDataStartByteIndex))
//                            {
//                                return true;
//                            }
//                            else
//                            {
//                                Log.Error("Corrupted buffer: Couldn't read the start byte index of the matched stat.");
//                                return false;
//                            }
//                        }

//                        // Skip start byte index
//                        readByteIndex += SizeOf_StatStartByteIndex;
//                    }
//                    else
//                    {
//                        Log.Error("Corrupted buffer: Couldn't read the stat type ID.");
//                        statStartByteIndexStartByteIndex = default;
//                        statDataStartByteIndex = default;
//                        return false;
//                    }
//                }
//            }
//            else
//            {
//                Log.Error("Corrupted buffer: Couldn't read stats count.");
//                statStartByteIndexStartByteIndex = default;
//                statDataStartByteIndex = default;
//                return false;
//            }

//            // The stat doesn't exist (not an error)
//            statStartByteIndexStartByteIndex = default;
//            statDataStartByteIndex = default;
//            return false;
//        }

//        private bool GetStatObserversAndModifiersMetaData(
//            ref DynamicBuffer<StatsDataElement> statsDataBuffer,
//            int statStartByteIndex,
//            out int observersCount,
//            out int observerDatasStartByteIndex,
//            out int modifiersCount,
//            out int modifierMetaDatasStartByteIndex,
//            out int modifierDatasStartByteIndex)
//        {
//            int readByteIndex = statStartByteIndex;
//            readByteIndex += ByteOffsetOf_ObserversCount;

//            // Read observers count
//            if (ByteCollectionUtility.Read(ref statsDataBuffer, readByteIndex, out readByteIndex, out observersCount))
//            {
//                // Read observers start byte index
//                if (ByteCollectionUtility.Read(ref statsDataBuffer, readByteIndex, out readByteIndex, out observerDatasStartByteIndex))
//                {
//                    // Read modifiers count
//                    if (ByteCollectionUtility.Read(ref statsDataBuffer, readByteIndex, out modifierMetaDatasStartByteIndex, out modifiersCount))
//                    {
//                        modifierDatasStartByteIndex = modifierMetaDatasStartByteIndex + (modifiersCount * (SizeOf_ModifierMetaData));
//                        return true;
//                    }
//                    else
//                    {
//                        Log.Error("Tried reading modifiers count at an invalid index");
//                        modifierDatasStartByteIndex = default;
//                        return false;
//                    }
//                }
//                else
//                {
//                    Log.Error("Tried reading start byte index at an invalid index");
//                    modifiersCount = default;
//                    modifierMetaDatasStartByteIndex = default;
//                    modifierDatasStartByteIndex = default;
//                    return false;
//                }
//            }
//            else
//            {
//                Log.Error("Tried reading Observers Count at an invalid index");
//                observerDatasStartByteIndex = default;
//                modifiersCount = default;
//                modifierMetaDatasStartByteIndex = default;
//                modifierDatasStartByteIndex = default;
//                return false;
//            }
//        }

//        private bool FindModifierStartByteIndex(ref DynamicBuffer<StatsDataElement> statsDataBuffer, int statStartByteIndex, int modifierID, out int modifierStartByteIndex)
//        {
//            int readByteIndex = statStartByteIndex;
//            readByteIndex += ByteOffsetOf_ModifiersCount;

//            // Read modifiers count
//            if (ByteCollectionUtility.Read(ref statsDataBuffer, readByteIndex, out readByteIndex, out int modifiersCount))
//            {
//                // iterate modifier metadatas
//                int startByteIndexOfModifierDatas = readByteIndex + (modifiersCount * SizeOf_ModifierMetaData);
//                int accumulatedModifierSizesBeforeMatch = 0;
//                for (int i = 0; i < modifiersCount; i++)
//                {
//                    // Modifier ID
//                    if (ByteCollectionUtility.Read(ref statsDataBuffer, readByteIndex, out readByteIndex, out int otherModifierID))
//                    {
//                        // Modifier size
//                        if (ByteCollectionUtility.Read(ref statsDataBuffer, readByteIndex, out readByteIndex, out int modifierSize))
//                        {
//                            // If found match
//                            if (otherModifierID == modifierID)
//                            {
//                                modifierStartByteIndex = startByteIndexOfModifierDatas + accumulatedModifierSizesBeforeMatch;
//                                return true;
//                            }
//                            else
//                            {
//                                accumulatedModifierSizesBeforeMatch += modifierSize;
//                            }
//                        }
//                        else
//                        {
//                            Log.Error("Corrupted buffer: Couldn't read modifier size.");
//                            modifierStartByteIndex = default;
//                            return false;
//                        }
//                    }
//                    else
//                    {
//                        Log.Error("Corrupted buffer: Couldn't read modifier ID.");
//                        modifierStartByteIndex = default;
//                        return false;
//                    }
//                }
//            }
//            else
//            {
//                Log.Error("Tried reading modifiers count at an invalid index");
//                modifierStartByteIndex = default;
//                return false;
//            }

//            // Couldnt find match for modifier ID (not an error)
//            modifierStartByteIndex = default;
//            return false;
//        }

//        /// <summary>
//        /// 
//        /// </summary>
//        /// <param name="statsDataBuffer"></param>
//        /// <param name="statReference"></param>
//        /// <returns>false if the reference couldn't be solved</returns>
//        private bool UpdateStatReferenceCachedData(ref DynamicBuffer<StatsDataElement> statsDataBuffer, ref StatReference statReference)
//        {
//            int statTypesVersion = GetStatTypesVersion(ref statsDataBuffer);
//            int statDatasVersion = GetStatDatasVersion(ref statsDataBuffer);

//            // Update cache data
//            if (statReference.CachedStatTypesVersion != dataLayoutVersion || statReference.HasCachedData == 0)
//            {
//                if (FindStatMetaDataAndDataStartByteIndex(ref statsDataBuffer, statReference.StatTypeID, out int statStartByteIndexStartByteIndex, out int statDataStartByteIndex))
//                {
//                    statReference.HasCachedData = 1;
//                    statReference.CachedStatStartByteIndex = statDataStartByteIndex;
//                    statReference.CachedStatTypesVersion = dataLayoutVersion;
//                }
//                else
//                {
//                    // Couldn't find stat
//                    return false;
//                }
//            }

//            return true;
//        }

//        /// <summary>
//        /// 
//        /// </summary>
//        /// <param name="statsDataBuffer"></param>
//        /// <param name="modifierReference"></param>
//        /// <returns>false if the reference couldn't be solved</returns>
//        private bool UpdateModifierReferenceCachedData(ref DynamicBuffer<StatsDataElement> statsDataBuffer, ref StatModifierReference modifierReference)
//        {
//            int dataLayoutVersion = GetDataLayoutVersion(ref statsDataBuffer);

//            // Update cache data
//            if (modifierReference.CachedDataLayoutVersion != dataLayoutVersion || modifierReference.HasCachedData == 0)
//            {
//                if (FindStatDataStartByteIndex(ref statsDataBuffer, modifierReference.StatTypeID, out int statStartByteIndex))
//                {
//                    if (FindModifierStartByteIndex(ref statsDataBuffer, modifierReference.StatTypeID, modifierReference.ModifierID, out int modifierStartByteIndex))
//                    {
//                        modifierReference.HasCachedData = 1;
//                        modifierReference.CachedStatStartByteIndex = statStartByteIndex;
//                        modifierReference.CachedStatModifierStartByteIndex = modifierStartByteIndex;
//                        modifierReference.CachedDataLayoutVersion = dataLayoutVersion;
//                    }
//                    else
//                    {
//                        // Couldn't find modifier
//                        return false;
//                    }
//                }
//                else
//                {
//                    // Couldn't find stat
//                    return false;
//                }
//            }

//            return true;
//        }

//        public bool GetStatValues(ref BufferLookup<StatsDataElement> statsDataBufferLookup, ref StatReference statReference, out StatValues statValues)
//        {
//            if (statsDataBufferLookup.TryGetBuffer(statReference.Entity, out DynamicBuffer<StatsDataElement> statsDataBuffer))
//            {
//                return GetStatValues(ref statsDataBuffer, ref statReference, out statValues);
//            }

//            statValues = default;
//            return false;
//        }

//        public bool GetStatValues(ref DynamicBuffer<StatsDataElement> statsDataBuffer, ref StatReference statReference, out StatValues statValues)
//        {
//            if (UpdateStatReferenceCachedData(ref statsDataBuffer, ref statReference))
//            {
//                int readByteIndex = statReference.CachedStatStartByteIndex;
//                if (ByteCollectionUtility.Read(ref statsDataBuffer, readByteIndex, out readByteIndex, out statValues))
//                {
//                    return true;
//                }
//            }

//            statValues = default;
//            return false;
//        }

//        public bool SetStatBaseValue(ref BufferLookup<StatsDataElement> statsDataBufferLookup, ref StatReference statReference, float baseValue)
//        {
//            if (statsDataBufferLookup.TryGetBuffer(statReference.Entity, out DynamicBuffer<StatsDataElement> statsDataBuffer))
//            {
//                return SetStatBaseValue(ref statsDataBufferLookup, ref statsDataBuffer, ref statReference, baseValue);
//            }

//            return false;
//        }

//        public bool SetStatBaseValue(ref BufferLookup<StatsDataElement> statsDataBufferLookup, ref DynamicBuffer<StatsDataElement> statsDataBuffer, ref StatReference statReference, float baseValue)
//        {
//            if (UpdateStatReferenceCachedData(ref statsDataBuffer, ref statReference))
//            {
//                int readByteIndex = statReference.CachedStatStartByteIndex;
//                ref StatValues statValues = ref ByteCollectionUtility.ReadAsRef<StatValues, StatsDataElement>(ref statsDataBuffer, readByteIndex, out _, out bool success);
//                if (success)
//                {
//                    if (baseValue != statValues.BaseValue)
//                    {
//                        statValues.BaseValue = baseValue;
//                        RecalculateStat(ref statsDataBufferLookup, ref statsDataBuffer, ref statReference);
//                    }
//                    return true;
//                }
//            }

//            return false;
//        }

//        public bool AddStatBaseValue(ref BufferLookup<StatsDataElement> statsDataBufferLookup, ref StatReference statReference, float value)
//        {
//            if (statsDataBufferLookup.TryGetBuffer(statReference.Entity, out DynamicBuffer<StatsDataElement> statsDataBuffer))
//            {
//                return AddStatBaseValue(ref statsDataBufferLookup, ref statsDataBuffer, ref statReference, value);
//            }

//            return false;
//        }

//        public bool AddStatBaseValue(ref BufferLookup<StatsDataElement> statsDataBufferLookup, ref DynamicBuffer<StatsDataElement> statsDataBuffer, ref StatReference statReference, float value)
//        {
//            if (value != 0f && UpdateStatReferenceCachedData(ref statsDataBuffer, ref statReference))
//            {
//                int readByteIndex = statReference.CachedStatStartByteIndex;
//                ref StatValues statValues = ref ByteCollectionUtility.ReadAsRef<StatValues, StatsDataElement>(ref statsDataBuffer, readByteIndex, out _, out bool success);
//                if (success)
//                {
//                    statValues.BaseValue += value;
//                    RecalculateStat(ref statsDataBufferLookup, ref statsDataBuffer, ref statReference);
//                    return true;
//                }
//            }

//            return false;
//        }

//        public bool RecalculateStat(ref BufferLookup<StatsDataElement> statsDataBufferLookup, ref StatReference statReference)
//        {
//            if (statsDataBufferLookup.TryGetBuffer(statReference.Entity, out DynamicBuffer<StatsDataElement> statsDataBuffer))
//            {
//                return RecalculateStat(ref statsDataBufferLookup, ref statsDataBuffer, ref statReference);
//            }

//            return false;
//        }

//        public bool RecalculateStat(ref BufferLookup<StatsDataElement> statsDataBufferLookup, ref DynamicBuffer<StatsDataElement> statsDataBuffer, ref StatReference statReference)
//        {
//            if (UpdateStatReferenceCachedData(ref statsDataBuffer, ref statReference))
//            {
//                // Create modifiers stack
//                TModifierStack modifierStack = new TModifierStack();
//                modifierStack.Reset();

//                // Get modifiers count and start indexes so we know which modifiers to apply
//                if (FindModifiersByteIndexDatas(
//                    ref statsDataBuffer, 
//                    statReference.CachedStatStartByteIndex, 
//                    out int observersCount, 
//                    out int observerDatasStartByteIndex, 
//                    out int modifiersStartByteIndex, 
//                    out int modifiersCount, 
//                    out int modifiersMapStartByteIndex, 
//                    out int modifierDatasStartByteIndex))
//                {
//                    // Apply all modifiers to stack
//                    TModifiersApplier modifiersApplier = new TModifiersApplier();
//                    modifiersApplier.ApplyModifiers(ref statsDataBuffer, ref modifierStack, modifiersStartByteIndex, modifiersCount);

//                    // Apply stack to stat values
//                    int readByteIndex = statReference.CachedStatStartByteIndex;
//                    ref StatValues statValues = ref ByteCollectionUtility.ReadAsRef<StatValues, StatsDataElement>(ref statsDataBuffer, readByteIndex, out _, out bool success);
//                    if (success)
//                    {
//                        float prevValue = statValues.Value;
//                        statValues.Value = statValues.BaseValue;
//                        modifierStack.Apply(statValues.BaseValue, ref statValues.Value);

//                        // Recalculate observers if value changed
//                        if (statValues.Value != prevValue)
//                        {
//                            readByteIndex = observerDatasStartByteIndex;
//                            for (int i = 0; i < observersCount; i++)
//                            {
//                                if (ByteCollectionUtility.Read(ref statsDataBuffer, readByteIndex, out readByteIndex, out StatReference observerOfStat))
//                                {
//                                    // Local observer
//                                    if (observerOfStat.Entity == statReference.Entity)
//                                    {
//                                        RecalculateStat(ref statsDataBufferLookup, ref statsDataBuffer, ref observerOfStat);
//                                    }
//                                    // Remote observer
//                                    else
//                                    {
//                                        RecalculateStat(ref statsDataBufferLookup, ref observerOfStat);
//                                    }
//                                }
//                            }
//                        }

//                        return true;
//                    }
//                }
//            }

//            return false;
//        }

//        private bool IsStatAInStatBsObserversChain(ref BufferLookup<StatsDataElement> statsDataBufferLookup, ref DynamicBuffer<StatsDataElement> statADataBuffer, ref StatReference statA, ref StatReference statB)
//        {
//            if (statsDataBufferLookup.TryGetBuffer(statB.Entity, out DynamicBuffer<StatsDataElement> statBDataBuffer))
//            {
//                return IsStatAInStatBsObserversChain(ref statsDataBufferLookup, ref statADataBuffer, ref statBDataBuffer, ref statA, ref statB);
//            }

//            return false;
//        }

//        private bool IsStatAInStatBsObserversChain(ref BufferLookup<StatsDataElement> statsDataBufferLookup, ref DynamicBuffer<StatsDataElement> statADataBuffer, ref DynamicBuffer<StatsDataElement> statBDataBuffer, ref StatReference statA, ref StatReference statB)
//        {
//            if (UpdateStatReferenceCachedData(ref statADataBuffer, ref statA) && UpdateStatReferenceCachedData(ref statBDataBuffer, ref statB))
//            {
//                // Check if stat A is in observers of stat B
//                if (FindObserversCountAndStartByteIndex(ref statADataBuffer, statB.CachedStatStartByteIndex, out int observersCount, out int observerDatasStartByteIndex))
//                {
//                    int readByteIndex = observerDatasStartByteIndex;
//                    for (int i = 0; i < observersCount; i++)
//                    {
//                        if (ByteCollectionUtility.Read(ref statADataBuffer, readByteIndex, out readByteIndex, out StatReference observerOfStatB))
//                        {
//                            if (observerOfStatB == statA)
//                            {
//                                return true;
//                            }
//                            // Check if statA is an observer of the observer of statB
//                            else
//                            {
//                                // Observer is local to B
//                                if (observerOfStatB.Entity == statB.Entity)
//                                {
//                                    if (IsStatAInStatBsObserversChain(ref statsDataBufferLookup, ref statADataBuffer, ref statBDataBuffer, ref statA, ref observerOfStatB))
//                                    {
//                                        return true;
//                                    }
//                                }
//                                // Observer is local to A
//                                else if (observerOfStatB.Entity == statA.Entity)
//                                {
//                                    if (IsStatAInStatBsObserversChain(ref statsDataBufferLookup, ref statADataBuffer, ref statADataBuffer, ref statA, ref observerOfStatB))
//                                    {
//                                        return true;
//                                    }
//                                }
//                                // Observer is other entity
//                                else
//                                {
//                                    if (IsStatAInStatBsObserversChain(ref statsDataBufferLookup, ref statADataBuffer, ref statA, ref observerOfStatB))
//                                    {
//                                        return true;
//                                    }
//                                }
//                            }
//                        }
//                    }
//                }
//            }

//            return false;
//        }

//        public bool AddModifier(ref BufferLookup<StatsDataElement> statsDataBufferLookup, ref StatReference affectedStatReference, TModifier modifier, out StatModifierReference modifierReference)
//        {
//            if (statsDataBufferLookup.TryGetBuffer(affectedStatReference.Entity, out DynamicBuffer<StatsDataElement> statsDataBuffer))
//            {
//                return AddModifier(ref statsDataBufferLookup, ref statsDataBuffer, ref affectedStatReference, modifier, out modifierReference);
//            }

//            modifierReference = default;
//            return false;
//        }

//        public bool AddModifier(ref BufferLookup<StatsDataElement> statsDataBufferLookup, ref DynamicBuffer<StatsDataElement> statsDataBuffer, ref StatReference affectedStatReference, TModifier modifier, out StatModifierReference modifierReference)
//        {
//            if (UpdateStatReferenceCachedData(ref statsDataBuffer, ref affectedStatReference))
//            {
//                // Get observed StatReferences
//                TStatReferenceList observedStatReferences = new TStatReferenceList();
//                modifier.AddObservedStatReferences(ref observedStatReferences);

//                // Prevent infinite observers loops
//                bool allObservedStatsValid = true;
//                bool wouldAddingModifierCauseInfiniteObserversLoop = false;
//                for (int i = 0; i < observedStatReferences.Length; i++)
//                {
//                    StatReference observedStatReference = observedStatReferences[i];

//                    if (UpdateStatReferenceCachedData(ref statsDataBuffer, ref observedStatReference))
//                    {
//                        // We're checking if "affectedStat" can become an observer of "observedStat".
//                        // We must validata that "observedStat" doesn't observe "affectedStat" somewhere down the observers chain.
//                        if (observedStatReference.Entity == affectedStatReference.Entity)
//                        {
//                            // Local entity stat
//                            if (IsStatAInStatBsObserversChain(ref statsDataBufferLookup, ref statsDataBuffer, ref statsDataBuffer, ref observedStatReference, ref affectedStatReference))
//                            {
//                                wouldAddingModifierCauseInfiniteObserversLoop = true;
//                                break;
//                            }
//                        }
//                        else
//                        {
//                            // Remote entity stat
//                            if (IsStatAInStatBsObserversChain(ref statsDataBufferLookup, ref statsDataBuffer, ref observedStatReference, ref affectedStatReference))
//                            {
//                                wouldAddingModifierCauseInfiniteObserversLoop = true;
//                                break;
//                            }
//                        }
//                        observedStatReferences[i] = observedStatReference;
//                    }
//                    else
//                    {
//                        allObservedStatsValid = false;
//                        break;
//                    }
//                }

//                if (allObservedStatsValid && !wouldAddingModifierCauseInfiniteObserversLoop)
//                {
//                    // Get new modifier ID
//                    if (GetAndIncrementModifierIDCounter(ref statsDataBuffer, out int modifierID))
//                    {
//                        if (FindModifiersByteIndexDatas(
//                            ref statsDataBuffer, 
//                            affectedStatReference.CachedStatStartByteIndex, 
//                            out int observersCount, 
//                            out int observerDatasStartByteIndex, 
//                            out int modifiersStartByteIndex, 
//                            out int modifiersCount, 
//                            out int modifiersMapStartByteIndex, 
//                            out int modifierDatasStartByteIndex))
//                        {
//                            // Get modifier byte index insert point (at the end of existing modifiers)
//                            int modifierInsertByteIndex = modifierDatasStartByteIndex;
//                            int readByteIndex = modifiersMapStartByteIndex;
//                            for (int i = 0; i < modifiersCount; i++)
//                            {
//                                // Skip modifier ID
//                                readByteIndex += SizeOf_ModifierID;

//                                // Read modifier size
//                                if (ByteCollectionUtility.Read(ref statsDataBuffer, readByteIndex, out readByteIndex, out int modifierSize))
//                                {
//                                    // Add size to index that started at modifier datas
//                                    modifierInsertByteIndex += modifierSize;
//                                }
//                                else
//                                {
//                                    // Error
//                                    modifierReference = default;
//                                    return false;
//                                }
//                            }
//                            int endOfModifierIDsAndSizesByteIndex = readByteIndex;

//                            // Insert new modifier data at the end of modifier datas
//                            PolymorphicElementMetaData modifierMetaData = modifier.InsertElementVariableSized(ref statsDataBuffer, modifierInsertByteIndex);
//                            if (modifierMetaData.IsValid())
//                            {
//                                // Increment version
//                                IncrementDataLayoutVersion(ref statsDataBuffer);

//                                // Update modifiers count
//                                {
//                                    ref int tmpModifiersCountRef = ref ByteCollectionUtility.ReadAsRef<int, StatsDataElement>(ref statsDataBuffer, modifiersStartByteIndex, out _, out bool success);
//                                    if (success)
//                                    {
//                                        tmpModifiersCountRef++;
//                                    }
//                                    else
//                                    {
//                                        // Error
//                                        modifierReference = default;
//                                        return false;
//                                    }
//                                }

//                                // Add modifier metaData
//                                int modifierTotalSize = modifier.GetVariableElementTotalSizeWithID();
//                                if (!ByteCollectionUtility.InsertAny(ref statsDataBuffer, endOfModifierIDsAndSizesByteIndex, new ModifierMetaData
//                                {
//                                    ModifierID = modifierID,
//                                    ModifierSize = modifierTotalSize,
//                                }))
//                                {
//                                    // Error
//                                    modifierReference = default;
//                                    return false;
//                                }

//                                // Update stat start indexes of every stat after our modifier (add modifier metadata + modifier size)
//                                UpdateStatStartByteIndexesAfterIndex(ref statsDataBuffer, affectedStatReference.CachedStatStartByteIndex, sizeof(ModifierMetaData) + modifierTotalSize);

//                                // Add affected stat as observer of modifier observed stats
//                                for (int i = 0; i < observedStatReferences.Length; i++)
//                                {
//                                    StatReference observedStatReference = observedStatReferences[i];

//                                    // Local entity stat
//                                    if (observedStatReference.Entity == affectedStatReference.Entity)
//                                    {
//                                        AddObserver(ref statsDataBufferLookup, ref statsDataBuffer, ref statsDataBuffer, ref affectedStatReference, ref observedStatReference);
//                                    }
//                                    // Remote entity stat
//                                    else
//                                    {
//                                        AddObserver(ref statsDataBufferLookup, ref statsDataBuffer, ref affectedStatReference, ref observedStatReference);
//                                    }
//                                }

//                                // Recalculate stat value, since modifiers will likely change it
//                                RecalculateStat(ref statsDataBufferLookup, ref statsDataBuffer, ref affectedStatReference);
//                            }
//                        }
//                    }
//                }
//            }

//            modifierReference = default;
//            return false;
//        }

//        public bool RemoveModifier(ref BufferLookup<StatsDataElement> statsDataBufferLookup, ref StatModifierReference modifierReference)
//        {
//            if (statsDataBufferLookup.TryGetBuffer(modifierReference.Entity, out DynamicBuffer<StatsDataElement> statsDataBuffer))
//            {
//                return RemoveModifier(ref statsDataBufferLookup, ref statsDataBuffer, ref modifierReference);
//            }

//            return false;
//        }

//        public bool RemoveModifier(ref BufferLookup<StatsDataElement> statsDataBufferLookup, ref DynamicBuffer<StatsDataElement> statsDataBuffer, ref StatModifierReference modifierReference)
//        {
//            // TODO:
//            // Find by cached or by search
//            // Remove modifier
//            // Recalc stat value

//            return false;
//        }

//        private bool AddObserver(ref BufferLookup<StatsDataElement> statsDataBufferLookup, ref DynamicBuffer<StatsDataElement> observerStatsDataBuffer, ref StatReference observerStatReference, ref StatReference observedStatReference)
//        {
//            if (statsDataBufferLookup.TryGetBuffer(observedStatReference.Entity, out DynamicBuffer<StatsDataElement> observedStatsDataBuffer))
//            {
//                return AddObserver(ref statsDataBufferLookup, ref observerStatsDataBuffer, ref observedStatsDataBuffer, ref observerStatReference, ref observedStatReference);
//            }

//            return false;
//        }

//        private bool AddObserver(ref BufferLookup<StatsDataElement> statsDataBufferLookup, ref DynamicBuffer<StatsDataElement> observerStatsDataBuffer, ref DynamicBuffer<StatsDataElement> observedStatsDataBuffer, ref StatReference observerStatReference, ref StatReference observedStatReferencee)
//        {
//            if (UpdateStatReferenceCachedData(ref observerStatsDataBuffer, ref observerStatReference) && UpdateStatReferenceCachedData(ref observedStatsDataBuffer, ref observedStatReference))
//            {
//                if (FindObserversCountAndStartByteIndex(ref statsDataBuffer, statStartByteIndex, out observersCount, out observerDatasStartByteIndex))
//                {
//                    // TODO
//                    // Modify start byte indexes on observed entity with size of added observer
//                }
//            }

//            return false;
//        }

//        private bool UpdateStatStartByteIndexesAfterIndex(ref DynamicBuffer<StatsDataElement> statsDataBuffer, int afterIndex, int changeAmount)
//        {
//            if (ByteCollectionUtility.Read(ref statsDataBuffer, StartByteIndexOfStatsCount, out int readByteIndex, out int statsCount))
//            {
//                for (int i = 0; i < statsCount; i++)
//                {
//                    // Skip stat TypeID
//                    readByteIndex += SizeOf_StatTypeID;

//                    // Read stat startByteIndex
//                    ref int tmpStatStartByteIndexRef = ref ByteCollectionUtility.ReadAsRef<int, StatsDataElement>(ref statsDataBuffer, readByteIndex, out readByteIndex, out bool success);
//                    if (success)
//                    {
//                        if (tmpStatStartByteIndexRef > afterIndex)
//                        {
//                            // Add size of inserted elements
//                            tmpStatStartByteIndexRef += changeAmount;
//                        }
//                    }
//                    else
//                    {
//                        // Error
//                        return false;
//                    }
//                }
//            }
//            else
//            {
//                // Error
//                return false;
//            }

//            return true;
//        }

//        // TODO: can be used to determine a max amount of modifiers affecting a stat
//        public bool GetModifiersCountOnStat(ref DynamicBuffer<StatsDataElement> statsDataBuffer, ref StatReference statReference, ushort modifierType)
//        {
//            return false;
//        }

//        // TODO: can be used to determine a max amount of modifiers of certain type affecting a stat
//        public bool GetModifiersCountOfTypeOnStat(ref DynamicBuffer<StatsDataElement> statsDataBuffer, ref StatReference statReference, ushort modifierType)
//        {
//            return false;
//        }
//    }
//}