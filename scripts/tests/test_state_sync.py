"""Run: python -m unittest discover -s scripts/tests -v"""
import importlib.util
import json
from datetime import datetime
from pathlib import Path
import subprocess
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / 'state_sync.py'
spec = importlib.util.spec_from_file_location('state_sync', SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class StateSyncTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / '.agent').mkdir()
        (self.root / '.agent/STATE.md').write_text(
            '# State\n\n## Done (verified)\n- Kept by humans.\n\n'
            '<!-- AUTO-SNAPSHOT:BEGIN -->\nold\n<!-- AUTO-SNAPSHOT:END -->\n',
            encoding='utf-8',
        )
        subprocess.run(['git', '-C', str(self.root), 'init', '-q'], check=True)
        subprocess.run(['git', '-C', str(self.root), 'config', 'user.email', 'test@example.invalid'], check=True)
        subprocess.run(['git', '-C', str(self.root), 'config', 'user.name', 'Test'], check=True)
        (self.root / '.gitignore').write_text('.agent/.state_sync.lock\n', encoding='utf-8')

    def test_sync_preserves_manual_notes_and_is_idempotent(self):
        ok, _ = module.sync(self.root)
        self.assertTrue(ok)
        state = (self.root / '.agent/STATE.md').read_text(encoding='utf-8')
        self.assertIn('- Kept by humans.', state)
        self.assertIn('Branch / HEAD:', state)
        ok, message = module.sync(self.root)
        self.assertTrue(ok)
        self.assertEqual(message, 'No change')

    def test_app_file_changes_are_seen_but_agent_files_hidden(self):
        (self.root / 'SomeService.cs').write_text('class Service {}', encoding='utf-8')
        module.sync(self.root)
        state = (self.root / '.agent/STATE.md').read_text(encoding='utf-8')
        self.assertIn('SomeService.cs', state)
        self.assertNotIn('?? .agent/', state)

    def test_missing_markers_prevents_overwrite(self):
        original = '# Manually written state\n'
        (self.root / '.agent/STATE.md').write_text(original, encoding='utf-8')
        ok, _ = module.sync(self.root)
        self.assertFalse(ok)
        self.assertEqual((self.root / '.agent/STATE.md').read_text(encoding='utf-8'), original)

    def test_root_resolution_from_subdirectory(self):
        inner = self.root / 'src' / 'Deep'
        inner.mkdir(parents=True)
        self.assertEqual(module.root_for({'cwd': str(inner)}), self.root)

    def test_hook_log_appends_all_three_events_with_time_and_cwd(self):
        cwd = str(self.root / 'arbejdsmappe æ')
        for event in ('SessionStart', 'PreCompact', 'Stop'):
            module.log_hook(self.root, {'hook_event_name': event, 'cwd': cwd,
                                       'prompt': 'Must not be logged'})
        entries = [json.loads(line) for line in
                   (self.root / module.LOG_REL).read_text(encoding='utf-8').splitlines()]
        self.assertEqual([entry['event'] for entry in entries],
                         ['SessionStart', 'PreCompact', 'Stop'])
        for entry in entries:
            self.assertEqual(set(entry), {'event', 'timestamp', 'cwd'})
            self.assertEqual(entry['cwd'], cwd)
            self.assertEqual(datetime.fromisoformat(entry['timestamp']).utcoffset().total_seconds(), 0)

    def test_manual_runs_do_not_create_hook_log(self):
        module.log_hook(self.root, {})
        self.assertFalse((self.root / module.LOG_REL).exists())


if __name__ == '__main__':
    unittest.main()
