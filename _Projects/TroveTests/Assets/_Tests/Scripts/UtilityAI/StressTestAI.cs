using System;
using System.Collections;
using System.Collections.Generic;
using Trove.UtilityAI;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct StressTestAI : IComponentData
{
    public int UpdateEveryXTick;
    public Unity.Mathematics.Random Random;

    public ConsiderationReference A0C0Ref;
    public ConsiderationReference A0C1Ref;
    public ConsiderationReference A0C2Ref;
    public ConsiderationReference A0C3Ref;
    public ConsiderationReference A0C4Ref;

    public ConsiderationReference A1C0Ref;
    public ConsiderationReference A1C1Ref;
    public ConsiderationReference A1C2Ref;
    public ConsiderationReference A1C3Ref;
    public ConsiderationReference A1C4Ref;

    public ConsiderationReference A2C0Ref;
    public ConsiderationReference A2C1Ref;
    public ConsiderationReference A2C2Ref;
    public ConsiderationReference A2C3Ref;
    public ConsiderationReference A2C4Ref;

    public ConsiderationReference A3C0Ref;
    public ConsiderationReference A3C1Ref;
    public ConsiderationReference A3C2Ref;
    public ConsiderationReference A3C3Ref;
    public ConsiderationReference A3C4Ref;

    public ConsiderationReference A4C0Ref;
    public ConsiderationReference A4C1Ref;
    public ConsiderationReference A4C2Ref;
    public ConsiderationReference A4C3Ref;
    public ConsiderationReference A4C4Ref;

    public ConsiderationReference A5C0Ref;
    public ConsiderationReference A5C1Ref;
    public ConsiderationReference A5C2Ref;
    public ConsiderationReference A5C3Ref;
    public ConsiderationReference A5C4Ref;

    public ConsiderationReference A6C0Ref;
    public ConsiderationReference A6C1Ref;
    public ConsiderationReference A6C2Ref;
    public ConsiderationReference A6C3Ref;
    public ConsiderationReference A6C4Ref;

    public ConsiderationReference A7C0Ref;
    public ConsiderationReference A7C1Ref;
    public ConsiderationReference A7C2Ref;
    public ConsiderationReference A7C3Ref;
    public ConsiderationReference A7C4Ref;

    public ConsiderationReference A8C0Ref;
    public ConsiderationReference A8C1Ref;
    public ConsiderationReference A8C2Ref;
    public ConsiderationReference A8C3Ref;
    public ConsiderationReference A8C4Ref;

    public ConsiderationReference A9C0Ref;
    public ConsiderationReference A9C1Ref;
    public ConsiderationReference A9C2Ref;
    public ConsiderationReference A9C3Ref;
    public ConsiderationReference A9C4Ref;
}

[Serializable]
public struct StressTestAIConfig : IComponentData
{
    public Entity Prefab;
    public int SpawnCount;
}

[Serializable]
[InternalBufferCapacity(100)]
public struct StressTestAIConsiderationReferences : IBufferElementData
{
    public ConsiderationReference Ref;
}
