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
- Stay/Sentry drone: key must not teleport it at any distance.
- Inventory or another modal UI open: summon key must still teleport the Follow drone.
- Set cfg `SummonKey=F8`, restart: F8 summons, F10 does not.
- Confirm lowercase `f8` also works.
- Invalid name, `None`, or undefined numeric key: fallback to F10.
- Legacy cfg `MenuKey` still works when `SummonKey` is absent.
- Old cfg with `SummonDistance=5.0`: nearby manual summon still works.
