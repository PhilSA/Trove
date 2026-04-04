
# Trove Audio

Trove Audio provides an ECS+Jobs+Burst FMOD integration built on top of the "*FMOD For Unity*" (com.fmod.fmod-for-unity-2-03) package. All audio emitter and listener logic happens fully in burst jobs.

## Installation

Refer to [Trove Readme](https://github.com/PhilSA/Trove/blob/main/README.md#installing-the-packages) for installation instructions.

## Project Setup

Importing Trove Audio will automatically import the "FMOD For Unity" package. You should see an FMOD setup wizard when opening your Unity project, and it'll take you through the steps for setting up FMOD in your project.

You should take a look at the official ["FMOD for Unity" user guide](https://fmod.com/docs/2.03/unity/user-guide.html) in order to understand how to setup your FMOD Studio project, and understand the FMOD plugin in general.

## Authoring components

Trove Audio provides bakers for the "FMOD For Unity" package's built-in `StudioEventEmitter`, `StudioListener` and `StudioBankLoader` components. Use these components on subscene GameObjects in order to turn them into pure Entities.

At runtime, these become Entity components:
* `FMODEventEmitter`
* `FMODListener`
* `FMODBankLoaderComponent`
* etc...

Note: Play/Stop events for emitters aren't supported in ECS. You should use the `FMODEmitterPlayPropertiesAuthoring` component for this instead.

## Controlling emitters

All emitter control actions (play, stop, setting parameters, etc...) can be done with the various static methods in `FMODUtilities`. These actions can all be done in bursted jobs.

## FMOD Singleton

For more advanced usage, you can gain access to the "FMOD For Unity" package's `StudioSystem` via the `FMODSingleton` singleton component. `StudioSystem` is fully unmanaged and its methods are thread safe, so it is safe tu use them in burst jobs.