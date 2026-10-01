# Excuse Me, Drone v1.0.0 test checklist

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

- Confirm the exact text: `ステイ中ですがテレポートさせますか？`.
- OK: teleport to the player's current front target; Stay order remains and the drone does not return to its old waiting point.
- Cancel / close / outside click: no teleport and no waiting-position change.
- Repeat F10 while confirmation is open: no duplicate/replaced dialog and no teleport.
- Follow drone: summon immediately without a Stay confirmation.
- Change order, remove drone, or leave the world before OK: no stale teleport.
- Broken Stay drone: OK uses the existing revive/ground/lift/shutdown cycle and keeps the new Stay location.
- Repeat from inventory/menu, with nearby and distant drones, and with a custom cfg summon key.
