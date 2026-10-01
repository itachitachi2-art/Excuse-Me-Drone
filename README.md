# Excuse Me, Drone v1.0.0 — 7 Days to Die v3.2

A small quality-of-life mod for the robotic drone.

## Current behavior

- **F10 is summon-only.** It does not open any drone menu or camera interaction.
- A healthy Follow-mode drone at any distance is summoned and returned immediately to Vanilla AI.
- A Stay-mode drone shows an OK/Cancel confirmation before manual summon. OK teleports it and updates its Stay position without switching to Follow; Cancel leaves it in place.
- When a zombie first enters 5 m, the drone is moved aside once. Vanilla follow/attack/stun-gun behavior resumes immediately.
- Combat dodge has a 60-second re-trigger cooldown.
- Follow-mode stuck rescue remains available.
- Normal mod-triggered teleports use `World.GetHeightAt(x, z) + GroundClearance`.

## Broken drone summon

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

Broken drone summon uses the redraw path observed in testing: **revive -> ground teleport -> Vanilla re-lift -> shutdown**.

Build with `build.cmd`.

## Summon key configuration

Edit `Config/ExcuseMeDrone.cfg`, then restart the game:

```ini
SummonKey=F10
```

Use a Unity `KeyCode` name such as `F8`, `Home`, or `G` (case-insensitive).
Missing or invalid keys fall back to F10. `None` and undefined numeric values are rejected.

Manual summon has no minimum distance, including for broken drones. Legacy
`SummonDistance` / `MenuRescueDistance` settings are ignored. Stay mode requires confirmation. Manual summon also works while inventory or other
modal UI windows are open. An already open message box is not replaced.

## Stay confirmation

Press the configured summon key (default F10) while the drone is in Stay mode:

> ステイ中ですがテレポートさせますか？

The standard game OK/Cancel message box is used. OK summons the drone to the
player's current position and facing target and updates its waiting position while
retaining Stay. Cancel (including dismissal) performs no teleport. Repeated summon
keys do not replace an open message box. If the drone disappears, changes owner or
leaves Stay while the box is open, OK does not move it.
