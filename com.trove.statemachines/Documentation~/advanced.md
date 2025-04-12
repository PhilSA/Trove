
# How it Works

In order to explain how all of this works, we will go through a hands-on example of creating a state machine that moves, rotates, and scales a cube.


## The state machine template

Your starting point for creating state machines is to create one from the State Machine template. You can do so with `Right Click in Project window > Create > Trove > StateMachines > New State Machine`.

Here are the main pieces of the template, assuming the we name the new file "**CubeSM**":

> Note: Trove State Machines relies on functionality provided by the [Trove Polymorphic Structs](https://github.com/PhilSA/Trove/blob/main/com.trove.polymorphicstructs/README.md) package, for generating polymorphic structs.

* `CubeSMState`: This is the main struct that represents a state of your state machine. It can be stored in a `DynamicBuffer`, and encompasses the code-generated `PolyCubeSMState` polymorphic struct, which allows it to take on the form of any of the various state types of this state machine.
* `ICubeSMState`: This is the polymorphic interface that represents the common methods that the states of your state machine should have. 
* `CubeSMStateA` and `CubeSMStateB`: These are example implementations of specific states of the state machine. They both implement the `ICubeSMState` interface, but they can contain different data.
* `CubeSMGlobalStateUpdateData`: Represents the "global" data (data not belonging to the entity of the state machine) that state updates should have access to. This is passed as parameter to the `ICubeSMState` interface methods.
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


#### The states buffer acts like a pool where states keep an unchanging index

You'll notice there are 8 buffer elements in the `DynamicBuffer<CubeSMState>`, even though the `CubeSMAuthoring` baking code currently only creates 2 states. This is because the states dynamic buffer acts as a "pool" of states where once we add a state at a certain index, it is guaranteed to always remain at that index, no matter how many other states we remove. This characteristic is what enables us to always keep a reliable "reference" to a specific state, using the `StateHandle` struct outputted when creating states. Elements in the `DynamicBuffer<CubeSMState>` buffer can therefore represent a "free available slot" in which to store a state. Not all elements of the buffer are necessarily valid states. You can check if a state element actually exists with the `Trove.Pool.Exists()` method.

> Note: because the `DynamicBuffer<CubeSMState>` is managed by the Trove Pool utility, it is very important that you never manually add, remove, clear, get, or set elements in it directly. Always go through the various `StateMachineUtilities` instead:
* `StateMachineUtilities.CreateState`
* `StateMachineUtilities.TryDestroyState`
* `StateMachineUtilities.TryGetState`
* `StateMachineUtilities.TrySetState`

#### 