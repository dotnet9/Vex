"""Compile production migration; run the same orchestration with an inert in-memory backend."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import uuid


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--compiler', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    root = Path(__file__).resolve().parent.parent
    report = output / 'assertions.txt'
    report.unlink(missing_ok=True)
    def literal(path):
        return str(path).replace("'", "''")
    source = f'''#define CodeWFSetupFixture
#define VexMigrationTest
#define TestRoot "{literal(output / ('data-' + uuid.uuid4().hex))}"
#define TestReport "{literal(report)}"
[Setup]
AppId=Vex.Migration.Verification.Only
AppName=Vex Migration Verification
AppVersion=1.0.0
DefaultDirName={{tmp}}\\Vex.Migration.Verification
PrivilegesRequired=lowest
Uninstallable=no
CreateUninstallRegKey=no
OutputDir={output}
OutputBaseFilename=Vex.Migration.Fixture
[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "{root.parent / 'Languages/ChineseSimplified.isl'}"
#include "{root / 'CodeWF.Setup.Maintenance.iss'}"
#include "{root / 'Vex.Migration.iss'}"
#include "{root / 'tests/Fixture.Migration.Assertions.iss'}"
#expr SaveToFile("{output / 'preprocessed.iss'}")
'''
    script = output / 'fixture.iss'
    script.write_text(source, encoding='utf-8-sig')
    compiled = subprocess.run([args.compiler, '/Q', str(script)], capture_output=True, text=True, encoding='utf-8', errors='replace')
    if compiled.returncode:
        raise RuntimeError(compiled.stdout + compiled.stderr)
    processed = (output / 'preprocessed.iss').read_text(encoding='utf-8-sig')
    # Test code cannot enumerate the real registry, start processes or create installed-app entries.
    for pattern in (r'\bReg\w+\s*\(', r'\bShellExec\s*\(', r'\bExec\s*\(', r'\bexternal\s+\'',
                    r'(?im)^\[(Files|Registry|Icons|Run|UninstallRun|UninstallDelete)\]'):
        if re.search(pattern, processed, re.I):
            raise RuntimeError('Unsafe fixture: ' + pattern)
    executable = output / 'Vex.Migration.Fixture.exe'
    subprocess.run([str(executable), '/VERYSILENT', '/SUPPRESSMSGBOXES', '/LANG=english'], timeout=45,
                   creationflags=subprocess.CREATE_NO_WINDOW)
    lines = report.read_text(encoding='utf-8-sig').splitlines()
    if not lines or not re.fullmatch(r'PASS \d+', lines[0]) or len(lines[1:]) != int(lines[0][5:]):
        raise RuntimeError('\n'.join(lines))
    if not all(line.startswith('PASS: ') for line in lines[1:]):
        raise RuntimeError('\n'.join(lines))
    (output / 'receipt.json').write_text(json.dumps({'assertions': len(lines)-1, 'productionInstallerExecuted': False,
        'registryAndProcessCallsRemoved': True, 'report': str(report)}, indent=2), encoding='utf-8')
    print('\n'.join(lines))


if __name__ == '__main__':
    main()
