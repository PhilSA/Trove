
# Trove Event Systems

This package provides an all-purpose, high-performance, high-versatility event system. It has the following features:
- Allows creating events that are stored globally, or events that are stored per-entity.
- Allows parallel writing of events in burst jobs.
- Allows executing events in parallel burst jobs, single burst jobs, burst main thread, or non-burst main thread. This is fully controlled by the user.
- Allows multiple event writers and multiple event readers.


## Quick Start

Import the package in your project. Make sure unsafe code is allowed in the .asmdef or in "Edit > Project Settings > Player > Other Settings Allow 'unsafe' code"

> Note: You can refer to the "/Tests" folder of the package to see examples of event systems.


### Creating a new event type

Then, right-click in the Project window and select "Create > Trove > EventSystems > ..." in order to create a new event system based on a template. See [Event System Types](#event-system-types) for a description of the various types of event systems.

When creating a new event type from templates this way, it is recommended to name it after the event type itself. For example, you can create a new event from the templates and call it "MyEvent". This will automatically take care of naming all the various components, systems, etc... appropriately. You are free to rename the file to something else after its initial creating.

Once you have your new event from template, read the comments in the file and look at all the `TODO`s in order to understand how it works.


### Using the events system

Event writer systems should:
* Update before the event system of the desired event type.
* Get the events singleton for the desired event type, and get the events manager from the singleton.
* Get either queue or a stream in which to write events. This can be done with `myEventsSingleton.QueueEventsManager.CreateEventQueue()` or `myEventsSingleton.StreamEventsManager.CreateEventStream()`.
* Write events to the queue or stream.

Event reader systems for a ["Global"](#global-vs-entity-events) event type should:
* Update after the event system of the desired event type.
* Get the events singleton for the desired event type, and get the events manager from the singleton.
* Get the events list with `myEventsSingleton.EventsList`.
* Read events in the list.
    * If the event type is polymorphic, this is done by calling this code on the event list, in an unsafe context:
        ```cs
        int readIndex = 0;
        byte* eventsPtr = eventsList.GetUnsafeReadOnlyPtr();
        while (MyEventNameManager.ExecuteNextEvent(eventsPtr, eventsList.Length, ref readIndex))
        { }
        ```

Event reader systems for an ["Entity"](#global-vs-entity-events) event type should:
* Update after the event system of the desired event type.
* Iterate entities that have both the enabled "Has[EventName]" component and the "[EventName]Element" `DynamicBuffer` for the desired event type.
* Read events from the buffer of events on the entity.
    * If the event type is polymorphic, this is done by calling this code on the event buffer, in an unsafe context:
        ```cs
        int readIndex = 0;
        byte* eventsPtr = (byte*)eventsBuffer.GetUnsafeReadOnlyPtr();
        while (MyEventNameManager.ExecuteNextEvent(eventsPtr, eventsBuffer.Length, ref readIndex))
        { }
        ```

Note: event systems always clear events from global lists or entity buffers right before adding new events to them, which means events from the previous frame will still be valid until the event system updates. It also means even writers are actually allowed to write events after their targeted event system, but in that case their events will only become available to readers on the next frame.


## Why events in DOTS?

Events in DOTS are useful when you need some action to be performed, but you are not yet in the most optimal/appropriate context in which to perform that action. They can be a key tool for unlocking performance opportunities, for implementing modular decoupled systems, or for simplifying defered actions.

For example:
* You are in a parallel bursted job, and you need to perform an action that could only be executed on the main thread in unbursted managed code.
* You are in monobehaviour code, and you need to remember to perform an action that should only be executed at a specific point later in the frame in a bursted job, after some other system has updated.
* You are iterating a certain entity query, and you need to schedule something to be executed on another entity, but only later when you are iterating that other entity archetype. Once we iterate that other entity's archetype, we can do archetype-dependent handling of the action, and we can gain very fast access to multiple component data on the entity.
* etc....

Moreover, they can also be a convenient way to communicate between systems and between entities.


## Event System Types

You can create event systems of different kinds using the "Create > Trove > EventSystems" menu:
* Global Events
* Entity Events
* Global Polymorphic Events
* Entity Polymorphic Events

"Global" events are events that end up in a globally-accessible `NativeList` in a singleton component.

"Entity" events are events that end up in `DynamicBuffer`s on target entities. This can be useful for:
* Archetype-dependent event handling.
* Fast access to components on target entities upon event execution (accessed through chunk iteration instead of component lookup).

"Polymorphic" events are events that can be serialized and deserialized to buffers. This allows storing events of different types in the same buffer. They can be interesting when:
* You need ordering across different event types.
* You have many event types and you don't want to pay the performance overhead of:
    * each event type having schedule jobs that poll for potential events. If events of all types don't happen most of the time, a single events job will usually have much less overhead.
    * (if using Entity events) each event type requiring a separate `DynamicBuffer` and enableable component on the target entity.


## How do event systems work internally?

* Each event type has an events singleton that is automatically created by event template code (created in the template event system's `OnCreate` by event subsystems).
* Event singletons keep lists of event queues and streams.
* Event writers request the creation of new event queues and streams to write events to, which are then tracked by the event singletons.
* Event system update:
    * Clear global event lists or event buffers (depending of if they're Global or Entity event systems).
    * For each event queue or stream that was produced for event writers, transfer the events from queues/streams to global event lists or event buffers (depending of if they're Global or Entity event systems).
    * Dispose all event queues/lists and clear the internal lists of tracked event queues/streams.