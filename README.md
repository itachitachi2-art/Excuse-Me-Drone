# Excuse Me, Drone v0.1.25 — 7 Days to Die v3.2 prototype

A small quality-of-life mod for the robotic drone.

## Current behavior

- **F10 is summon-only.** It does not open any drone menu or camera interaction.
- A distant healthy Follow-mode drone is summoned and returned immediately to Vanilla AI.
- A Stay-mode drone is not summoned.
- When a zombie first enters 5 m, the drone is moved aside once. Vanilla follow/attack/stun-gun behavior resumes immediately.
- Combat dodge has a 60-second re-trigger cooldown.
- Follow-mode stuck rescue remains available.
- Normal mod-triggered teleports use `World.GetHeightAt(x, z) + GroundClearance`.

## Broken drone F10 experiment

Broken/shutdown drones are not moved automatically by combat dodge or stuck rescue.

When F10 is used on a distant broken Follow-mode drone:

1. The broken drone is moved to a player-relative safe position.
2. After the nearby entity resumes updating, Health is temporarily set to **2**.
3. Vanilla `setShutdown(false)` and `playWakeupAnim()` are called.
4. After one complete Vanilla update, the now-live drone is teleported once to the detected ground height.
5. Vanilla drone AI is allowed to lift the drone from the ground under its own movement.
6. A lift of `BrokenLiftThreshold` (default **0.25 m**) is required, and the drone remains alive for at least `BrokenMinimumAwakeSeconds` (default **1.50 s**) after the ground teleport.
7. The broken state is restored with Health **1** + `performShutdown()`.
8. If the drone never re-lifts, the safety timeout is `BrokenReviveTimeoutSeconds` (default **6 s**) and shutdown is restored anyway.

The purpose of v0.1.25 is to deliberately reproduce the redraw path observed in testing: **revive -> ground teleport -> Vanilla re-lift -> shutdown**.

Build with `build.cmd`.
