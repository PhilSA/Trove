using System;
using Unity.Entities;

namespace Trove.Statemachines
{
    public interface IStateUpdateData
    {
        public void ResetForStateMachine();
    }
    
    public interface IState<TGlobalStateUpdateData, TEntityStateUpdateData> 
        where TGlobalStateUpdateData : unmanaged
        where TEntityStateUpdateData : unmanaged
    {
        public void OnStateEnter(ref StateMachine stateMachine, ref TGlobalStateUpdateData globalData, ref TEntityStateUpdateData entityData);
        public void OnStateExit(ref StateMachine stateMachine, ref TGlobalStateUpdateData globalData, ref TEntityStateUpdateData entityData);
        public void Update(ref StateMachine stateMachine, ref TGlobalStateUpdateData globalData, ref TEntityStateUpdateData entityData);
    }
    
    public struct StateMachine : IComponentData
    {
        public StateHandle CurrentStateHandle;
    }
    
    [InternalBufferCapacity(0)]
    public struct StateData : IBufferElementData
    {
        public int Version;

        public bool Exists()
        {
            return Version > 0;
        }
    }
    
    public struct StateHandle : IEquatable<StateHandle>
    {
        public int Index;
        public int Version;

        public static StateHandle Null = default;
        
        public StateHandle(int index, int version)
        {
            Index = index;
            Version = version;
        }
        
        public bool Exists()
        {
            return Version > 0 && Index >= 0;
        }

        public bool Equals(StateHandle other)
        {
            return Index == other.Index && Version == other.Version;
        }

        public override bool Equals(object obj)
        {
            return obj is StateHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Index, Version);
        }

        public static bool operator ==(StateHandle left, StateHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StateHandle left, StateHandle right)
        {
            return !left.Equals(right);
        }
    }
}
