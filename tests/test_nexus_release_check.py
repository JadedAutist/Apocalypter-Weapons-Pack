import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest
import zipfile

spec = importlib.util.spec_from_file_location('nexus_check', Path(__file__).resolve().parents[1] / 'scripts/nexus-release-check.py')
check = importlib.util.module_from_spec(spec)
spec.loader.exec_module(check)


class NexusPreflightTests(unittest.TestCase):
    def package(self, folder, extra=None):
        path = Path(folder) / 'mod.zip'
        with zipfile.ZipFile(path, 'w') as archive:
            for name in ('ChainsawPrototype.dll', 'ChainsawIdle.json', 'Flamethrower/Vlammewerper.obj'):
                archive.writestr(check.PREFIX + name, b'fixture')
            if extra:
                archive.writestr(extra, b'fixture')
        return path, hashlib.sha256(path.read_bytes()).hexdigest()

    def test_approved_runtime_package(self):
        with tempfile.TemporaryDirectory() as folder:
            path, digest = self.package(folder)
            self.assertEqual(check.check_zip(path, digest), digest)
            with self.assertRaises(ValueError):
                check.check_zip(path, '0' * 64)

    def test_rejects_private_files_source_and_traversal(self):
        for extra in ('src/Plugin.cs', check.PREFIX + 'WoodenSpear/item.obj', '../outside', check.PREFIX + 'backup.zip', check.PREFIX + 'OtherMod.dll'):
            with self.subTest(extra=extra), tempfile.TemporaryDirectory() as folder:
                path, digest = self.package(folder, extra)
                with self.assertRaises(ValueError):
                    check.check_zip(path, digest)

    def test_read_only_discovery_and_publish_guards(self):
        calls = []
        def get(url, headers):
            calls.append(url)
            self.assertEqual(headers['apikey'], 'test-secret')
            if url.endswith('/users/validate.json'):
                return {'name': 'JadedAutist', 'email': 'not-reported', 'key': 'not-reported'}
            if url.endswith('/games/apocalypter/mods/68'):
                return {'data': {'id': 'mod-guid', 'name': 'Apocalypter Weapons Pack'}}
            if url.endswith('/mods/mod-guid/files'):
                return {'data': {'mod_files': [{'id': 'file-guid', 'name': 'Main', 'is_active': True}, {'id': '8164890', 'name': 'Numeric modern ID', 'is_active': True}]}}
            if url.endswith('/games/apocalypter/mod-file-versions/123'):
                return {'data': {'file': {'id': 'file-guid'}}}
            if url.endswith('/mod-files/file-guid/versions') or url.endswith('/mod-files/8164890/versions'):
                return {'data': {'versions': [{'version': '1.6.1'}]}}
            self.fail('Unexpected endpoint: ' + url)
        report = check.nexus_target('test-secret', '', False, '1.6.1', get)
        self.assertEqual(report['files'][0]['id'], 'file-guid')
        self.assertNotIn('email', report)
        self.assertNotIn('key', report)
        self.assertFalse(report['published'])
        self.assertEqual(check.nexus_target('test-secret', '123', False, '1.6.1', get)['file_id'], 'file-guid')
        self.assertEqual(check.nexus_target('test-secret', '8164890', False, '1.6.1', get)['file_id'], '8164890')
        for file_id, version in (('', '1.6.2'), ('wrong-mod-file', '1.6.2'), ('file-guid', '1.6.1')):
            with self.subTest(file_id=file_id), self.assertRaises(ValueError):
                check.nexus_target('test-secret', file_id, True, version, get)
        self.assertEqual(check.nexus_target('test-secret', 'file-guid', True, '1.6.2', get)['file_id'], 'file-guid')
        self.assertTrue(all('/uploads' not in url and '/changelogs' not in url for url in calls))

    def test_invalid_tag(self):
        for tag in ('main', '1.6.1\nmalicious', '../1.6.1'):
            with self.subTest(tag=tag), self.assertRaises(ValueError):
                check.release_version(tag)
        self.assertEqual(check.release_version('v1.6.2'), '1.6.2')

    def test_same_version_replacement_requires_exact_current_main(self):
        versions = [{'id': 'current-id', 'version': '1.6.2', 'category': 'main'},
                    {'id': 'old-id', 'version': '1.6.1', 'category': 'archived'}]
        def get(url, headers):
            if url.endswith('/users/validate.json'):
                return {'name': 'JadedAutist'}
            if url.endswith('/games/apocalypter/mods/68'):
                return {'data': {'id': 'mod-id', 'name': 'Apocalypter Weapons Pack'}}
            if url.endswith('/mods/mod-id/files'):
                return {'data': {'mod_files': [{'id': 'file-id', 'name': 'Main', 'is_active': True}]}}
            if url.endswith('/mod-files/file-id/versions'):
                return {'data': {'versions': versions}}
            self.fail('Unexpected endpoint')
        def target(replacement='', version='1.6.2'):
            return check.nexus_target('test-secret', 'file-id', True, version, get, replacement)
        for bad in ('', 'wrong-id', 'old-id'):
            with self.subTest(replacement=bad), self.assertRaises(ValueError):
                target(bad)
        self.assertEqual(target('current-id')['replacement_of'], 'current-id')
        with self.assertRaises(ValueError):
            target('current-id', '1.6.3')
        versions[0]['category'] = 'archived'
        with self.assertRaises(ValueError):
            target('current-id')
        # A successful replacement makes retrying the old request fail.
        versions.insert(0, {'id': 'new-id', 'version': '1.6.2', 'category': 'main'})
        with self.assertRaises(ValueError):
            target('current-id')


if __name__ == '__main__':
    unittest.main()
