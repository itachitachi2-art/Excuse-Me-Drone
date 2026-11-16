#!/usr/bin/env python3
"""Compile unmodified production Hotkeys/Config with controlled doubles.
Usage: python3 tests/run-hotkey-tests.py /path/to/mcs /path/to/mono
Does not execute full Controller, game UI, native physics, multiplayer or Harmony.
"""
from pathlib import Path
import subprocess, sys
root = Path(__file__).resolve().parents[1]
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
