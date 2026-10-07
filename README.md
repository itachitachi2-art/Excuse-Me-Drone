# Excuse Me, Drone v1.0.4 candidate — 7 Days to Die v3.3

A small quality-of-life mod for the robotic drone.

## 1.0.4 revenge-registration guard

This candidate adds one Harmony prefix to `EntityAlive.SetRevengeTarget(EntityAlive)`.
It skips non-null registrations only when the receiving entity is an `EntityDrone`
(including subclasses). True null clears and every non-drone call continue normally.
It applies to all attackers and setter callers, including players, zombies and scripts;
it is not restricted to bullets or blade traps. A destroyed Unity wrapper that compares
equal to null but is still a non-null managed reference is also blocked.

No damage, HP, hit events, sensors, attack tasks, healing or movement code is changed
by this guard. Normal AI may still independently select and attack enemies. Existing
revenge/attack targets are not forcibly cleared, and this does not promise an immediate
stop to combat already in progress. Other mods that bypass this setter are outside
the guard. There is no new setting, per-frame cleanup, or local-player ownership gate.

The source baseline is PR #2, commit `441fcd4b6d7639c27f32b1fdc2fd2c1c8468211f`
(1.0.3 candidate), rather than the older 0.1.25 main branch. The Controller, Config
implementation, previous patches and Windows build scripts remain byte-for-byte
unchanged. The effective cfg settings are unchanged; only its header identifies 1.0.4.

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

- All five production sources compiled with C# 5 and the supplied actual v3.3 game/Unity/Harmony references. The supplemental build uses Mono; Windows Framework csc was not run here.
- 21 direct production-prefix cases and 5 real bundled-Harmony patch/unpatch cases passed on controlled entity doubles.
- The existing 25 extracted-production-method placement/rescue/redraw cases passed again with deterministic physics/world doubles, using a Mono host.
- Static checks preserve protected baseline files and keep the 1.0.4 version markers consistent.
- The historical 15 summon-flow cases reported for 1.0.3 are not present in the baseline repository and were not rerun.
- Full game Harmony PatchAll, bullet/blade reproduction, native AI, damage/healing, physics/rendering and multiplayer remain unverified. A server-authoritative installation is needed to affect server registration; a client-only install is not a verified solution.

See `REVENGE_GUARD_DEVELOPMENT.md` for API evidence and the validation boundaries.
The supplementary `tests/run-candidate-tests.py --help` documents its explicit local
tool/reference paths. It does not install an SDK or replace the Windows build workflow.

Run the placement cases with .NET 8 and Roslyn:

```text
python tests/run-placement-tests.py /path/to/dotnet /path/to/csc.dll
```

See `TEST_CHECKLIST.md` for game checks. This branch is a candidate, not a published Nexus release.
