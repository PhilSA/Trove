
# Trove Event Systems

This package provides an all-purpose, high-performance, high-versatility event system. It has the following features:
- Allows creating events that are stored globally in a singleton list, or events that are stored per-entity in dynamic buffers.
- Allows parallel writing of events in burst jobs.
- Allows executing events in parallel burst jobs, single burst jobs, burst main thread, or non-burst main thread. This is fully controlled by the user.
- Allows multiple event writers and multiple event readers.
- Allows polymorphic ordered events when combined with Trove Polymorphic Structs.


## Quick Start

### Creating a new event type

Right-click in the Project window and select "Create > Trove > EventSystems > ..." in order to create a new event system based on a template. See [Event System Types](#event-system-types) for a description of the various types of event systems.

When creating a new events file from templates this way, it is recommended to name it after the event type itself. For example, you can create a new event file from the templates and call it "MyEvent", or "DamageEvent". This will automatically take care of naming all the various components, systems, etc... appropriately. You are free to rename the file to something else after its initial creating.

Once you have your new event from template, read the comments in the file and look at all the `TODO`s in order to understand how it works.


### Using the events system

> Note: each of the event templates include example events, event writers, and event readers. You can delete or change them after creating the template, but they should give you enough of an idea of how to use this system.

Events are simply a struct type that gets written to queues or streams by your event writer systems, then gets transfered to a list or dynamic buffer by an events system, and then gets read from the list or dynamic buffer by your event reader systems.

Event writer systems should:
* Update before the event system of the desired event type (They can update after, but then the event will be processed the next frame).
* Get the events singleton for the desired event type, and get the events manager from the singleton.
* Get either queue or a stream in which to write events. This can be done with `myEventsSingleton.QueueEventsManager.CreateWriter()` or `myEventsSingleton.StreamEventsManager.CreateWriter(x)`.
    * Using a queue writer is the simplest way to go and should be considered the default. However, using a stream writer can be more efficient in parallel jobs.
* Write events to the queue or stream writers using the writer's `Write()` method.
(see `Example[EventName]WriterSystem` in the event templates)

Event reader systems for a ["Global"](#global-vs-entity-events) event type should:
* Update after the event system of the desired event type.
* Get the events singleton for the desired event type, and get the events manager from the singleton.
* Get the events list with `myEventsSingleton.ReadEventsList`.
* Read events in the list.
    * If the event type is "PolyByteArray", this is done with a special events iterator for the `byte` event list:
        ```cs
        // Get the iterator that can read through the polymorphic structs of the list
        PolymorphicObjectNativeListIterator<PolyMyEvent> iterator = 
            PolymorphicObjectUtilities.GetIterator<PolyMyEvent>(ReadEventsList);
        while (iterator.GetNext(out PolyMyEvent e, out _, out _))
        {
            // Execute the event (execution logic is implemented in the event struct itself)
            e.Execute();
        }
        ```
(see `Example[EventName]ReaderSystem` in the event templates)

Event reader systems for an ["Entity"](#global-vs-entity-events) event type should:
* Update after the event system of the desired event type.
* Iterate entities that have both the enabled `Has[EventName]` component and the `DynamicBuffer<[EventName]>` for the desired event type.
* Read events from the buffer of events on the entity.
    * If the event type is "PolyByteArray", this is done by reinterpreting the buffer to `byte`s and iterating it with this special events iterator:
        ```cs
        DynamicBuffer<byte> eventsBytesBuffer = eventsBuffer.Reinterpret<byte>();

        // Get the iterator that can read through the polymorphic structs of the list
        PolymorphicObjectDynamicBufferIterator<PolyMyEvent> iterator = 
            PolymorphicObjectUtilities.GetIterator<PolyMyEvent>(eventsBytesBuffer);
        while (iterator.GetNext(out PolyMyEvent e, out _, out _))
        {
            // Execute the event (execution logic is implemented in the event struct itself)
            e.Execute();
        }
        ```
(see `Example[EventName]ReaderSystem` in the event templates)

Note: event systems always clear events from global lists or entity buffers right before adding new events to them, which means events from the previous frame will still be valid up until the event system updates. It also means event writers are actually allowed to write events after their targeted event system, but in that case their events will only become available to readers on the next frame.


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
* Global PolyByteArray Events (only available when Trove Polymorphic Structs package is present)
* Entity PolyByteArray Events (only available when Trove Polymorphic Structs package is present)

"Global" events are events that end up in a globally-accessible `NativeList` in a singleton component.

"Entity" events are events that end up in `DynamicBuffer`s on target entities. This can be useful for:
* Archetype-dependent event handling.
* Fast access to components on target entities upon event execution (accessed through chunk iteration instead of component lookup).

"PolyByteArray" events are events that can be serialized and deserialized to byte arrays. This allows storing events of different types in the same buffer. They can be interesting when:
* You need ordering across different event types.
* You have many event types and you don't want to pay the performance overhead of:
    * each event type having schedule jobs that poll for potential events. If events of all types don't happen most of the time, a single events job will usually have much less overhead.
    * (if using Entity events) each event type requiring a separate `DynamicBuffer` and enableable component on the target entity.
See additional details in the [Polymorphic events](#polymorphic-events) section.


## How do event systems work internally?

* Each event type has an events singleton that is automatically created by event template code (created in the template event system's `OnCreate` by event subsystems).
* Event singletons keep lists of event queues and streams.
* Event writers request the creation of new event queues and streams to write events to, which are then tracked by the event singletons.
* Event system update:
    * Clear global event lists or event buffers (depending of if they're Global or Entity event systems).
    * For each event queue or stream that was produced for event writers, transfer the events from queues/streams to global event lists or event buffers (depending of if they're Global or Entity event systems).
    * Dispose all event queues/lists and clear the internal lists of tracked event queues/streams.


## Polymorphic events

There are two main ways to handle polymorphic events in this system.

The first and most recommended way is to create an event polymorphic struct using the Trove Polymorphic Structs package, and then simply use the generated polymorphic struct as your event struct in a regular event system. This means you would use the regular "Global Event" or "Entity Event" templates for this, and not the "Global PolyByteArray Event" or "Entity PolyByteArray Event". (Or, you could write your own "polymorphic" struct by hand).

The second way is theoretically more efficient, but comes with more pitfalls and limitations, so it should mainly be considered for events that are extremely performance-critical AND that can vary in size greatly. In this approach, you'd create an event type from either the "Global PolyByteArray Event" or "Entity PolyByteArray Event" template. These templates come with a pre-made polymorphic struct that you can customize. The advantage of this approach is that it will serialize/deserialize events to byte arrays, meaning there will be no potential waste of space if you have event types that vary greatly in size. This can improve performance. This approach only supports writing events to streams (not queues), and events must be read from byte arrays using a special iterator type, so it is less easy to use. The serialized event bytes data is also not suitable for sending over netcode or saving to disk, because of the possibility of different platforms interpreting the types with different sizes. So these events should only ever exist in a temporary non-netcoded runtime context.