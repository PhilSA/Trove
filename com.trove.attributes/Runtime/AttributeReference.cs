using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Trove.Attributes
{
    public struct AttributeReference
    {
        public Entity Entity;
        public int AttributeType;

        public AttributeReference(Entity entity, int attributeType)
        {
            Entity = entity;
            AttributeType = attributeType;
        }
    }
}