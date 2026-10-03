"""Run extracted production placement methods against deterministic physics/world doubles.
Usage: python tests/run-placement-tests.py /path/to/dotnet /path/to/csc.dll
Requires .NET 8; this does not run the Unity physics engine.
"""
from pathlib import Path
import json, os, subprocess, sys, tempfile
root = Path(__file__).resolve().parents[1]
source = (root / 'Scripts/ExcuseMeDroneController.cs').read_text()
names = ['FindManualSummonTarget', 'TryFindTeleportTarget', 'TryPlacement',
         'IsEntityCollider', 'ComputeSummonAnchor', 'ComputeBrokenSummonTarget',
         'UpdateStuckRescue', 'Rescue', 'ComputeDodgeAnchor', 'ResetStuckSample',
         'FindGroundRedrawTarget', 'UpdatePendingBrokenReshutdown']
def extract(name):
    import re
    start = re.search(r'        private static \w+ '+name+r'\(', source).start()
    brace = source.index('{', start)
    depth, end = 1, brace + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]
code = (root / 'tests/placement-harness.cs').read_text().replace('// PRODUCTION_METHODS', '\n'.join(extract(n) for n in names))
dotnet, csc = map(Path, sys.argv[1:3])
frameworks = sorted((dotnet.parent / 'shared/Microsoft.NETCore.App').glob('8.*'))
if not frameworks: raise SystemExit('Requires a .NET 8 runtime beside dotnet')
framework = frameworks[-1]
with tempfile.TemporaryDirectory() as scratch:
    out = Path(scratch); (out/'tests.cs').write_text(code)
    subprocess.run([str(dotnet), str(csc), '/noconfig', '/nostdlib+', '/nologo', '/warn:0', '/target:exe', '/out:'+str(out/'tests.dll')]+['/reference:'+str(p) for p in framework.glob('*.dll')]+[str(out/'tests.cs')], check=True, env=dict(os.environ,DOTNET_ROLL_FORWARD='Major'))
    (out/'tests.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':framework.name}}}))
    subprocess.run([str(dotnet), str(out/'tests.dll')], check=True)
