"""Source contracts for the 1.0.6 test candidate."""
from pathlib import Path
import xml.etree.ElementTree as ET
r=Path(__file__).resolve().parents[1]
assert ET.parse(r/'ModInfo.xml').getroot().find('Version').get('value')=='1.0.6'
c=(r/'Scripts/ExcuseMeDroneController.cs').read_text()
assert '!state.PendingBrokenRevive && !state.PendingBrokenReshutdown' in c
assert 'ExcuseMeDroneHotkeys.ReleaseWithoutSummon(drone);' in c
h=(r/'Scripts/ExcuseMeDroneHotkeys.cs').read_text()
assert 'OnEntityActivated(new EntityActivationCommand("storage"' in h
assert 'openStorageWindow(' not in h
assert 'ui.windowManager.Close(group, false)' in h
assert 'ConnectionManager.Instance.IsServer' in h
assert 'Input.anyKeyDown' in h
print('PASS 1.0.6 source contracts (not runtime proof)')
