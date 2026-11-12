"""Run the isolated USB acceptance backend, fixed USB station and staff page.

All station secrets are loaded from the current Windows user's DPAPI record.
The child processes inherit them in memory; no secret is printed or logged here.
Stop with Ctrl+C to close the services. A resumed test database is retained.
"""

from __future__ import annotations

import argparse
import os
import re
import ssl
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path


def wait_for(url: str, context: ssl.SSLContext | None, process: subprocess.Popen,
             seconds: int = 45, origin: str | None = None) -> None:
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        if process.poll() is not None:
            raise RuntimeError("A local acceptance service exited during startup")
        try:
            request = urllib.request.Request(url, headers={"Origin": origin} if origin else {})
            with urllib.request.urlopen(request, context=context, timeout=2) as response:
                if response.status == 200:
                    return
        except (OSError, urllib.error.URLError):
            pass
        time.sleep(0.5)
    raise RuntimeError("A local acceptance service did not become ready")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--embedded-root", type=Path, required=True)
    parser.add_argument("--backend-ip", required=True)
    parser.add_argument("--resume-db", help="Existing isolated codex_stock_* database to retain")
    parser.add_argument("--without-station", action="store_true",
                        help="Test normal device operation while USB supplies power only")
    args = parser.parse_args()
    if args.resume_db and not re.fullmatch(r"codex_stock_[0-9a-f]{32}", args.resume_db):
        parser.error("--resume-db must name an isolated codex_stock_* database")
    root = Path(__file__).resolve().parent.parent
    sys.path.insert(0, str(args.embedded_root / "HardwareTest" / "UsbStation"))
    from station_config import DPAPIProtector, default_path, load

    settings = load(default_path(), DPAPIProtector())
    if settings.backend_host != args.backend_ip or settings.backend_port != 55134 or not settings.tls:
        raise RuntimeError("Station settings do not match the isolated TLS backend")
    tls_dir = Path(os.environ["LOCALAPPDATA"]) / "AuktionshusetUsbAcceptance" / "tls"
    certificate = tls_dir / "backend.pfx"
    ca_file = tls_dir / "ca.pem"
    if not certificate.is_file() or not ca_file.is_file():
        raise RuntimeError("The isolated TLS certificate is missing")
    stop_file = tls_dir.parent / "stop"
    stop_file.unlink(missing_ok=True)

    backend_env = os.environ.copy()
    backend_env.update({
        "ASPNETCORE_ENVIRONMENT": "Development",
        "AH_BROWSER_ACCEPTANCE": "1",
        "AUCTION_STOCK_TEST_CONNECTION":
            "Host=127.0.0.1;Port=55439;Database=postgres;Username=codex;Password=codex-isolated-test",
        "AH_USB_ACCEPTANCE_BIND_IP": args.backend_ip,
        "AH_USB_ACCEPTANCE_CERT_PFX": str(certificate),
        "AH_USB_ACCEPTANCE_STATION_ID": settings.station_id,
        "AH_USB_ACCEPTANCE_ORIGIN": settings.approved_origin,
        "AH_USB_ACCEPTANCE_GRANT_KEY_HEX": settings.grant_key.hex(),
        "AH_BROWSER_STOP_FILE": str(stop_file),
    })
    if args.resume_db:
        backend_env["AH_USB_ACCEPTANCE_RESUME_DB"] = args.resume_db
    frontend_env = os.environ.copy()
    frontend_env.update({
        "ASPNETCORE_ENVIRONMENT": "Development",
        "ASPNETCORE_URLS": "https://localhost:7063",
        "Api__BaseUrl": "http://127.0.0.1:55133",
    })
    hidden = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
    children: list[subprocess.Popen] = []
    try:
        backend = subprocess.Popen(
            ["dotnet", "test", "Auktionshuset.Api.Test/Auktionshuset.Api.Test.csproj",
             "--no-build", "--filter", "FullyQualifiedName~BrowserAcceptanceHost",
             "--logger", "console;verbosity=quiet"],
            cwd=root, env=backend_env, stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL, creationflags=hidden,
        )
        children.append(backend)
        context = ssl.create_default_context(cafile=str(ca_file))
        wait_for(f"https://{args.backend_ip}:55134/health/live", context, backend)
        if not args.without_station:
            station = subprocess.Popen(
                [sys.executable, "-B", str(args.embedded_root / "HardwareTest" / "UsbStation" / "station.py"), "run"],
                env=os.environ.copy(), stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL, creationflags=hidden,
            )
            children.append(station)
            wait_for("http://127.0.0.1:8765/v1/device", None, station, origin=settings.approved_origin)
        frontend = subprocess.Popen(
            ["dotnet", "run", "--no-launch-profile", "--no-build", "--project", "Auktionshuset"],
            cwd=root, env=frontend_env, stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL, creationflags=hidden,
        )
        children.append(frontend)
        # Python's OpenSSL store does not include the Windows-trusted dotnet
        # development certificate. This probe sends no credential and stays on loopback.
        wait_for("https://localhost:7063/login", ssl._create_unverified_context(), frontend)
        print("TLS-testbackend og medarbejderside kører" + (" uden USB-station." if args.without_station else " med USB-station."))
        print("Åbn https://localhost:7063/admin/devices i browseren.")
        print("Tryk Ctrl+C her for at stoppe alle tre tjenester.")
        while all(child.poll() is None for child in children):
            time.sleep(1)
        raise RuntimeError("A local acceptance service stopped unexpectedly")
    except KeyboardInterrupt:
        print("Stopper lokale accepttjenester.")
        return 0
    finally:
        stop_file.touch()
        for child in reversed(children[1:]):
            if child.poll() is None:
                child.terminate()
        if children:
            try:
                children[0].wait(timeout=10)
            except subprocess.TimeoutExpired:
                children[0].terminate()
        for child in children:
            try:
                child.wait(timeout=5)
            except subprocess.TimeoutExpired:
                child.kill()


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception:
        print("Local acceptance setup failed; check prerequisites and service availability.", file=sys.stderr)
        raise SystemExit(1)
