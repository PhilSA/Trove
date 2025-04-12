
# How it Works

In order to explain how all of this works, we will go through a hands-on example of creating a state machine that moves, rotates, and scales a cube.

**Table of Contents**
* [The state machine template](#the-state-machine-template)
* [Getting a basic state machine working in a scene](#getting-a-basic-state-machine-working-in-a-scene)
* [Making states affect entity transform](#making-states-affect-entity-transform)
* [Turning it into a hierarchical state machine](#turning-it-into-a-hierarchical-state-machine)


## The state machine template

Your starting point for creating state machines is to create one from the State Machine template. You can do so with `Right Click in Project window > Create > Trove > StateMachines > New State Machine`.

Here are the main pieces of the template, assuming the we name the new file "**CubeSM**":

> Note: Trove State Machines relies on functionality provided by the [Trove Polymorphic Structs](https://github.com/PhilSA/Trove/blob/main/com.trove.polymorphicstructs/README.md) package, for generating polymorphic structs.

* `CubeSMState`: This is the main struct that represents a state of your state machine. It can be stored in a `DynamicBuffer`, and encompasses the code-generated `PolyCubeSMState` polymorphic struct, which allows it to take on the form of any of the various state types of this state machine.
* `ICubeSMState`: This is the polymorphic interface that represents the common methods that the states of your state machine should have. 
* `CubeSMStateA` and `CubeSMStateB`: These are example implementations of specific states of the state machine. They both implement the `ICubeSMState` interface, but they can contain different data.
* `CubeSMGlobalStateUpdateData`: Represents the "global" data (data not belonging to the entity of the state machine) that state updates should have access to. This is passed as parameter to the `ICubeSMState` interface methods. Here you can store time data, singleton data, native collections, component lookups, etc... if your states might need access to them.
* `CubeSMEntityStateUpdateData`: Represents the entity data (data belonging to the entity of the state machine) that state updates should have access to. This is passed as parameter to the `ICubeSMState` interface methods.
* `ExampleCubeSMSystem`: An example of a system that updates state machines on entities. It schedules an `IJobEntity` that iterates entities that have a `StateMachine` and a `DynamicBuffer<CubeSMState>`, and calls `StateMachineUtilities.Update()` on those.
* `CubeSMAuthoring`: An example of an authoring component that sets up a state machine on an entity, using `StateMachineUtilities`. 

The next step you should take is to reorganize these into more manageable files. Most importantly, `CubeSMAuthoring` must be moved to a file that has this exact name, since it is a `MonoBehaviour`. But a recommended setup would be:
* "**CubeSMAuthoring.cs**": your state machine authoring (`CubeSMAuthoring`).
* "**ExampleCubeSMSystem.cs**": your state machine update system (`ExampleCubeSMSystem`).
* "**CubeSMStateMachine.cs**": the interfaces, components, and update datas that define your state machine: 
    * `ICubeSMState`, `CubeSMState`, `CubeSMGlobalStateUpdateData`, `CubeSMEntityStateUpdateData`
* A "CubeSMStates" folder containing a file for each of the individual CubeSM states: `CubeSMStateA`, `CubeSMStateB`, etc...


## Getting a basic state machine working in a scene

Create a new cube GameObject in a SubScene, and put a `CubeSMAuthoring` component on it. If you look at the resulting entity in the Entities Hierarchy window, you'll see the entity has a `StateMachine` component and a `DynamicBuffer<CubeSMState>`.

You'll notice there are 8 buffer elements in the `DynamicBuffer<CubeSMState>`, even though the `CubeSMAuthoring` baking code currently only creates 2 states. This is because the states dynamic buffer acts as a "pool" of states where once we add a state at a certain index, it is guaranteed to always remain at that index, no matter how many other states we remove. 8 is its starting capacity, but it will grow automatically. This characteristic is what enables us to always keep a reliable "reference" to a specific state, using the `StateHandle` struct outputted when creating states. Elements in the `DynamicBuffer<CubeSMState>` buffer can therefore represent a "free available slot" in which to store a state. Not all elements of the buffer are necessarily valid states. You can check if a state element actually exists with the `Trove.Pool.Exists()` method.

> Note: because the `DynamicBuffer<CubeSMState>` is managed by the Trove Pool utility, it is very important that you never manually add, remove, clear, get, or set elements in it directly. Always go through the various `StateMachineUtilities` instead:
* `StateMachineUtilities.CreateState`
* `StateMachineUtilities.TryDestroyState`
* `StateMachineUtilities.TryGetState`
* `StateMachineUtilities.TrySetState`

We'll now make the cube state machine alternate between `CubeSMStateA` and `CubeSMStateB` based on timers.

First, we need to modify our authoring component so each state remember which state it should transition to:
* In both `CubeSMStateA` and `CubeSMStateB`, add a `public StateHandle TargetState;` field, and a `public float Duration;` field.
* In `CubeSMAuthoring`, where we set the state datas using `StateMachineUtilities.TrySetState`, assign the `stateBHandle` as the StateA's `TargetState`, and assign the `stateAHandle` as StateB's `TargetState`. Also assign `Duration` for both (we'll use durations of 1f and 2f for this example).

Now we need to handle the Timer logic in our states:
* Add a `public float Timer;` field to both `CubeSMStateA` and `CubeSMStateB`.
* In `OnStateEnter()` of both states, set the `Timer` to 0f. So this resets the timer when we ente the state.
* In `Update()` of both states, update the `Timer` by adding `globalData.DeltaTime`. Then add a condition that if the `Timer` is past a certain value, we call `StateMachineUtilities.TryStateTransition` to transition to the `TargetState`.
* Add some `Debug.Log()`s in `OnStateEnter()`, `OnStateExit()`, and when calling state transitions, so you can see transitions happening during play.

The final code for the authoring and states is shown below:

Authoring (just the part where we set state data):
```cs
StateMachineUtilities.TrySetState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
    ref statesBuffer,
    stateAHandle,
    new CubeSMState
    {
        State = new CubeSMStateA
        {
            TargetState = stateBHandle,
            Duration = Duration,
        },
    });
StateMachineUtilities.TrySetState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
    ref statesBuffer,
    stateBHandle,
    new CubeSMState
    {
        State = new CubeSMStateB
        {
            TargetState = stateAHandle,
            Duration = 2f,
        },
    });
```

States:
```cs
[PolymorphicStruct] 
public struct CubeSMStateA : ICubeSMState
{
    public StateHandle TargetState;
    public float Duration;
    
    public float Timer;
    
    public void OnStateEnter(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        Timer = 0f;
        UnityEngine.Debug.Log("State A Enter");
    } 

    public void OnStateExit(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        UnityEngine.Debug.Log("State A Exit");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        Timer += globalData.DeltaTime;

        if (Timer > Duration)
        {
            UnityEngine.Debug.Log("State A Transition to B");
            StateMachineUtilities.TryStateTransition(ref stateMachine, ref entityData.StatesBuffer, ref globalData, ref entityData, TargetState);
        }
    }
}
```

```cs
[PolymorphicStruct]
public struct CubeSMStateB : ICubeSMState 
{
    public StateHandle TargetState;
    public float Duration;
    
    public float Timer;
    
    public void OnStateEnter(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        Timer = 0f;
        UnityEngine.Debug.Log("State B Enter");
    }

    public void OnStateExit(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        UnityEngine.Debug.Log("State B Exit");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        Timer += globalData.DeltaTime;

        if (Timer > 2f)
        {
            UnityEngine.Debug.Log("State B Transition to A");
            StateMachineUtilities.TryStateTransition(ref stateMachine, ref entityData.StatesBuffer, ref globalData, ref entityData, TargetState);
        }
    }
}
```

At this point, you can press Play. If you look at the console, you should see the state transition messages. The logs should read:
* `State A Enter`
* `State A Transition to B`
* `State A Exit`
* `State B Enter`
* `State B Transition to A`
* `State B Exit`
* `State A Enter`
* ....

An interesting characteristic to notice here is that during state transitions, the state that we transition from always calls its `OnStateExit` before the state we transition to calls its `OnStateEnter`. This is a guarantee of this state machine system, and it can be exploited in your code.


## Making states affect entity transform

We will now turn `CubeSMStateA` into a state that moves the cube position, and `CubeSMStateB` into a state that rotates the cube. Rename `CubeSMStateA` to `CubeSMPositionState`, and `CubeSMRotationState`.

Our states will need read/write access to the entity's `LocalTransform` component. To achieve this, we will have to add the `LocalTransform` as a `RefRW<LocalTransform>` field in the `CubeSMEntityStateUpdateData` struct. Then, our `ExampleCubeSMSystem` will need to assign that data to the `CubeSMEntityStateUpdateData` before updating the state machine. Look for the `// HERE` comments in the code below:
```cs
public struct CubeSMEntityStateUpdateData
{
    public Entity Entity;
    public DynamicBuffer<CubeSMState> StatesBuffer;
    public RefRW<LocalTransform> LocalTransformRef; // HERE we add a RefRW<LocalTransform> field
    
    public CubeSMEntityStateUpdateData(
        Entity entity,
        DynamicBuffer<CubeSMState> statesBuffer,
        RefRW<LocalTransform> localTransformRef) // HERE we add this field to the constructor so we don't forget to assign it
    {
        Entity = entity;
        StatesBuffer = statesBuffer;
        LocalTransformRef = localTransformRef; // HERE we assign it
    }
}
```

```cs
[BurstCompile]
public partial struct CubeSMUpdateJob : IJobEntity
{
    public CubeSMGlobalStateUpdateData GlobalData;
    
    public void Execute(
        Entity entity, 
        ref StateMachine stateMachine, 
        ref DynamicBuffer<CubeSMState> statesBuffer,
        RefRW<LocalTransform> localTransformRef) // HERE we add a RefRW<LocalTransform> to our IJobEntity's query
    {
        // Here we build the per-entity data
        CubeSMEntityStateUpdateData entityData = new CubeSMEntityStateUpdateData(
            entity, 
            statesBuffer,
            localTransformRef); // HERE we assign the RefRW<LocalTransform> to our `CubeSMEntityStateUpdateData`

        // Update the state machine
        StateMachineUtilities.Update(ref stateMachine, ref statesBuffer, ref GlobalData, ref entityData);
    }
}
```

At this point, we need to modify our states logic so they handle position/rotation movement. This will be explained in the code below with the `// HERE` comments:
```cs
[PolymorphicStruct] 
public struct CubeSMPositionState : ICubeSMState
{
    public StateHandle TargetState;
    public float Duration;
    public float PositionOffset; // HERE: field representing how much position movement our state applies
    
    public float Timer;
    public float3 StartPosition;
    public float3 RandomDirection;
    
    public void OnStateEnter(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        // HERE: When we enter the state, remember our StartPosition, and set a random direction based on the entity Index
        Timer = 0f;
        StartPosition = entityData.LocalTransformRef.ValueRW.Position;
        RandomDirection = Unity.Mathematics.Random.CreateFromIndex((uint)entityData.Entity.Index).NextFloat3Direction();
    } 

    public void OnStateExit(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        // HERE: When we exit the state, reset our entity's position to the remembered StartPosition
        entityData.LocalTransformRef.ValueRW.Position = StartPosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        Timer += globalData.DeltaTime;

        // HERE: In our state update, move the position based on a sine function of our normalized state time
        float normTime = math.saturate(Timer / Duration);
        entityData.LocalTransformRef.ValueRW.Position = StartPosition + math.lerp(float3.zero, RandomDirection * PositionOffset, math.sin(normTime * math.PI));
        
        if (Timer >= Duration)
        {
            StateMachineUtilities.TryStateTransition(ref stateMachine, ref entityData.StatesBuffer, ref globalData, 
                ref entityData, TargetState);
        }
    }
}
```

```cs
[PolymorphicStruct]
public struct CubeSMRotationState : ICubeSMState 
{
    public StateHandle TargetState;
    public float Duration;
    public float RotationSpeed; // HERE: field representing rotation speed
    
    public float Timer;
    public float3 RandomDirection;
    
    public void OnStateEnter(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        // HERE: When we enter the state, remember a random direction based on entity index
        Timer = 0f;
        RandomDirection = Unity.Mathematics.Random.CreateFromIndex((uint)entityData.Entity.Index).NextFloat3Direction();
    }

    public void OnStateExit(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        Timer += globalData.DeltaTime;
        
        // HERE: In our state update, rotate the entity by RotationSpeed along the axis represented by our RandomDirection
        entityData.LocalTransformRef.ValueRW.Rotation = math.mul(
            quaternion.Euler(RandomDirection * RotationSpeed * globalData.DeltaTime), entityData.LocalTransformRef.ValueRW.Rotation);
            
        if (Timer >= Duration)
        {
            StateMachineUtilities.TryStateTransition(ref stateMachine, ref entityData.StatesBuffer, ref globalData, 
                ref entityData, TargetState);
        }
    }
}
```

The final step is to assign data to our states in baking:
```cs
StateMachineUtilities.TrySetState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
    ref statesBuffer,
    stateAHandle,
    new CubeSMState
    {
        State = new CubeSMPositionState
        {
            TargetState = stateBHandle,
            Duration = 1f,
            PositionOffset = 2f, // HERE: position offset
        },
    });
StateMachineUtilities.TrySetState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
    ref statesBuffer,
    stateBHandle,
    new CubeSMState
    {
        State = new CubeSMRotationState
        {
            TargetState = stateAHandle,
            Duration = 2f,
            RotationSpeed = 3f, // HERE: rotation speed
        },
    });
```

Now press Play. You should see the cube move for 1s, then rotate for 2s. You can duplicate your cube object in the scene to create more cubes. Each will have a random move direction and rotation axis, since our randoms are based on the entity index.


## Turning it into a hierarchical state machine

We will now make the state machine hierarchical. We'll make it so that while we are in the `CubeSMRotationState`, a nested state machine will update, and will transition between 3 states that change the scale of our cube.

We'll start by adding a `StateMachine` field to the `CubeSMRotationState`, and in the state's `Update()`, we'll update this state machine. Look for the `// HERE` comments:
```cs
[PolymorphicStruct]
public struct CubeSMRotationState : ICubeSMState 
{
    public StateHandle TargetState;
    public float Duration;
    public float RotationSpeed;
    
    public StateMachine SubStateMachine; // HERE: adding the StateMachine field
    
    public float Timer;
    public float3 RandomDirection;
    
    public void OnStateEnter(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        Timer = 0f;
        RandomDirection = Unity.Mathematics.Random.CreateFromIndex((uint)entityData.Entity.Index).NextFloat3Direction();
    }

    public void OnStateExit(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        Timer += globalData.DeltaTime;

        entityData.LocalTransformRef.ValueRW.Rotation = math.mul(
            quaternion.Euler(RandomDirection * RotationSpeed * globalData.DeltaTime), entityData.LocalTransformRef.ValueRW.Rotation);
            
        if (Timer >= Duration)
        {
            StateMachineUtilities.TryStateTransition(ref stateMachine, ref entityData.StatesBuffer, ref globalData, 
                ref entityData, TargetState);
        }
        
        // HERE: updating the StateMachine field. We choose to update it at the end of our state update, but it could update at any point.
        // IMPORTANT: Notice that we pass our "SubStateMachine" field as parameter to the update function, not the parent "stateMachine" that is passed on to us as parameter.
        StateMachineUtilities.Update(ref SubStateMachine, ref entityData.StatesBuffer, ref globalData, ref entityData);
    }
}
```

Now we'll create a third state type; the `CubeSMScaleState`. This will be a simple state that simply sets a scale when it enters, and transitions to another state after a time. Look for the `// HERE` comments:
```cs
[PolymorphicStruct]
public struct CubeSMScaleState : ICubeSMState 
{
    public StateHandle TargetState;
    public float Duration;
    public float Scale; // HERE: the scale to set on the entity 
    
    public float Timer;
    
    public void OnStateEnter(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        // HERE: when the state enters, it sets the entity scale
        Timer = 0f;
        entityData.LocalTransformRef.ValueRW.Scale = Scale;
    }

    public void OnStateExit(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(ref StateMachine stateMachine, ref CubeSMGlobalStateUpdateData globalData, ref CubeSMEntityStateUpdateData entityData)
    {
        Timer += globalData.DeltaTime;
            
        if (Timer >= Duration)
        {
            StateMachineUtilities.TryStateTransition(ref stateMachine, ref entityData.StatesBuffer, ref globalData, 
                ref entityData, TargetState);
        }
    }
}
```

Finally, we'll modify our authoring component so that it creates 3 of these scaling states, and adds them to our `CubeSMRotationState`'s nested state machine:
```cs
// Create a few states and remember their StateHandles.
StateMachineUtilities.CreateState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
        ref statesBuffer,
        default,
        out StateHandle stateAHandle);
StateMachineUtilities.CreateState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
        ref statesBuffer,
        default,
        out StateHandle stateBHandle);
// HERE: Create 3 additional states and get their handles
StateMachineUtilities.CreateState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
    ref statesBuffer,
    default,
    out StateHandle stateC1Handle);
StateMachineUtilities.CreateState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
    ref statesBuffer,
    default,
    out StateHandle stateC2Handle);
StateMachineUtilities.CreateState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
    ref statesBuffer,
    default,
    out StateHandle stateC3Handle);
    
// Set state data, now that we have all of our state handles created.
// Note: it can be useful to set state data after creating all of our state handles, in cases where
// Our states must store state handles to transition to. If not, we could've also set state data directly
// in the "CreateState" function.
StateMachineUtilities.TrySetState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
    ref statesBuffer,
    stateAHandle,
    new CubeSMState
    {
        State = new CubeSMPositionState
        {
            TargetState = stateBHandle,
            Duration = 1f,
            PositionOffset = 2f,
        },
    });
StateMachineUtilities.TrySetState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
    ref statesBuffer,
    stateBHandle,
    new CubeSMState
    {
        State = new CubeSMRotationState
        {
            TargetState = stateAHandle,
            Duration = 2f,
            RotationSpeed = 3f,
            
            // HERE: in our CubeSMRotationState, we'll create a new sub state machine with "stateC1Handle" as its initial state
            SubStateMachine = new StateMachine(stateC1Handle),
            }
        },
    });

// HERE: set the data of our 3 "CubeSMScaleState" states. Make them all transition to each other
StateMachineUtilities.TrySetState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
    ref statesBuffer,
    stateC1Handle,
    new CubeSMState
    {
        State = new CubeSMScaleState()
        {
            TargetState = stateC2Handle, // HERE: transition to C2
            Duration = 0.3f,
            Scale = 0.3f,
        },
    });
StateMachineUtilities.TrySetState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
    ref statesBuffer,
    stateC2Handle,
    new CubeSMState
    {
        State = new CubeSMScaleState()
        {
            TargetState = stateC3Handle, // HERE: transition to C3
            Duration = 0.4f,
            Scale = 0.75f,
        },
    });
StateMachineUtilities.TrySetState<CubeSMState, CubeSMGlobalStateUpdateData, CubeSMEntityStateUpdateData>(
    ref statesBuffer,
    stateC3Handle,
    new CubeSMState
    {
        State = new CubeSMScaleState()
        {
            TargetState = stateC1Handle, // HERE: transition to C1
            Duration = 0.5f,
            Scale = 1f,
        },
    });
```

Now you can press Play. You should see that the cube first moves for 1s, then starts rotating for 2s, but as it is rotating (and ONLY as it is rotating), its scale changes between 3 different states. This is because the sub state machine whose initial state is our first Scale state only updates within the `CubeSMRotationState` state's update. Once our first scale state transitions to the second scale state, this second scale state becomes our sub state machine's "current state", so it keeps updating only within the `CubeSMRotationState` state's update.

These state machines can be quite efficient, despite their "polymorphic" and hierarchical nature. Try spawning 100,000 of these or more to get an idea.

![](./Images/manycubes.gif)