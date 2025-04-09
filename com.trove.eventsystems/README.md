
# Trove Event Systems

This package provides an all-purpose, high-performance, high-versatility event system. It has the following features:
- Allows creating events that are stored globally in a singleton list, or events that are stored per-entity in dynamic buffers.
- Allows parallel writing of events in burst jobs.
- Allows executing events in parallel burst jobs, single burst jobs, burst main thread, or non-burst main thread. This is fully controlled by the user.
- Allows multiple event writers and multiple event readers.
- Allows polymorphic ordered events when combined with Trove Polymorphic Structs.

See [Documentation](./Documentation~/quickstart.md)