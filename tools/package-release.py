"""Package an already built, clean source checkpoint. Run through prepare-release.ps1."""
import hashlib, json, re, shutil, subprocess, tempfile, zipfile
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath

ROOT = Path(__file__).resolve().parents[1]
def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, text=True).strip()
def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()
if git('status', '--porcelain', '--untracked-files=normal'):
    raise SystemExit('Source checkpoint is not clean')
commit = git('rev-parse', 'HEAD')
source = (ROOT/'src/Prospice.Placement/Plugin.cs').read_text()
version = re.search(r'BepInPlugin\(Id, "Prospice Gamepad Placement", "([0-9.]+)"', source).group(1)
if f'Placement {version} - playtest' not in (ROOT/'release/INSTALL.md').read_text():
    raise SystemExit('Installation document version disagrees with plugin')
label = 'playtest'
name = f'Prospice-Gamepad-Placement-{version}-{label}-win-x64-{commit[:8]}'
out = ROOT/'artifacts/releases'/name
out.mkdir(parents=True, exist_ok=False)
payload = {
    'BepInEx/plugins/Prospice.Placement.dll': ROOT/'src/Prospice.Placement/bin/Release/net6.0/Prospice.Placement.dll',
    'INSTALL.md': ROOT/'release/INSTALL.md',
    'THIRD_PARTY_NOTICES.md': ROOT/'release/THIRD_PARTY_NOTICES.md',
}
for relative, original in payload.items():
    target = out/'stage'/relative
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(original, target)
stage = out/'stage'
manifest = {
    'version': version, 'label': label, 'platform': 'windows-x64-il2cpp',
    'source_commit': commit, 'source_repository': 'https://github.com/b2kdaman/prospicemod',
    'created_utc': datetime.now(timezone.utc).isoformat(),
    'compatibility': {'unity': '2022.3.62f2', 'game_assembly_sha256': '511B68EBDE548CF7BFE4178B5F8F76CF9397B4C8821EE411079DD6225C7D339D',
        'bepinex': '6.0.0-be.788+5b766a3', 'il2cpp_interop': '1.5.3.0', 'harmonyx': '2.10.2.1', 'runtime': '6.0.7', 'sdk': '8.0.425',
        'requires_existing_matching_metadata_and_interop': True},
    'acceptance': {'standalone_checks': 55, 'build': 'passed', 'current_native_runtime': 'not_tested',
        'controller_and_visual': 'pending', 'publication': 'not_published', 'game_launched_for_packaging': False},
    'files': [{'path': p, 'bytes': (stage/p).stat().st_size, 'sha256': digest(stage/p)} for p in sorted(payload)],
}
(stage/'manifest.json').write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
checks = {p: digest(stage/p) for p in [*payload, 'manifest.json']}
(stage/'SHA256SUMS.txt').write_text(''.join(f'{checks[p]}  {p}\n' for p in sorted(checks)), encoding='utf-8')
expected = set(payload) | {'manifest.json', 'SHA256SUMS.txt'}
archive = out/(name+'.zip')
with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for p in sorted(expected): z.write(stage/p, p)
# Extract only after validating every member; reject duplicate paths, traversal and extras.
with tempfile.TemporaryDirectory(prefix='verify-', dir=out) as temp:
    verify = Path(temp)
    with zipfile.ZipFile(archive) as z:
        names = z.namelist()
        if len(names) != len(expected) or set(names) != expected:
            raise SystemExit('Unexpected, missing or duplicate ZIP entries')
        for entry in z.infolist():
            path = PurePosixPath(entry.filename)
            if path.is_absolute() or '..' in path.parts or ':' in entry.filename or '\\' in entry.filename:
                raise SystemExit('Unsafe ZIP path')
            target = verify.joinpath(*path.parts)
            target.parent.mkdir(parents=True, exist_ok=True)
            with z.open(entry) as source_stream, target.open('wb') as target_stream:
                shutil.copyfileobj(source_stream, target_stream)
        if z.testzip() is not None: raise SystemExit('ZIP CRC failure')
    for p in expected:
        if digest(verify/p) != digest(stage/p): raise SystemExit(f'Round-trip mismatch: {p}')
    restored = json.loads((verify/'manifest.json').read_text())
    if restored != manifest: raise SystemExit('Release manifest mismatch')
    for record in restored['files']:
        file = verify/record['path']
        if digest(file) != record['sha256'] or file.stat().st_size != record['bytes']:
            raise SystemExit('Payload manifest mismatch')
    for line in (verify/'SHA256SUMS.txt').read_text().splitlines():
        sha, p = line.split('  ', 1)
        if digest(verify/p) != sha: raise SystemExit('Checksum list mismatch')
result = {'archive': str(archive), 'sha256': digest(archive), 'bytes': archive.stat().st_size,
          'payload_files': len(payload), 'archive_files': len(expected), 'source_commit': commit,
          'version': version, 'verified': True, 'manual_acceptance': 'pending'}
(out/'verification.json').write_text(json.dumps(result, indent=2)+'\n')
(out/(archive.name+'.sha256')).write_text(f"{result['sha256']}  {archive.name}\n")
print(json.dumps(result, indent=2))
