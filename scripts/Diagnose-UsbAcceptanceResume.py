"""Resume one already pending isolated registration without printing credentials.

This is a physical acceptance diagnostic after a staff click has created the
pending record. It refuses to create a backend device or choose another USB port.
"""

from __future__ import annotations

import json
import re
import sys
import urllib.error
import urllib.request


API = "http://127.0.0.1:55133"
STATION = "http://127.0.0.1:8765"
ORIGIN = "https://localhost:7063"


def request(url: str, body: dict | None = None, headers: dict | None = None) -> dict:
    all_headers = dict(headers or {})
    data = None if body is None else json.dumps(body, separators=(",", ":")).encode("utf-8")
    if data is not None:
        all_headers["Content-Type"] = "application/json"
    try:
        with urllib.request.urlopen(urllib.request.Request(url, data=data, headers=all_headers), timeout=15) as response:
            return json.load(response)
    except urllib.error.HTTPError as error:
        try:
            code = json.load(error).get("error", "")
        except (OSError, ValueError):
            code = ""
        safe = code if isinstance(code, str) and re.fullmatch(r"[A-Za-z_]{1,40}", code) else "unavailable"
        raise RuntimeError(f"HTTP {error.code}: {safe}") from None


def main() -> int:
    station = request(f"{STATION}/v1/device", headers={"Origin": ORIGIN})
    if station.get("state") != "unconfigured" or not station.get("hardwareId"):
        raise RuntimeError("The connected device is no longer unconfigured")
    login = request(f"{API}/api/auth/login", {"email": "admin@example.test", "password": "test-password-123"})
    auth = {"Authorization": f"Bearer {login['accessToken']}"}
    pending = request(f"{API}/api/staff/devices/station-pending/{station['hardwareId']}", headers=auth)
    if pending.get("deviceNumber") != 9001 or pending.get("stage") != "registration":
        raise RuntimeError("The pending test registration does not match device 9001")
    registration = request(f"{API}/api/staff/devices/station-resume", {
        "stationId": station["stationId"], "connectionId": station["connectionId"],
        "deviceNumber": 9001, "hardwareId": station["hardwareId"],
        "presenceGrant": station["presenceGrant"],
    }, auth)
    if registration.get("deviceId") != pending.get("deviceId") or registration.get("deviceNumber") != 9001:
        raise RuntimeError("Resume returned another device identity")
    print("Backend resume returned the same pending device ID.", flush=True)
    result = request(f"{STATION}/v1/provision", {
        "connectionId": station["connectionId"], "deviceId": registration["deviceId"],
        "deviceNumber": 9001, "secret": registration["secret"],
    }, {"Origin": ORIGIN, "X-Station-Grant": registration["grant"]})
    if result.get("deviceId") != pending["deviceId"] or result.get("deviceNumber") != 9001:
        raise RuntimeError("Station acknowledgement did not match the pending device")
    print("Station acknowledged the saved configuration for device 9001.", flush=True)
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (KeyError, OSError, ValueError, RuntimeError) as error:
        # Only fixed-format status and error codes are allowed out. Never print
        # urllib exceptions, request bodies, grants, passwords, or secret values.
        message = str(error) if isinstance(error, RuntimeError) else "A local response was invalid"
        print(message, file=sys.stderr)
        raise SystemExit(1)
