"""Read-only release/access checks. Uploading is a separate, explicit action."""
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid
import zipfile

REPOSITORY = 'JadedAutist/Apocalypter-Weapons-Pack'
PREFIX = 'BepInEx/plugins/Apocalypter Weapons Pack/'


def read_json(url, headers):
    request = urllib.request.Request(url, headers=headers, method='GET')
    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        # Never log authenticated request headers or API response bodies.
        raise RuntimeError(f'HTTP {error.code} reading {urllib.parse.urlparse(url).path}') from None


def release_version(tag):
    if not re.fullmatch(r'v?\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?', tag):
        raise ValueError('Use an exact release tag, such as 1.6.1 or v1.6.2.')
    return tag.removeprefix('v')


def check_zip(path, expected):
    if not re.fullmatch(r'[A-Fa-f0-9]{64}', expected):
        raise ValueError('Expected SHA-256 must contain exactly 64 hexadecimal characters.')
    with path.open('rb') as stream:
        actual = hashlib.file_digest(stream, 'sha256').hexdigest()
    if actual.lower() != expected.lower():
        raise ValueError('ZIP checksum does not match the approved package.')
    with zipfile.ZipFile(path) as archive:
        seen = set()
        for entry in archive.infolist():
            name = entry.filename
            if '\\' in name or ':' in name or name.startswith('/') or '..' in PurePosixPath(name).parts:
                raise ValueError('Unsafe path in ZIP.')
            if name.casefold() in seen:
                raise ValueError('Duplicate path in ZIP.')
            seen.add(name.casefold())
            if re.search(r'spear|backup|(^|/)(Recovery|staging|obj|\.git)(/|$)', name, re.I):
                raise ValueError('Development or backup files found in ZIP.')
            if entry.is_dir():
                continue
            if name in ('README.md', 'CHANGELOG.md'):
                continue
            if not name.startswith(PREFIX):
                raise ValueError('Only the installable Weapons Pack ZIP may be uploaded.')
            suffix = Path(name).suffix.lower()
            if suffix not in {'.dll', '.png', '.wav', '.json', '.cfg', '.obj'}:
                raise ValueError('Unexpected runtime file type in ZIP.')
            if suffix == '.dll' and name != PREFIX + 'ChainsawPrototype.dll':
                raise ValueError('Unexpected DLL in ZIP.')
        for required in ('ChainsawPrototype.dll', 'ChainsawIdle.json', 'Flamethrower/Vlammewerper.obj'):
            if (PREFIX + required).casefold() not in seen:
                raise ValueError('Required runtime files are missing from ZIP.')
    return actual


def nexus_target(api_key, requested_id, publish, version, get=read_json, replace_version_id=''):
    if not api_key:
        raise ValueError('Add the GitHub Actions secret NEXUSMODS_API_KEY first.')
    headers = {'apikey': api_key, 'Accept': 'application/json', 'User-Agent': 'Apocalypter-Weapons-Pack'}
    identity = get('https://api.nexusmods.com/v1/users/validate.json', headers)
    if str(identity.get('name', '')).casefold() != 'jadedautist':
        raise ValueError('Nexus key must belong to the mod owner, JadedAutist.')
    base = 'https://api.nexusmods.com/v3'
    mod = get(base + '/games/apocalypter/mods/68', headers)['data']
    if str(mod.get('name', '')).casefold() != 'apocalypter weapons pack':
        raise ValueError('Unexpected Nexus mod page; refusing to continue.')
    mod_id = str(mod['id'])
    files = get(base + '/mods/' + urllib.parse.quote(mod_id, safe='') + '/files', headers)['data']['mod_files']
    target = requested_id.strip()
    if target and not re.fullmatch(r'[A-Za-z0-9-]{1,80}', target):
        raise ValueError('Invalid Nexus File ID.')
    allowed_ids = {str(item['id']) for item in files}
    # Modern persistent File IDs can also be numeric. Resolve an old
    # game-scoped download ID only if it is not already one of this page's IDs.
    if target.isdecimal() and target not in allowed_ids:
        legacy = get(base + '/games/apocalypter/mod-file-versions/' + target, headers)['data']
        target = str(legacy['file']['id'])
    if target and target not in allowed_ids:
        raise ValueError('File ID does not belong to this mod page.')
    if publish and not target:
        raise ValueError('A File ID from the access-check report is required before publishing.')
    versions = []
    if target:
        versions = get(base + '/mod-files/' + urllib.parse.quote(target, safe='') + '/versions', headers)['data']['versions']
        if publish:
            matching = [item for item in versions if item['version'] == version]
            if replace_version_id:
                # An explicit author-approved same-version correction must name
                # the exact current main version. After success it is archived,
                # so repeating the same request cannot publish another copy.
                active = [item for item in versions if item.get('category') == 'main']
                if not matching or len(active) != 1 or str(active[0].get('id')) != replace_version_id or active[0]['version'] != version or str(versions[0].get('id')) != replace_version_id:
                    raise ValueError('Replacement must identify the exact latest main version with the same version number.')
            elif matching:
                raise ValueError('This version already exists on Nexus. Do not upload a duplicate.')
    report = {
        'account': 'JadedAutist', 'mod_page': 'https://www.nexusmods.com/apocalypter/mods/68',
        'mod_id': mod_id, 'file_id': target, 'published': False,
        'files': [{key: item.get(key) for key in ('id', 'name', 'is_active')} for item in files],
        'existing_versions': [item['version'] for item in versions],
        'version_details': [{key: item.get(key) for key in ('id', 'version', 'game_scoped_id', 'name', 'category', 'is_primary', 'uploaded_at')} for item in versions],
        'replacement_of': replace_version_id or None,
    }
    return report


