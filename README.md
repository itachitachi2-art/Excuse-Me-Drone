# Excuse Me, Drone v0.1.26 — 7 Days to Die v3.2 prototype

A small quality-of-life mod for the robotic drone.

## Current behavior

- **F10 is summon-only.** It does not open any drone menu or camera interaction.
- A healthy Follow-mode drone at any distance is summoned and returned immediately to Vanilla AI.
- A Stay-mode drone is not summoned.
- When a zombie first enters 5 m, the drone is moved aside once. Vanilla follow/attack/stun-gun behavior resumes immediately.
- Combat dodge has a 60-second re-trigger cooldown.
- Follow-mode stuck rescue remains available.
- Normal mod-triggered teleports use `World.GetHeightAt(x, z) + GroundClearance`.

## Broken drone F10 experiment

Broken/shutdown drones are not moved automatically by combat dodge or stuck rescue.

When F10 is used on a broken Follow-mode drone at any distance:

1. The broken drone is moved to a player-relative safe position.
2. After the nearby entity resumes updating, Health is temporarily set to **2**.
3. Vanilla `setShutdown(false)` and `playWakeupAnim()` are called.
4. After one complete Vanilla update, the now-live drone is teleported once to the detected ground height.
5. Vanilla drone AI is allowed to lift the drone from the ground under its own movement.
6. A lift of `BrokenLiftThreshold` (default **0.25 m**) is required, and the drone remains alive for at least `BrokenMinimumAwakeSeconds` (default **1.50 s**) after the ground teleport.
7. The broken state is restored with Health **1** + `performShutdown()`.
8. If the drone never re-lifts, the safety timeout is `BrokenReviveTimeoutSeconds` (default **6 s**) and shutdown is restored anyway.

The purpose of v0.1.26 is to deliberately reproduce the redraw path observed in testing: **revive -> ground teleport -> Vanilla re-lift -> shutdown**.

Build with `build.cmd`.

## Summon key configuration

Edit `Config/ExcuseMeDrone.cfg`, then restart the game:

```ini
SummonKey=F10
```

Use a Unity `KeyCode` name such as `F8`, `Home`, or `G` (case-insensitive).
Missing or invalid keys fall back to F10. `None` and undefined numeric values are rejected.

Manual summon has no minimum distance, including for broken drones. Legacy
`SummonDistance` / `MenuRescueDistance` settings are ignored. Stay/Sentry orders
and modal UI guards still apply.
