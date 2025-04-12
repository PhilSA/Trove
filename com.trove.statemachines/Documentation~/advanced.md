
# Advanced

**Table of Contents**
* [Netcode](#netcode)
* [State inheritance and composition](#state-inheritance-and-composition)
* [Multiple state updates](#multiple-state-updates)


## Netcode

State machines can be made compatible with netcode and prediction. Simply create ghost variants for the `StateMachine` component and your `DynamicBuffer<MyState>`, and make sure all fields are ghost fields. Note however that since `MyState` will be a polymorphic struct, you must follow the guidance on [netcode for polymorphic structs](https://github.com/PhilSA/Trove/blob/main/com.trove.polymorphicstructs/Documentation~/netcode.md).

Then make your state update system run in the prediction system group, and you're good to go.


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