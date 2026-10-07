# Excuse Me, Drone v1.0.4 test checklist

## 1.0.4 revenge guard (all in-game checks pending)

- Start with a fresh game process; compare the unchanged 1.0.3 baseline and candidate under the same conditions. Close the game between DLL swaps. Do not hot-patch.
- Confirm the log shows v1.0.4 and successful Harmony PatchAll without an exception.
- Test bullet and blade single/repeated hits, owner/other player, allies/non-allies, and blade owners that are absent/unresolved. Record observed attacks separately from actual HP changes.
- When a registration is observable, candidate drones must reject non-null SetRevengeTarget calls from players, zombies and scripts. True null clears must continue. Use a debugger or separate temporary test instrumentation if needed; this candidate adds no recurring logs.
- Do not infer success solely from an absence of attack in a condition where baseline also does not retaliate. Record that as not reproduced.
- Existing targets must not be forcibly cleared by this patch. Normal AI can still choose enemies independently.
- Verify drone HP loss, armor/friend checks, hit effects, shutdown and repair. Blade self-wear must remain; it is not drone retaliation.
- Verify ordinary zombie defense/stun-weapon use, healing, follow/stay and inventory access. Non-drone revenge must remain native.
- Repeat every relevant F10, current-floor and broken-redraw check below.
- Single-player/local-host first. Dedicated/remote authority and installation need separate validation; client-only success is not assumed.
- If the observed issue follows another path, keep the repro/log evidence and investigate that path. Do not mark this candidate as a confirmed fix or broaden it to immunity/disabled AI.

## Broken drone F10

1. Break the drone with `kill <entityId>`.
2. Test both within 3 m (including directly beside the player) and farther than 5 m.
3. Press F10.
4. Confirm the drone appears at the player-relative safe position.
5. Confirm the temporary wake/revive occurs.
6. Confirm the live drone is then placed on the ground once.
7. Confirm Vanilla AI lifts it from the ground.
8. Confirm it returns to the broken/shutdown state only after the re-lift/grace period.
9. Check whether the drone model remains rendered after the final shutdown.

Useful logs:
- `Broken drone temporary revive applied.`
- `Broken drone ground redraw cycle started.`
- `Broken drone re-lifted; shutdown restored.`
- `Broken-drone re-lift timed out; shutdown restored.`

## Manual summon and configuration

- Healthy Follow drone: summon within 3 m, at 5 m, and beyond 5 m.
- Stay drone: key must show confirmation without teleporting until OK.
- Inventory or another modal UI open: summon key must still teleport the Follow drone.
- Set cfg `SummonKey=F8`, restart: F8 summons, F10 does not.
- Confirm lowercase `f8` also works.
- Invalid name, `None`, or undefined numeric key: fallback to F10.
- Legacy cfg `MenuKey` still works when `SummonKey` is absent.
- Old cfg with `SummonDistance=5.0`: nearby manual summon still works.

## Stay confirmation

- Confirm the exact text: `The drone is in Stay mode. Switch to Follow and teleport it to you?`.
- OK: switch to Follow before teleport; confirm it follows and does not return to its old Stay position or remain buried.
- Cancel / close / outside click: no teleport and no waiting-position change.
- Repeat F10 while confirmation is open: no duplicate/replaced dialog and no teleport.
- Follow drone: summon immediately without a Stay confirmation.
- Change order, remove drone, or leave the world before OK: no stale teleport.
- Broken Stay drone: OK switches to Follow and uses the existing revive/ground/lift/shutdown cycle.
- Repeat from inventory/menu, with nearby and distant drones, and with a custom cfg summon key.


## v1.0.3 current-floor placement (in-game checks pending)

- Recall healthy drones from the ground floor, middle floor, roof and underground; confirm the target stays on the player's current level.
- Face a wall and press recall: confirm another nearby candidate is used.
- Test low ceilings, doors, narrow corridors, slopes, stairs and partial blocks.
- Over open air, manual recall must still work. With ground snap enabled its fallback uses the player-foot baseline; with ground snap disabled it uses player-relative airborne height.
- Test `GroundSnapEnabled=false` and custom summon offsets.
- Test away from world origin, after a floating-origin reposition.
- Trigger combat dodge indoors and confirm it stays on the current floor.
- Leave a Follow drone stationary at least 3 m above/below the player but within 8 m; confirm rescue after `StuckSeconds`.
- When all candidates are blocked, automatic movement must leave the drone in place and avoid repeated failure logs.
- Broken drone: repeat recall and redraw checks indoors on several floors; confirm no global-height relocation and that shutdown is restored.
- Unloaded Follow and Stay drones: confirm v1.0.1 recall still works; cancel must preserve saved position.
- Test single-player and local host. Remote-client unloaded recall remains server-only.
- Observe the final manual center fallback in confined spaces; check clipping and subsequent Vanilla separation.

## Ground redraw regression

- Healthy recall must show the ground-placement then Vanilla-lift redraw path.
- Broken recall must preserve wake -> ground teleport -> Vanilla lift -> restored shutdown.
- Test blocked/failed floor search: ground teleport must still execute before its applied flag is set.
- Keep GroundSnapEnabled=true when validating this ground redraw path.
