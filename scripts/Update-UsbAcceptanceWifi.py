"""Update only the protected test station's Wi-Fi password from a local prompt."""

from __future__ import annotations

import dataclasses
import getpass
import hmac
import sys
from pathlib import Path


def main() -> int:
    if len(sys.argv) != 2:
        return 2
    sys.path.insert(0, str(Path(sys.argv[1]) / "HardwareTest" / "UsbStation"))
    from station_config import DPAPIProtector, default_path, load, save

    path = default_path()
    protector = DPAPIProtector()
    old = load(path, protector)
    if old.wifi_ssid != "Zyxel_EC1F 2.4":
        raise RuntimeError("Unexpected station Wi-Fi network")
    password = getpass.getpass("Aktuel 2,4 GHz-adgangskode (skjult): ")
    repeated = getpass.getpass("Gentag adgangskoden (skjult): ")
    if not hmac.compare_digest(password, repeated):
        print("PASSWORD_CONFIRMATION_MISMATCH")
        return 1
    updated = dataclasses.replace(old, wifi_password=password)
    save(updated, path, protector, replace=True)
    checked = load(path, protector)
    if not hmac.compare_digest(checked.wifi_password, password) or \
            checked.station_id != old.station_id or checked.grant_key != old.grant_key:
        raise RuntimeError("Protected station settings could not be verified")
    print("STATION_WIFI_UPDATED")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception:
        print("STATION_WIFI_UPDATE_FAILED")
        raise SystemExit(1)
