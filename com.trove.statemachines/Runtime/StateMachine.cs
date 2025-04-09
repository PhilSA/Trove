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
        public StateHandle InitialState;
        public StateHandle CurrentStateHandle;

        public MultiLinkedListPool ChildStates;
        
        public byte HasInitialized;
    }
    
    public struct StateHandle : IEquatable<StateHandle>
    {
        public Pool.ObjectHandle Handle;
        
        public bool Exists()
        {
            return Handle.Exists();
        }

        public bool Equals(StateHandle other)
        {
            return Handle.Equals(other.Handle);
        }

        public override bool Equals(object obj)
        {
            return obj is StateHandle other && Handle.Equals(other.Handle);
        }

        public override int GetHashCode()
        {
            return Handle.GetHashCode();
        }

        public static bool operator ==(StateHandle left, StateHandle right)
        {
            return left.Handle.Equals(right.Handle);
        }

        public static bool operator !=(StateHandle left, StateHandle right)
        {
            return !left.Handle.Equals(right.Handle);
        }
    }
}
