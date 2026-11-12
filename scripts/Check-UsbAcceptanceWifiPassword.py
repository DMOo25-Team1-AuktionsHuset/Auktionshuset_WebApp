"""Compare a locally entered Wi-Fi password with the protected station setting.

This helper prints only a match result; it never echoes or persists the input.
"""

from __future__ import annotations

import getpass
import hmac
import sys
from pathlib import Path


def main() -> int:
    if len(sys.argv) != 2:
        print("Embedded repository path required.")
        return 2
    sys.path.insert(0, str(Path(sys.argv[1]) / "HardwareTest" / "UsbStation"))
    from station_config import DPAPIProtector, default_path, load

    stored = load(default_path(), DPAPIProtector()).wifi_password
    entered = getpass.getpass("Aktuel adgangskode til Zyxel_EC1F 2.4 (skjult): ")
    print("STATION_PASSWORD_MATCH" if hmac.compare_digest(stored, entered)
          else "STATION_PASSWORD_MISMATCH")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception:
        print("Adgangskodekontrollen kunne ikke gennemfoeres uden at vise hemmeligheder.")
        raise SystemExit(1)