def output(name, value):
    if os.environ.get('GITHUB_OUTPUT'):
        delimiter = uuid.uuid4().hex
        with open(os.environ['GITHUB_OUTPUT'], 'a', encoding='utf-8') as stream:
            stream.write(f'{name}<<{delimiter}\n{value}\n{delimiter}\n')


def main():
    if os.environ.get('GITHUB_REPOSITORY', REPOSITORY) != REPOSITORY:
        raise ValueError('This workflow is configured only for the Weapons Pack repository.')
    tag = os.environ.get('RELEASE_TAG', '').strip()
    version = release_version(tag)
    expected = os.environ.get('EXPECTED_SHA256', '').strip()
    if not re.fullmatch(r'[A-Fa-f0-9]{64}', expected):
        raise ValueError('Provide the SHA-256 of the approved installable ZIP.')
    headers = {'Accept': 'application/vnd.github+json', 'User-Agent': 'WeaponsPack-NexusCheck'}
    if os.environ.get('GH_TOKEN'):
        headers['Authorization'] = 'Bearer ' + os.environ['GH_TOKEN']
    release = read_json('https://api.github.com/repos/' + REPOSITORY + '/releases/tags/' + urllib.parse.quote(tag, safe=''), headers)
    if release.get('draft') or release.get('tag_name') != tag:
        raise ValueError('Select an exact, published GitHub release.')
    filename = f'Apocalypter-Weapons-Pack-{version}.zip'
    matches = [item for item in release.get('assets', []) if item['name'] == filename and item.get('state') == 'uploaded']
    if len(matches) != 1:
        raise ValueError('The release must contain exactly one matching installable ZIP.')
    asset = matches[0]
    if asset.get('digest') and asset['digest'].lower() != 'sha256:' + expected.lower():
        raise ValueError('GitHub asset digest differs from the approved checksum.')
    destination = Path(os.environ.get('RUNNER_TEMP', '.')) / filename
    url = asset['browser_download_url']
    if urllib.parse.urlparse(url).netloc != 'github.com':
        raise ValueError('Unexpected release download host.')
    # Public release download: do not forward either service credential.
    with urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent': 'WeaponsPack-NexusCheck'}), timeout=90) as source, destination.open('wb') as target:
        total = 0
        while chunk := source.read(1024 * 1024):
            total += len(chunk)
            if total > 512 * 1024 * 1024:
                raise ValueError('Unexpectedly large package.')
            target.write(chunk)
    actual = check_zip(destination, expected)
    publish = os.environ.get('PUBLISH_TO_NEXUS', '').lower() == 'true'
    replacement = os.environ.get('REPLACE_VERSION_ID', '').strip()
    if replacement and not re.fullmatch(r'[A-Za-z0-9-]{1,80}', replacement):
        raise ValueError('Invalid replacement version ID.')
    if publish and replacement and os.environ.get('ARCHIVE_PREVIOUS', '').lower() != 'true':
        raise ValueError('Same-version replacement requires archiving the previous download.')
    report = nexus_target(os.environ.get('NEXUSMODS_API_KEY', ''), os.environ.get('NEXUS_FILE_ID', ''), publish, version, replace_version_id=replacement)
    report.update(version=version, sha256=actual, mode='publish preflight' if publish else 'read-only verification')
    print('NEXUS_CHECK_JSON=' + json.dumps(report))
    if os.environ.get('GITHUB_STEP_SUMMARY'):
        with open(os.environ['GITHUB_STEP_SUMMARY'], 'a', encoding='utf-8') as stream:
            stream.write('## Nexus access check\n\n```json\n' + json.dumps(report, indent=2) + '\n```\n')
    for name, value in {'filename': str(destination.resolve()), 'version': version, 'file_id': report['file_id'], 'mod_id': report['mod_id'], 'notes': release.get('body') or release['html_url']}.items():
        output(name, value)


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print('CHECK FAILED: ' + str(error), file=sys.stderr)
        sys.exit(1)
