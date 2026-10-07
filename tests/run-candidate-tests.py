#!/usr/bin/env python3
"""Supplementary cloud/Mono validation; Windows build.cmd remains unchanged.

Compile every production source against user-provided v3.3 references, run the
exact new prefix with real Harmony on controlled doubles, then run the unchanged
25-case placement harness with extracted production methods. No game is launched.
"""
import argparse
import ast
import hashlib
import json
import os
from pathlib import Path
import re
import shlex
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    p = argparse.ArgumentParser(description=__doc__)
    for key in ('mono', 'mcs', 'managed', 'harmony', 'output'):
        p.add_argument('--' + key, type=Path, required=True)
    a = p.parse_args()
    root = Path(__file__).resolve().parents[1]
    out = a.output.resolve()
    out.mkdir(parents=True, exist_ok=True)
    manifest = {'baseline_commit': '441fcd4b6d7639c27f32b1fdc2fd2c1c8468211f',
                'mod_version': ET.parse(root / 'ModInfo.xml').getroot().find('Version').get('value'),
                'game_executed': False, 'windows_csc_executed': False,
                'real_game_harmony_patchall_executed': False, 'checks': []}

    def run(name, cmd, env=None):
        result = subprocess.run(list(map(str, cmd)), cwd=root, text=True, capture_output=True, env=env)
        (out / (name + '.log')).write_text('Command: ' + shlex.join(list(map(str, cmd))) + '\n' + result.stdout + result.stderr)
        manifest['checks'].append({'name': name, 'exit_code': result.returncode,
                                   'status': 'PASS' if result.returncode == 0 else 'FAIL'})
        print(result.stdout + result.stderr, end='')
        (out / 'validation.json').write_text(json.dumps(manifest, indent=2) + '\n')
        if result.returncode:
            raise SystemExit(result.returncode)

    run('candidate-contract', [sys.executable, root / 'tests/check-candidate-contract.py'])
    ps1 = (root / 'build.ps1').read_text(encoding='utf-8-sig')
    required = re.findall(r"'([^']+\.dll)'", re.search(r'\$required = @\((.*?)\n\)', ps1, re.S)[1])
    optional = re.findall(r"'([^']+\.dll)'", re.search(r'foreach \(\$optional in @\((.*?)\n\)\)', ps1, re.S)[1])
    refs = [a.managed.resolve() / name for name in required]
    refs += [a.managed.resolve() / name for name in optional if (a.managed / name).is_file()]
    refs.append(a.harmony.resolve())
    assert all(x.is_file() for x in refs)
    sources = sorted((root / 'Scripts').glob('*.cs'))
    manifest['source_sha256'] = {str(x.relative_to(root)): digest(x) for x in sources}
    manifest['references_sha256'] = {x.name: digest(x) for x in refs}
    manifest['build_scripts_sha256'] = {x: digest(root / x) for x in ('build.cmd', 'build.ps1')}
    manifest['production_language_version'] = '5'
    manifest['reference_list_matches_windows_build'] = True
    manifest['nostdlib_and_command_line_noconfig'] = True
    dll = out / 'ExcuseMeDrone.dll'
    rsp = out / 'actual-v33.rsp'
    rsp.write_text('\n'.join(['/nologo', '/nostdlib+', '/target:library', '/optimize+', '/debug-', '/langversion:5',
                              '/out:"' + str(dll) + '"'] + ['/reference:"' + str(x) + '"' for x in refs] +
                             ['"' + str(x) + '"' for x in sources]) + '\n')
    if dll.exists():
        dll.unlink()
    run('actual-v33-build', [a.mcs, '/noconfig', '@' + str(rsp)])
    assert dll.is_file()
    manifest['dll_sha256'] = digest(dll)
    revenge = out / 'revenge-tests.exe'
    run('revenge-compile', [a.mcs, '-noconfig', '-langversion:5', '-r:' + str(a.harmony.resolve()),
                            '-out:' + str(revenge), root / 'Scripts/ExcuseMeDroneRevengePatch.cs',
                            root / 'tests/revenge-harness.cs'])
    env = dict(os.environ, MONO_PATH=str(a.harmony.resolve().parent))
    run('revenge-controlled-harmony', [a.mono, revenge, '--harmony'], env)
    manifest['direct_prefix_cases'] = 21
    manifest['real_harmony_controlled_cases'] = 5

    # Reuse the unchanged extractor and method list from the existing runner;
    # replace only its unavailable .NET8 host with the supplied Mono host.
    original = ast.parse((root / 'tests/run-placement-tests.py').read_text())
    selected = [node for node in original.body if
                (isinstance(node, ast.Assign) and any(isinstance(t, ast.Name) and t.id == 'names' for t in node.targets)) or
                (isinstance(node, ast.FunctionDef) and node.name == 'extract')]
    scope = {'source': (root / 'Scripts/ExcuseMeDroneController.cs').read_text()}
    exec(compile(ast.Module(body=selected, type_ignores=[]), '<existing-placement-extractor>', 'exec'), scope)
    code = (root / 'tests/placement-harness.cs').read_text().replace('// PRODUCTION_METHODS',
               '\n'.join(scope['extract'](name) for name in scope['names']))
    with tempfile.TemporaryDirectory() as temp:
        source = Path(temp) / 'placement.cs'
        source.write_text(code)
        exe = Path(temp) / 'placement-tests.exe'
        run('placement-compile', [a.mcs, '-noconfig', '-langversion:latest', '-warn:0', '-out:' + str(exe), source])
        run('placement-regression', [a.mono, exe])
    manifest['placement_regression_cases'] = 25
    manifest['status'] = 'PASS_BUILD_AND_51_CONTROLLED_CASES'
    manifest['limitations'] = [
        'Real Harmony patching uses controlled entity doubles, not Assembly-CSharp/Unity execution.',
        'The 25 placement cases use deterministic physics/world doubles; the original .NET8 runner is unchanged.',
        'Historical 15 summon-flow tests were not present in the baseline repository and were not rerun.',
        'Bullets, blades, damage, rendering, normal defense/healing, F10 and multiplayer need in-game validation.',
        'Windows Framework csc scripts remain unchanged but were not executed in this Linux environment.'
    ]
    (out / 'validation.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print(manifest['status'])


if __name__ == '__main__':
    main()
