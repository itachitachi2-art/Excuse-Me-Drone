# Excuse Me, Drone v0.1.25 test checklist

## Broken drone F10

1. Break the drone with `kill <entityId>`.
2. Move more than the F10 minimum summon distance away.
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
