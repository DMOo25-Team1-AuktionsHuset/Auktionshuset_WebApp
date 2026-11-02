#!/usr/bin/env python3
"""No-LLM repo snapshot for Codex/Claude hooks; preserves human STATE.md sections."""
from __future__ import annotations

from datetime import datetime, timezone
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time

START = "<!-- AUTO-SNAPSHOT:BEGIN -->"
END = "<!-- AUTO-SNAPSHOT:END -->"
STATE_REL = Path('.agent/STATE.md')
LOCK_REL = Path('.agent/.state_sync.lock')
LOG_REL = Path('.agent/state_sync.log')
MAX_PATHS = 10
MAX_CONTEXT_CHARS = 3600


def hook_payload() -> dict:
    # Hook input is one JSON object on stdin. Interactive runs require no input.
    if sys.stdin.isatty():
        return {}
    try:
        raw = sys.stdin.buffer.read(65536)
        payload = json.loads(raw) if raw.strip() else {}
        return payload if isinstance(payload, dict) else {}
    except (ValueError, OSError):
        return {}


def root_for(payload: dict) -> Path:
    script_root = Path(__file__).resolve().parent.parent
    incoming = payload.get('cwd')
    if isinstance(incoming, str) and incoming:
        p = Path(incoming).resolve()
        for candidate in (p, *p.parents):
            state = candidate / STATE_REL
            if state.is_file():
                try:
                    content = state.read_text(encoding='utf-8')
                    if START in content and END in content:
                        return candidate
                except OSError:
                    pass
    return script_root


def git(root: Path, *args: str) -> subprocess.CompletedProcess[bytes]:
    return subprocess.run(
        ['git', '-C', str(root), *args], stdout=subprocess.PIPE,
        stderr=subprocess.DEVNULL, timeout=5, check=False,
    )


def visible(path: str) -> bool:
    path = path.replace('\\', '/')
    ignored = (
        '.agent/', '.claude/', '.codex/', 'scripts/state_sync.py',
        '.gitignore', '.ignore', 'AGENTS.md', 'CLAUDE.md',
    )
    return not any(path == part.rstrip('/') or path.startswith(part)
                   for part in ignored)


def safe_inline(value: str) -> str:
    # Never put untrusted filesystem names into a Markdown code fence.
    return value.replace('`', "'").replace('\r', ' ').replace('\n', ' ')[:110]


def snapshot(root: Path) -> str:
    try:
        branch_result = git(root, 'branch', '--show-current')
        if branch_result.returncode:
            return '## Repository signal (automatic; NOT proof of completion)\n- Git is not initialized here.\n'
        branch = branch_result.stdout.decode('utf-8', 'replace').strip() or '(detached HEAD)'
        head_result = git(root, 'rev-parse', '--short', 'HEAD')
        head = (head_result.stdout.decode('utf-8', 'replace').strip()
                if head_result.returncode == 0 else '(no commits)')
        status_result = git(root, 'status', '--porcelain=v1', '-z', '--untracked-files=normal')
        if status_result.returncode:
            raise RuntimeError('git status failed')
        entries = status_result.stdout.split(b'\x00')
        changes: list[str] = []
        n = 0
        while n < len(entries):
            entry = entries[n]
            n += 1
            if not entry:
                continue
            status = entry[:2].decode('ascii', 'replace')
            path = entry[3:].decode('utf-8', 'replace')
            if 'R' in status or 'C' in status:
                # porcelain -z emits an extra old path after a rename/copy
                n += 1
            if visible(path):
                changes.append(f'{status} {path}')
        lines = [
            '## Repository signal (automatic; NOT proof of completion)',
            f'- Branch / HEAD: `{safe_inline(branch)}` / `{safe_inline(head)}`',
            f'- Working tree: {len(changes)} changed path(s) outside agent configuration.',
        ]
        if changes:
            lines.append('- Paths (max 10; status + path):')
            lines.extend(f'  - `{safe_inline(name)}`' for name in changes[:MAX_PATHS])
            if len(changes) > MAX_PATHS:
                lines.append(f'  - … and {len(changes) - MAX_PATHS} more.')
        else:
            lines.append('- No application-file changes detected by Git.')
        return '\n'.join(lines) + '\n'
    except (FileNotFoundError, subprocess.TimeoutExpired, RuntimeError):
        return '## Repository signal (automatic; NOT proof of completion)\n- Git status unavailable.\n'


