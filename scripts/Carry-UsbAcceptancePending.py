"""Keep a pending test device across an isolated acceptance-host restart.

Only Device and DeviceCredentials from the throwaway PostgreSQL database are
copied. The SQL stays encrypted with Windows DPAPI at rest and is never logged.
"""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("action", choices=("snapshot", "restore"))
    parser.add_argument("database")
    parser.add_argument("--embedded-root", type=Path, required=True)
    args = parser.parse_args()
    if not re.fullmatch(r"codex_stock_[0-9a-f]{32}", args.database):
        raise RuntimeError("Only an isolated acceptance database is allowed")
    sys.path.insert(0, str(args.embedded_root / "HardwareTest" / "UsbStation"))
    from station_config import DPAPIProtector

    path = Path(os.environ["LOCALAPPDATA"]) / "AuktionshusetUsbAcceptance" / "pending.bin"
    protector = DPAPIProtector()
    if args.action == "snapshot":
        if path.exists():
            raise RuntimeError("An encrypted pending snapshot already exists")
        command = ["docker", "exec", "ah-usb-accept-postgres", "pg_dump", "-U", "codex",
                   "-d", args.database, "--data-only", "--column-inserts",
                   "-t", '"Device"', "-t", '"DeviceCredentials"']
        result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, check=False)
        if result.returncode or b"9001" not in result.stdout or b"INSERT INTO" not in result.stdout:
            raise RuntimeError("The pending test device could not be snapshotted")
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(protector.protect(result.stdout))
        print("Pending test device saved under Windows DPAPI.")
    else:
        if not path.exists():
            raise RuntimeError("The encrypted pending snapshot is missing")
        count = subprocess.run(["docker", "exec", "ah-usb-accept-postgres", "psql", "-U", "codex",
                                "-d", args.database, "-Atc", 'SELECT COUNT(*) FROM "Device" WHERE "DeviceNumber"=9001'],
                               stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, check=False)
        if count.returncode or count.stdout.strip() != b"0":
            raise RuntimeError("Destination database is not empty for device 9001")
        sql = protector.unprotect(path.read_bytes())
        result = subprocess.run(["docker", "exec", "-i", "ah-usb-accept-postgres", "psql", "-U", "codex",
                                 "-d", args.database, "-v", "ON_ERROR_STOP=1"], input=sql,
                                stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False)
        if result.returncode:
            raise RuntimeError("Pending test device could not be restored")
        path.unlink()
        print("Same pending test device restored; encrypted temporary snapshot removed.")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception:
        print("Pending-device transfer failed; no credentials were printed.", file=sys.stderr)
        raise SystemExit(1)
