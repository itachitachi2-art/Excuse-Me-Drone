#!/usr/bin/env python3
"""Fail if protected 1.0.3 behavior/build files or version boundaries drift."""
import hashlib
from pathlib import Path
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
unchanged = {
    'Scripts/ExcuseMeDroneController.cs': '1b165f5e922c12d6f53edce661ef569a08679d9d8a32bf4f7b2be86e7997c1ea',
    'Scripts/ExcuseMeDroneConfig.cs': 'cc2c9045e26f4209294fb0a5f339b503d0ff89b1a0a8060f2db98597f055d6f3',
    'Scripts/ExcuseMeDronePatches.cs': '5c36660330210342c1d79e8366bd84a70b79de3757782883dcf4e07618ceb11e',
    'build.ps1': '59a0b021926f7f9d58328712f57845366e182d6f1c889c032401ceee3ec32c4b',
    'build.cmd': '98f6f8a4c730a6c4cd827872b52f0db27e8e10fc90e143058d0570a7a3e4b94a',
    'tests/placement-harness.cs': 'dba26b879c0a529db30097810735a95fa994c9777086cc354e869fa96aab0fb3',
    'tests/run-placement-tests.py': '8d11838aee24310d3fc8300c7cbdeea0b10ba0146e27dc82cb2019e1c6433c79',
}
for name, expected in unchanged.items():
    assert hashlib.sha256((root / name).read_bytes()).hexdigest() == expected, name
    print('PASS unchanged baseline ' + name)
version = ET.parse(root / 'ModInfo.xml').getroot().find('Version').get('value')
assert version == '1.0.4'
for name in ('Scripts/ExcuseMeDroneMod.cs', 'Config/ExcuseMeDrone.cfg', 'README.md', 'INSTALL.txt',
             'NEXUS-CHANGELOG.txt', 'TEST-JA.txt', 'package.cmd', 'package.ps1', 'TEST_CHECKLIST.md'):
    assert version in (root / name).read_text(encoding='utf-8-sig'), name
print('PASS consistent 1.0.4 candidate version markers')
assert set(p.name for p in (root / 'Scripts').glob('*.cs')) == {
    'ExcuseMeDroneConfig.cs', 'ExcuseMeDroneController.cs', 'ExcuseMeDroneMod.cs',
    'ExcuseMeDronePatches.cs', 'ExcuseMeDroneRevengePatch.cs'}
print('PASS sole additional production source is revenge setter guard')