class LockedState:
    def __init__(self, root: Path):
        self.path = root / LOCK_REL
        self.taken = False

    def __enter__(self) -> 'LockedState':
        deadline = time.monotonic() + 1.7
        while time.monotonic() < deadline:
            try:
                fd = os.open(self.path, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
                os.close(fd)
                self.taken = True
                return self
            except FileExistsError:
                try:
                    if time.time() - self.path.stat().st_mtime > 60:
                        self.path.unlink(missing_ok=True)
                except OSError:
                    pass
                time.sleep(0.06)
        return self

    def __exit__(self, *exc: object) -> None:
        if self.taken:
            self.path.unlink(missing_ok=True)


def sync(root: Path) -> tuple[bool, str]:
    state = root / STATE_REL
    if not state.is_file():
        return False, f'Missing {STATE_REL}'
    with LockedState(root) as lock:
        if not lock.taken:
            return False, 'Another state sync is in progress'
        existing = state.read_text(encoding='utf-8')
        if existing.count(START) != 1 or existing.count(END) != 1:
            return False, 'STATE.md must contain exactly one AUTO-SNAPSHOT marker pair'
        before, remainder = existing.split(START, 1)
        _, after = remainder.split(END, 1)
        updated = before + START + '\n' + snapshot(root) + END + after
        if updated == existing:
            return True, 'No change'
        temp = None
        try:
            with tempfile.NamedTemporaryFile('w', dir=state.parent, encoding='utf-8',
                                             newline='\n', delete=False, prefix='.state-') as out:
                temp = Path(out.name)
                out.write(updated)
            os.replace(temp, state)
        finally:
            if temp and temp.exists():
                temp.unlink()
        return True, 'Updated repository signal'


def log_hook(root: Path, payload: dict) -> None:
    event = payload.get('hook_event_name')
    if event not in ('SessionStart', 'PreCompact', 'Stop'):
        return  # Manual runs must not look like automatic hook execution.
    try:
        with (root / LOG_REL).open('a', encoding='utf-8') as log:
            log.write(json.dumps({
                'event': event,
                'timestamp': datetime.now(timezone.utc).isoformat(),
                'cwd': payload.get('cwd') or str(Path.cwd()),
            }, ensure_ascii=False) + '\n')
    except OSError as exc:
        print(f'state_sync: diagnostic log unavailable: {exc}', file=sys.stderr)


def main(argv: list[str]) -> int:
    action = argv[1] if len(argv) > 1 else 'sync'
    if action not in ('start', 'sync', 'doctor'):
        print('Usage: python scripts/state_sync.py [start|sync|doctor]', file=sys.stderr)
        return 2
    payload = hook_payload() if action != 'doctor' else {}
    root = root_for(payload)
    state_file = root / STATE_REL
    if action == 'doctor':
        print(f'Python: {sys.version.split()[0]}')
        print(f'Project root: {root}')
        ok, result = sync(root)
        print(f'State sync: {result}')
        for config in (root / '.claude/settings.json', root / '.codex/hooks.json'):
            try:
                json.loads(config.read_text(encoding='utf-8'))
                print(f'{config.relative_to(root)}: valid JSON')
            except (OSError, ValueError) as exc:
                ok = False
                print(f'{config.relative_to(root)}: ERROR {exc}')
        return 0 if ok else 1
    try:
        ok, result = sync(root)
        if not ok:
            # Do not block the agent, but surface configuration mistakes.
            print(f'state_sync: {result}', file=sys.stderr)
            return 0
        log_hook(root, payload)
        if action == 'start':
            content = state_file.read_text(encoding='utf-8')
            # Plain stdout is model context for SessionStart in both products.
            if len(content) > MAX_CONTEXT_CHARS:
                content = content[:MAX_CONTEXT_CHARS] + '\n[truncated; read STATE.md for the rest]'
            print('Project state (repository notes; verify against code):\n' + content)
        return 0
    except (OSError, UnicodeError, ValueError, subprocess.SubprocessError) as exc:
        print(f'state_sync: {type(exc).__name__}: {exc}', file=sys.stderr)
        return 0  # Advisories never block the coding task.


if __name__ == '__main__':
    raise SystemExit(main(sys.argv))
