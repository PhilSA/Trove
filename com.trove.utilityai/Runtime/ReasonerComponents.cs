using System;
using Unity.Entities;

namespace Trove.UtilityAI
{
    public interface IIDElement
    {
        public int ID { get; }
    }

    [Serializable]
    public struct Reasoner : IComponentData
    {
        public int __internal__actionsVersion;
        public int __internal__considerationsVersion;
        public int __internal__actionIDCounter;
        public int __internal__considerationIDCounter;
        public int __internal__highestActionConsiderationsCount;

        public byte __internal__actionIDCounterHasLooped;
        public byte __internal__considerationIDCounterHasLooped;
        public byte __internal__mustRecomputeHighestActionConsiderationsCount;
    }

    [Serializable]
    [InternalBufferCapacity(0)]
    public partial struct Action : IBufferElementData, IIDElement
    {
        public int Type;
        public int IndexInType;
        public float ScoreMultiplier;

        public byte __internal__flags;
        public int __internal__id;
        public float __internal__latestScoreWithoutMultiplier;

        public int ID => __internal__id;
        public float Score => __internal__latestScoreWithoutMultiplier * ScoreMultiplier;
        public float ScoreWithoutMultiplier => __internal__latestScoreWithoutMultiplier;
        public bool IsCreated { get { return BitUtilities.GetBit(__internal__flags, ReasonerUtilities.IsCreatedBitPosition); } set { BitUtilities.SetBit(value, ref __internal__flags, ReasonerUtilities.IsCreatedBitPosition); } }
        public bool IsEnabled { get { return BitUtilities.GetBit(__internal__flags, ReasonerUtilities.IsEnabledBitPosition); } set { BitUtilities.SetBit(value, ref __internal__flags, ReasonerUtilities.IsEnabledBitPosition); } }
    }

    [Serializable]
    [InternalBufferCapacity(0)]
    public partial struct ConsiderationInput : IBufferElementData
    {
        public float __internal__input;
    }

    [Serializable]
    [InternalBufferCapacity(0)]
    public partial struct Consideration : IBufferElementData, IIDElement
    {
        public byte __internal__flags;
        public int __internal__actionIndex;
        public int __internal__id;
        public BlobAssetReference<ConsiderationDefinition> Definition;

        public int ID => __internal__id;
        public bool IsCreated { get { return BitUtilities.GetBit(__internal__flags, ReasonerUtilities.IsCreatedBitPosition); } set { BitUtilities.SetBit(value, ref __internal__flags, ReasonerUtilities.IsCreatedBitPosition); } }
        public bool IsEnabled { get { return BitUtilities.GetBit(__internal__flags, ReasonerUtilities.IsEnabledBitPosition); } set { BitUtilities.SetBit(value, ref __internal__flags, ReasonerUtilities.IsEnabledBitPosition); } }
    }

    [Serializable]
    public struct ActionReference
    {
        public byte __internal__isCreated;
        public int __internal__id;
        public int __internal__index;
        public int __internal__actionsVersion;

        public int ID => __internal__id;
        public bool IsCreated { get { return __internal__isCreated == 1; } }
    }

    [Serializable]
    public struct ConsiderationReference
    {
        public byte __internal__isCreated;
        public int __internal__id;
        public int __internal__index;
        public int __internal__considerationsVersion;

        public int ID => __internal__id;
        public bool IsCreated { get { return __internal__isCreated == 1; } }
    }

    [Serializable]
    public struct ActionDefinition
    {
        public int Type;
        public int Index;
        public float ScoreMultiplier;

        public ActionDefinition(int type, int index = 0, float scoreMultiplier = 1f)
        {
            Type = type;
            Index = index;
            ScoreMultiplier = scoreMultiplier;
        }
    }

    [Serializable]
    public struct ConsiderationDefinition
    {
        public ParametricCurve Curve;

        public ConsiderationDefinition(ConsiderationDefinitionAuthoring authoring)
        {
            Curve = authoring.ParametricCurveAuthoring.ParametricCurve;
        }
    }
}