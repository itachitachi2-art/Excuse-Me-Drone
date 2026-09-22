# Excuse Me Drone

A 7 Days to Die mod for improving drone recovery and repositioning.

## Current feature set

- Press **F10** to recall a distant drone.
- Reposition drones away from nearby zombies.
- Apply combat reposition cooldowns to avoid excessive movement.
- Rescue broken or stuck drones.

## Build

Development is intended to work without requiring a separate .NET SDK installation.

The established local workflow uses the Windows .NET Framework compiler (`csc.exe`) and resolves required 7 Days to Die / Harmony assemblies from the local game installation.

See Issue #1 for the build workflow and ChatGPT handoff notes.

## Status

The previously validated release/source line was **v0.1.25**.

The exact v0.1.25 source archive is being recovered before source files are committed here; code will not be reconstructed from guesses.

## Distribution note

Game-provided assemblies such as `Assembly-CSharp.dll`, Unity assemblies, and `0Harmony.dll` are development references only and should not be bundled in release archives.
