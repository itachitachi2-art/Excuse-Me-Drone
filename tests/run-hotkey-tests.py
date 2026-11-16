#!/usr/bin/env python3
"""Compile unmodified production Hotkeys/Config with controlled doubles.
Usage: python3 tests/run-hotkey-tests.py /path/to/mcs /path/to/mono
Does not execute full Controller, game UI, native physics, multiplayer or Harmony.
"""
from pathlib import Path
import subprocess, sys
root = Path(__file__).resolve().parents[1]
# Static contract only: the full Controller is not executed by the harness.
controller = (root/'Scripts/ExcuseMeDroneController.cs').read_text()
import re
body = re.search(r'internal static bool CanLock\(EntityDrone drone\)\s*\{(.*?)\n        \}', controller, re.S).group(1)
assert '!IsShutdownOrBroken(drone)' in body
assert '!state.PendingBrokenRevive' in body
assert '!state.PendingBrokenReshutdown' in body
assert 'ExcuseMeDroneHotkeys.ReleaseWithoutSummon(drone);' in controller
assert re.search(r'if\s*\(cfg\.CombatDodgeEnabled\s*&&\s*!ExcuseMeDroneHotkeys\.IsLockRequested\(drone\)\)\s*UpdateCombatDodge\(drone, owner, state, cfg\);', controller)
print('PASS static combat-dodge guard uses requested intent, not effective positioning state')
print('PASS static CanLock broken/pending-revive/pending-reshutdown guard and summon-release contract (not runtime coverage)')
out = root / 'tests/hotkey-output' 
out.mkdir(exist_ok=True)
exe = out / 'hotkey-tests.exe'
cmd = [sys.argv[1], '-langversion:5', '-out:' + str(exe),
       str(root/'Scripts/ExcuseMeDroneHotkeys.cs'), str(root/'Scripts/ExcuseMeDroneConfig.cs'),
       str(root/'tests/hotkey-harness.cs')]
p = subprocess.run(cmd, text=True, capture_output=True)
(out/'hotkey-compile.log').write_text(p.stdout+p.stderr)
print(p.stdout+p.stderr, end='')
p.check_returncode()
p = subprocess.run([sys.argv[2], str(exe)], text=True, capture_output=True)
(out/'hotkey-tests.log').write_text(p.stdout+p.stderr)
print(p.stdout+p.stderr, end='')
p.check_returncode()
