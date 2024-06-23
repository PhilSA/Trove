using System.Runtime.CompilerServices;
using Unity.Assertions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Trove.ObjectHandles
{
    public static partial class ValueObjectManager
    {
        private const float ObjectsCapacityGrowFactor = 2f;
    }
}