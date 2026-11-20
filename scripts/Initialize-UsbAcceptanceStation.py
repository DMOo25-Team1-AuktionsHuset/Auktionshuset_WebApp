"""One-time local station setup for the isolated physical USB acceptance run.

Only safe identity is printed. The Wi-Fi password and random station grant key
are saved through the station's Windows DPAPI storage, never in this repository.
"""

from __future__ import annotations

import argparse
import getpass
import secrets
import sys
import uuid
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--embedded-root", type=Path, required=True)
    parser.add_argument("--backend-host", required=True)
    parser.add_argument("--ca-pem", type=Path, required=True)
    parser.add_argument("--approved-origin", default="https://localhost:7063")
    parser.add_argument("--usb-serial")
    parser.add_argument("--wifi-ssid")
    args = parser.parse_args()

    sys.path.insert(0, str(args.embedded_root / "HardwareTest" / "UsbStation"))
    from station_config import DPAPIProtector, default_path, save
    from station_core import PySerialBackend, Settings

    if default_path().exists():
        print("Station settings already exist; refusing to overwrite them.", file=sys.stderr)
        return 1
    ports = [port for port in PySerialBackend().ports()
             if (args.usb_serial is None or port.serial_number == args.usb_serial)
             and port.serial_number and port.location
             and port.vid is not None and port.pid is not None]
    if len(ports) != 1:
        print("Expected the connected device at one fixed USB location.", file=sys.stderr)
        return 1
    ssid = args.wifi_ssid or input("2.4 GHz Wi-Fi SSID (WPA2/WPA3): ").strip()
    password = getpass.getpass("Wi-Fi password (not echoed): ")
    settings = Settings(
        str(uuid.uuid4()), args.approved_origin, 8765,
        ports[0].vid, ports[0].pid, ports[0].location,
        ssid, password, args.backend_host, 55134, True,
        args.ca_pem.read_text(encoding="ascii"), secrets.token_bytes(32),
    )
    try:
        save(settings, default_path(), DPAPIProtector())
    except Exception:
        print("Invalid station settings; nothing was saved.", file=sys.stderr)
        return 1
    print(f"Station ready at USB location {ports[0].location}. Station ID: {settings.station_id}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
