# 1.0.4 candidate: revenge registration only

## Baseline and scope

- Source baseline: [PR #2](https://github.com/itachitachi2-art/Excuse-Me-Drone/pull/2), `441fcd4b6d7639c27f32b1fdc2fd2c1c8468211f`, 1.0.3 candidate. PR #2 and main are not merged or changed by this work.
- Related investigation: [Issue #3](https://github.com/itachitachi2-art/Excuse-Me-Drone/issues/3). Its initial bullet/blade-specific plan was followed by a request for a minimal revenge-only guard without changing ordinary AI.
- The implemented guard covers **all non-null calls through the setter received by drones**, irrespective of attacker, damage cause, ownership or caller. This includes zombie- and script-originated registration. It does not disable normal AI target discovery/attack.
- Null clearing, non-drone revenge, the existing Controller/F10/Stay/floor-height behavior and config values are preserved. There is no damage/event/HP hook, SetAttackTarget hook, sensor override, CanExecute override or per-frame target reset.
- A pre-existing revenge or attack target stays until native code clears/expires it. A non-null destroyed Unity wrapper is blocked; only CLR null is passed as a clear. Direct field mutations by other mods can bypass a setter guard.

## Actual v3.3 API evidence

The user's supplied `Assembly-CSharp.dll` was inspected read-only:

- SHA-256: `fceec27300ffd3a1f97b097e43b60f3b07597f441e7ecbc6e1b59eebb234c705`
- MVID: `1a9a4203-3d95-4c90-b094-8926dec1ee9c`
- AssemblyVersion: `0.0.0.0`; it is not used to identify the game release.
- Exactly one declared `EntityAlive.SetRevengeTarget(EntityAlive _other)` exists, token `0x06002305`, flags `0x86`: public, nonvirtual, instance, returning void.
- Its body assigns `revengeEntity = _other`, then sets `revengeTimer` to 0 or 500 using Unity null equality. It does not process damage or fire hit events.
- `EntityDrone` inherits via `EntityNPC` from `EntityAlive`. The prefix's `is EntityDrone` also covers subclasses.
- `DroneSensors.GetNearestEnemyInRange` checks revenge before normal enemy selection. Suppressing this registration does not suppress independent normal selection.
- The shipped `0Harmony.dll` is version `2.13.0.0`, SHA-256 `c349e1a3fd13fa5a9facc9805a5e160161b14489f46f6bdd38202b8e124f78df`; its `(Type, string, Type[])` patch attribute is available.

## Validation performed

1. All 20 baseline blobs were checked against their GitHub blob identities before editing.
2. Every production source, including mod entry and both old/new Harmony patch files, compiled against the actual supplied v3.3 references. The supplemental Linux Mono build uses the Windows script's exact required/available-optional reference list, C# 5, `/nostdlib+` and command-line `/noconfig`.
3. 21 controlled cases call the exact production prefix, including drone/subclass/non-drone, true/fake null, attacker classes, repetition, old target/timer preservation, and unrelated-state preservation.
4. 5 additional cases use the actual bundled Harmony to patch and unpatch a controlled setter with the production patch class. Attribute discovery, `_other`/`__instance` injection and original-body skipping/clearing are exercised. These are **not game-entity executions**.
5. The existing 25 placement/rescue/redraw cases pass unchanged with a supplemental Mono host. Physics/world behavior is simulated.
6. Protected baseline file hashes and version markers are checked automatically. The original Windows build scripts and original placement runner/harness remain unchanged.
7. Independent review found no implementation blocker in the minimal setter guard.

The historical 15 summon-flow tests are absent from the baseline repository and were not rerun. No Unity/game launch, real-game PatchAll, Windows csc run, multiplayer session or in-game bullet/blade reproduction was performed. The automated checks establish a buildable candidate, not a verified in-game fix.

## Home test and rollback

Close the game, back up the installed mod and custom cfg, and replace the DLL plus ModInfo with this candidate. Do not hot-swap or keep duplicate mod folders. Restart for a clean test.

Use the same saved test conditions for the 1.0.3 baseline and 1.0.4 candidate; restart between them. Check bullets and running blades with different owners/alliance states, then ordinary zombie defense, Follow/Stay, healing and F10. Record actual HP and effects separately from retaliation; normal blade self-wear is not removed. Existing-target persistence and ordinary defense are not a failed promise of total pacifism.

Start with single-player/local host. Dedicated/remote sessions require separate installation/authority checks and remain unverified. If behavior differs unexpectedly, close the game and restore the backed-up DLL/ModInfo/config. Do not broaden the guard to damage immunity or disabled AI to mask a failed reproduction.
