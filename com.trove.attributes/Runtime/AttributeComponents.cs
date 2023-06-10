using System;
using Unity.Entities;

namespace Trove.Attributes
{
    [Serializable]
    public struct AttributesOwner : IComponentData
    {
        public uint ModifierIDCounter;
    }

    [Serializable]
    public partial struct AttributeObserver : IBufferElementData
    {
        public AttributeReference ObserverAttribute;
        public int ObservedAttributeType;
        public uint Count;
    }

    [Serializable]
    public struct ModifierReference
    {
        public AttributeReference AffectedAttribute;
        public uint ID;
    }

    [Serializable]
    public struct ModifierReferenceNotification : IBufferElementData
    {
        public ModifierReference ModifierReference;
        public int NotificationID;
    }
}