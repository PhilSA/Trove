
# Advanced

**Table of Contents**
* [Netcode](#netcode)
* [Non-instant state transitions](#non-instant-state-transitions)
* [State inheritance and composition](#state-inheritance-and-composition)
* [Multiple state updates](#multiple-state-updates)
* [State Machines as coroutines](#state-machines-as-coroutines)


## Netcode

State machines can be made compatible with netcode and prediction. Simply create ghost variants for the `StateMachine` component and your `DynamicBuffer<MyState>`, and make sure all fields are ghost fields. Note however that since `MyState.State` will be a polymorphic struct, you must make your state polymorphic structs use the "Merged Fields" approach. See [netcode for polymorphic structs](https://github.com/PhilSA/Trove/blob/main/com.trove.polymorphicstructs/Documentation~/netcode.md) for more details.

Then make your state update system run in the prediction system group, and you're good to go.


## Non-instant state transitions

You can create non-instant state transitions by turning the transition itself into a state. A transition from `StateA` to `StateB` could involve:
* Creating a new `TransitionState`.
* When`StateA` is ready to transition to `StateB`, it gets the `TransitionState` from its handle, sets a "from" and "to" state handle in it, and transitions to it.
* Now `TransitionState` is the active state, and it remembers it's transitioning from `StateA` to `StateB`. It handles transition logic, and calls a transition to its "to" state when ready.

Here's an example of a timed transition called from `StateA`:
```cs
[PolymorphicStruct] 
public struct StateA : IMySMState
{
    // (...)
    public StateHandle SelfState;
    public StateHandle TargetState;
    public StateHandle TimedTransitionState;
    
    public void OnStateEnter(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
        // (...)
    } 

    public void OnStateExit(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
        // (...)
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
        // (...)

        // .....when ready to transition:
        if(StateMachineUtilities.TryGetState<MySMState, MySMGlobalStateUpdateData, MySMEntityStateUpdateData>(ref entityData.StatesBuffer, TimedTransitionState, out MySMState timedTransitionState))
        {   
            // First, set some data in the TimedTransitionState:
            timedTransitionState.State = new TimedStateTransition
            {
                Duration = 1f,
                FromState = SelfState,
                ToState = TargetState,
            };
            StateMachineUtilities.TrySetState<MySMState, MySMGlobalStateUpdateData, MySMEntityStateUpdateData>(ref entityData.StatesBuffer, TimedTransitionState, timedTransitionState);

            // Then transition to the TimedStateTransition state
            StateMachineUtilities.TryStateTransition(ref stateMachine, ref entityData.StatesBuffer, ref globalData, ref entityData, TimedTransitionState);
        }
    }
}

[PolymorphicStruct] 
public struct TimedStateTransition : IMySMState
{
    public float Timer;
    public float Duration;
    public StateHandle FromState;
    public StateHandle ToState;
    
    public void OnStateEnter(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
        Timer = 0f;
        entityData.StateBlendRatio = 0f;
    } 

    public void OnStateExit(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
        entityData.StateBlendRatio = 1f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
        Timer += globalData.DeltaTime;

        // HERE, you could even implement some update logic that merges the updates of the "From" and "To" states. In this case, we calculate
        // a "StateBlendRatio" based on transition normalized time, store it in our "entityData", and call the "Update()" function of our "From" 
        // and "To" states. These updates can take the "StateBlendRatio" into account
        {
            if(StateMachineUtilities.TryGetState<MySMState, MySMGlobalStateUpdateData, MySMEntityStateUpdateData>(ref entityData.StatesBuffer, FromState, out MySMState fromState) &&
               StateMachineUtilities.TryGetState<MySMState, MySMGlobalStateUpdateData, MySMEntityStateUpdateData>(ref entityData.StatesBuffer, ToState, out MySMState toState))
            {
                entityData.StateBlendRatio = math.saturate(Timer / Duration);
                fromState.Update(ref stateMachine, ref globalData, ref entityData);
                entityData.StateBlendRatio = 1f - entityData.StateBlendRatio;
                toState.Update(ref stateMachine, ref globalData, ref entityData);

                StateMachineUtilities.TrySetState<MySMState, MySMGlobalStateUpdateData, MySMEntityStateUpdateData>(ref entityData.StatesBuffer, FromState, fromState);
                StateMachineUtilities.TrySetState<MySMState, MySMGlobalStateUpdateData, MySMEntityStateUpdateData>(ref entityData.StatesBuffer, ToState, toState);
            }
        }

        if(Timer >= Duration)
        {
            StateMachineUtilities.TryStateTransition(ref stateMachine, ref entityData.StatesBuffer, ref globalData, ref entityData, ToState);
        }
    }
}
```

Notice the comment in the `Update()` function, that explains that you can handle a transition update that merges the updates of the "From" and "To" states.



## State inheritance and composition

Some patterns can facilitate code re-use across states.

#### State inheritance

You can mimmick a `StateA` inheriting from a `StateB` by making `StateA` store a `StateB` field, and calling all of `StateB`'s state functions:
```cs
[PolymorphicStruct] 
public struct StateB : IMySMState
{
    public float Timer;
    
    public void OnStateEnter(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
        Timer = 0f;
    } 

    public void OnStateExit(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
        Timer += globalData.DeltaTime;
    }
}

[PolymorphicStruct] 
public struct StateA : IMySMState
{
    public StateB StateB; // HERE: StateB is stored in StateA, giving StateA the "Timer" functionality of StateB as well.
    
    public void OnStateEnter(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
        StateB.OnStateEnter(ref stateMachine, ref globalData, ref entityData);
    } 

    public void OnStateExit(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
        StateB.OnStateExit(ref stateMachine, ref globalData, ref entityData);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
        StateB.Update(ref stateMachine, ref globalData, ref entityData);
    }
}
```

You can create chains of inheritance this way. For example you could create a `StateC` that `StateB` "inherits" from just like `StateA` "inherits" from `StateB`. `StateA` would then inherit both from `StateB` and `StateC`.

#### State Composition

You may also choose to handle code reuse across states by composition rather than inheritance. You could create several reusable state "Modules" that implement all the state functions, and add several modules to your states:
```cs
[PolymorphicStruct] 
public struct StateA : IMySMState
{
    public TimerModule TimerModule;
    public MovePositionModule MovePositionModule;
    
    public void OnStateEnter(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
        TimerModule.OnStateEnter(ref stateMachine, ref globalData, ref entityData);
        MovePositionModule.OnStateEnter(ref stateMachine, ref globalData, ref entityData);
    } 

    public void OnStateExit(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
        TimerModule.OnStateExit(ref stateMachine, ref globalData, ref entityData);
        MovePositionModule.OnStateExit(ref stateMachine, ref globalData, ref entityData);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
        TimerModule.Update(ref stateMachine, ref globalData, ref entityData);
        MovePositionModule.Update(ref stateMachine, ref globalData, ref entityData);
    }
}

[PolymorphicStruct] 
public struct StateB : IMySMState
{
    public TimerModule TimerModule;
    
    public void OnStateEnter(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
        TimerModule.OnStateEnter(ref stateMachine, ref globalData, ref entityData);
    } 

    public void OnStateExit(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
        TimerModule.OnStateExit(ref stateMachine, ref globalData, ref entityData);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
        TimerModule.Update(ref stateMachine, ref globalData, ref entityData);
    }
}
```


## Multiple state updates

It's possible to require multiple different state "Update()" functions for your state machine. For example, states might need to do a certain thing on regular update, and something else on fixed update. There are 2 main ways you could choose to handle this.

#### Update Id
Add a `public int UpdateId;` field to your state machine's `GlobalStateUpdateData` struct. This represents what type of update is requested. In regular update, your system calling the state update would set this to `0`, and in fixed update it would set it to `1`, etc... Then in your state updates, simply do a different thing based on if the `UpdateId` is `0` or `1`, with a `switch` statement for example.

#### New polymorphic update method
Add a `FixedUpdate()` to your polymorphic state interface (it should be named `IMySMState` if you named your state machine file `MySM` when creating it from the template):
```cs
[PolymorphicStructInterface]
public interface IMySMState : IState<MySMGlobalStateUpdateData, MySMEntityStateUpdateData>
{
    public void FixedUpdate(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData);
}
```

Then make your states implement it:
```cs
[PolymorphicStruct] 
public struct MySMStateA : IMySMState
{
    public void OnStateEnter(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    { } 

    public void OnStateExit(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    { }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FixedUpdate(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData, ref MySMEntityStateUpdateData entityData)
    {
    }
}
```

Then make your encompassing state buffer element call it on the polymorphic struct:
```cs
[InternalBufferCapacity(8)] // TODO: tweak internal capacity
public struct MySMState : IBufferElementData, IPoolObject, IState<MySMGlobalStateUpdateData, MySMEntityStateUpdateData>
{
    // Required for VersionedPool handling. Determines if the state exists in the states pool.
    public int Version { get; set; }
    // This is the generated polymorphic state struct, based on the IMySMStateMachineState polymorphic interface
    public PolyMySMState State;

    public void OnStateEnter(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData,
        ref MySMEntityStateUpdateData entityData)
    {
        State.OnStateEnter(ref stateMachine, ref globalData, ref entityData);
    }

    public void OnStateExit(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData,
        ref MySMEntityStateUpdateData entityData)
    {
        State.OnStateExit(ref stateMachine, ref globalData, ref entityData);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData,
        ref MySMEntityStateUpdateData entityData)
    {
        State.Update(ref stateMachine, ref globalData, ref entityData);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FixedUpdate(ref StateMachine stateMachine, ref MySMGlobalStateUpdateData globalData,
        ref MySMEntityStateUpdateData entityData)
    {
        State.FixedUpdate(ref stateMachine, ref globalData, ref entityData);
    }
}
```

And finally, call your state machine `FixedUpdate` from a system/job with this static function:
```cs        
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void StateMachineFixedUpdate(
    ref StateMachine stateMachine,
    ref DynamicBuffer<MySMState> statesBuffer,
    ref MySMGlobalStateUpdateData globalStateUpdateData,
    ref MySMEntityStateUpdateData entityStateUpdateData)
{
    MySMState nullState = default;
    ref MySMState currentState = ref Pool.TryGetObjectRef(ref statesBuffer, stateMachine.CurrentStateHandle.Handle, out bool success, ref nullState);
    if (success)
    {
        currentState.FixedUpdate(ref stateMachine, ref globalStateUpdateData, ref entityStateUpdateData);
    }
}
``` 

> Note: you could also use this concept of adding new polymorphic methods to states in order implement advanced state transition logic, where a state may need to "ask" another state if it's ready to transition, before actually transitioning. In this case, you can add a new `CanTransition()` polymorphic function to states.


## State machines as Coroutines

A coroutine is, essentially, a state machine. It's a thing that updates, but the update logic goes through different "states" over time.

For example, consider this Coroutine that makes something rotate towards a target, then wait 1s once it has started facing the target, then do some action:
```cs
IEnumerator ExampleCoroutine()
{
    while(!reachedTargetRotation)
    {
        RotateTowardsTarget();
        yield return null;
    }

    yield return new WaitForSeconds(1f);

    DoAction();    
}
```

This coroutine can be modeled as this state machine:
* Start with a `RotateTowardsTarget` state. In its `Update()`, it performs the `RotateTowardsTarget()` logic, and if it detects that it has reached the target rotation, it triggers a transition to the next state `WaitState`.
* `WaitState` is a simple timer state that triggers a transition to the next state once the 1s timer is reached. It transitions to `DoActionState`.
* `DoActionState` performs the action in its `OnStateEnter()`, and then ends the state machine by setting the `CurrentStateHandle` to `default` in the `stateMachine`. It doesn't need any `Update()` logic.