# Excuse Me, Drone v1.0.8 — 7 Days to Die v3.4 b10

Default keys: **Home** toggles behind-follow lock; **End** toggles the drone inventory; **F10** recalls it. `BehindLockDistance=3.0` is unchanged. Edit Config/ExcuseMeDrone.cfg and restart to remap single Unity KeyCode names. Keep bindings distinct.

Updating the DLL does not rewrite or migrate your config. Existing `At` / `Colon` (or custom) values remain active. Missing or invalid settings use Home / End in 1.0.8. To adopt the new defaults while keeping other settings, change only `BehindLockKey=Home` and `InventoryKey=End`. Replacing the whole cfg with the bundled file also replaces your other preferences. Legacy At/Colon character fallback remains available only when those values are configured; Home/End use direct key detection without character matching. Compact keyboards may require Fn; keypad 7/1 are separate key codes.

Behind-lock follows the owner's horizontal body direction, not camera direction. It is session-only and supported in single-player/local-host authority. It pauses the drone state driver while active. Other entity updates remain active. Blocked geometry leaves the drone at its last position until a clear target becomes available. Toggle off returns to Follow and uses the existing forward summon. F10 also releases the lock. Dead owners, broken/removed drones invalidate it; pending broken-redraw cannot enter lock mode.

Storage uses the native storage activation/lock path and native close/unlock, only for the owned loaded drone within the normal interaction distance. Text entry and unrelated modal windows suppress the new keys. This does not provide remote storage or remote-client fixed-position authority.

1.0.7 was accepted in-game by the maintainer, including earlier broken-drone recall/F10/combat-return checks. 1.0.8 changes only default bindings plus version/documentation; it is newly compiled against supplied 3.4 b10 Assembly-CSharp, with earlier Unity/Harmony references. Automated checks are recorded in tests/hotkey-validation.json. The maintainer confirmed Home/End and explicitly configured At/Colon both work in-game in 1.0.8. Other 1.0.8 scenarios were not individually reported; dedicated/remote multiplayer remains unverified. See TEST-1.0.8-JA.txt.

## v1.0.7 combat sidestep suppression
Behind-lock ON suppresses this mod's CombatDodge side teleport, including during temporary combat/heal suspension. Native stun approach and retreat, healing, and post-combat behind return remain unchanged. Turning the lock OFF restores normal configured CombatDodge behavior.

## v1.0.6 combat suspension
With the stun baton installed, a native stun Attack temporarily releases behind positioning and the state-driver suppression. The toggle stays ON. After the last native Attack, wait at least two seconds and finish any active/pending Heal before resuming behind positioning. This is based on the drone's actual AI task, not merely nearby enemies or the player's combat flag. Repeated attacks extend the wait. Manual toggle OFF or F10 cancels the pending return. Removing the baton while suspended also honors the short delay and an ongoing heal; without a baton no new combat suspension begins.

The mod does not change healing priority, thresholds, supplies, cooldown or weapon range. Healing and attack tasks were never globally disabled by fixed mode; the old Japanese test note claiming this was incorrect. Fixed positioning can interfere with movement needed by those tasks. Baton-free fixed mode is deliberately unchanged by this update.

## Prior v1.0.4 documentation (historical)

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

## Release preparation (2026-10-17)

The maintainer reported test OK and requested release preparation.
The supplied test-rebuild archive contains all 26 repository files byte-for-byte equal
to production source commit `3b11adaeeca9d3fd4f87d8f3225e8f7afcd060ab`.
The prepared runtime ZIP reuses its DLL without rebuilding it:
SHA-256 `7894af242bb1492a9ab3785de7ef4c3567875cdb622bf34de4aba34200f4ff60`.
Production code, ModInfo and configuration are unchanged.

This is the supplied Roslyn/C# 5 rebuild, compiled against v3.3 game references
and Lib.Harmony 2.3.3 net472. It is not the older Mono binary recorded in
`tests/candidate-validation.json`. The supplied rebuild provenance is preserved in
`tests/test-rebuild-validation.json`; current acceptance and artifact identity are
recorded in `tests/release-preparation-20261017.json`.

The general pass report does not enumerate every checklist item or establish
dedicated/remote multiplayer support. The installed DLL was not read from the
maintainer's computer. No new game session, rebuild or CI run was performed for
this documentation/packaging update. The branch remains an unpublished candidate.

## Historical automated source validation

- All five production sources compiled with C# 5 and the supplied actual v3.3 game/Unity/Harmony references. The supplemental build uses Mono; Windows Framework csc was not run here.
- 21 direct production-prefix cases and 5 real bundled-Harmony patch/unpatch cases passed on controlled entity doubles.
- The existing 25 extracted-production-method placement/rescue/redraw cases passed again with deterministic physics/world doubles, using a Mono host.
- Static checks preserve protected baseline files and keep the 1.0.4 version markers consistent.
- The historical 15 summon-flow cases reported for 1.0.3 are not present in the baseline repository and were not rerun.
- The historical automation did not execute full game Harmony PatchAll, bullet/blade reproduction, native AI, damage/healing, physics/rendering or multiplayer. The later maintainer pass report is recorded separately above. A server-authoritative installation is needed to affect server registration; dedicated/remote and client-only installation are not verified solutions.

See `REVENGE_GUARD_DEVELOPMENT.md` for API evidence and the validation boundaries.
The supplementary `tests/run-candidate-tests.py --help` documents its explicit local
tool/reference paths. It does not install an SDK or replace the Windows build workflow.

Run the placement cases with .NET 8 and Roslyn:

```text
python tests/run-placement-tests.py /path/to/dotnet /path/to/csc.dll
```

See `TEST_CHECKLIST.md` for game checks. This branch is a candidate, not a published Nexus release.

