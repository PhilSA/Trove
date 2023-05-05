using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Trove.UtilityAI
{
    [Serializable]
    public class ConsiderationDefinitionAuthoring
    {
        public ParametricCurveAuthoring ParametricCurveAuthoring;

        public static ConsiderationDefinitionAuthoring GetDefault(float minY = float.MinValue, float maxY = float.MaxValue)
        {
            return new ConsiderationDefinitionAuthoring
            {
                ParametricCurveAuthoring = ParametricCurveAuthoring.GetDefault(minY, maxY),
            };
        }

        public BlobAssetReference<ConsiderationDefinition> ToConsiderationDefinition(IBaker baker)
        {
            BlobBuilder builder = new BlobBuilder(Allocator.Temp);
            ref ConsiderationDefinition definition = ref builder.ConstructRoot<ConsiderationDefinition>();
            definition = new ConsiderationDefinition(this);
            BlobAssetReference<ConsiderationDefinition> blobReference = builder.CreateBlobAssetReference<ConsiderationDefinition>(Allocator.Persistent);
            baker.AddBlobAsset(ref blobReference, out var hash);
            builder.Dispose();

            return blobReference;
        }
    }
}