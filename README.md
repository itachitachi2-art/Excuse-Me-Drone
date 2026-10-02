# Excuse Me, Drone v1.0.3 candidate — 7 Days to Die v3.2

A small quality-of-life mod for the robotic drone.

## Recall and automatic movement

- The configurable summon key (default F10) recalls Follow-mode drones at any distance, including with inventory open.
- Stay-mode recall asks for confirmation, then switches to Follow.
- v1.0.1 unloaded-drone recall is retained for single-player and the local host.
- Combat dodge moves the drone aside once when a zombie enters 5 m; cooldown is 60 seconds.
- Stuck rescue handles distant stationary drones and stationary drones at least 3 m above/below the player.

## Current-floor placement fix

Mod teleports no longer use `World.GetHeightAt(x, z)`. Ground placement casts down only from 0.75 m above the player's feet to 0.75 m below them, checks for a movement-blocking world block under the hit, and checks nearby clearance. Candidate positions start at the requested offset, then circle the player at 1.5, 1.0 and 0.6 m. Unity physics coordinates account for `Origin.position`.

The clearance check uses a conservative 0.9 × 0.8 × 0.9 m box above the placement pivot. Own-drone colliders are ignored. Owner overlap is allowed only at the final center candidate. A full overlap buffer is treated as blocked.

Automatic movement is skipped if no clear candidate exists. Healthy manual recall with ground snap enabled always runs the ground-placement redraw step. If the local search fails, it uses the player's foot position plus `GroundClearance`. Broken initial recall and ground-snap-disabled recall use player-relative airspace. Center fallbacks may overlap the owner or geometry in extremely confined spaces and require in-game checks.

`GroundSnapEnabled=false` preserves player-relative height while still checking clearance. Broken-drone initial recall remains airborne. Its revive/ground/lift/shutdown redraw cycle now uses the local placement search; if no clear ground candidate exists, it teleports to the player-foot fallback before marking the step applied. The wake/ground/lift/shutdown sequence is preserved.

## Build

Run `build.cmd` or `package.cmd` on Windows, optionally supplying the game installation directory. The scripts use Windows' C# compiler and the game's runtime references. Harmony is resolved from the game or local `References`; game/Harmony assemblies are not included in this repository.

## Validation

- Changed Controller/Config compiled against the available actual 7DTD/Unity assemblies.
- 25 extracted-production-method placement/rescue/redraw cases passed with deterministic physics/world doubles.
- 15 extracted-method summon-flow regression cases passed, including unloaded Stay recall and cancellation.
- Full Harmony build, Unity physics behavior and in-game rendering remain to be validated on the user's installation.

Run the placement cases with .NET 8 and Roslyn:

```text
python tests/run-placement-tests.py /path/to/dotnet /path/to/csc.dll
```

See `TEST_CHECKLIST.md` for game checks. This branch is a candidate, not a published Nexus release.
