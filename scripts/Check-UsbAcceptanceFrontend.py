"""Read-only rendering check for the isolated USB acceptance staff page."""

from __future__ import annotations

import http.cookiejar
import re
import ssl
import urllib.parse
import urllib.request


def main() -> None:
    base = "https://localhost:7063"
    cookies = http.cookiejar.CookieJar()
    opener = urllib.request.build_opener(
        urllib.request.HTTPCookieProcessor(cookies),
        urllib.request.HTTPSHandler(context=ssl._create_unverified_context()),
    )
    login = opener.open(base + "/login", timeout=10).read().decode("utf-8")
    token = re.search(r'name="__RequestVerificationToken" value="([^"]+)"', login)
    if token is None:
        raise RuntimeError("Isolated login form has no antiforgery token")
    data = urllib.parse.urlencode({
        "__RequestVerificationToken": token[1],
        "email": "admin@example.test", "password": "test-password-123",
    }).encode("utf-8")
    opener.open(base + "/auth/login", data=data, timeout=15).read()
    response = opener.open(base + "/admin/devices", timeout=25)
    html = response.read().decode("utf-8")
    print("PAGE_STATUS", response.status)
    print("PAGE_URL", response.url)
    print("HAS_ISSUE_SECTION", "Udlever device" in html)
    print("OVERVIEW_FAILED", "Oversigten kunne ikke hentes" in html)
    reason = re.search(r"Oversigten kunne ikke hentes \(([^)]{1,40})\)", html)
    print("OVERVIEW_REASON", reason[1] if reason else "unknown")
    print("HAS_DEVICE_9001", "Device 9001" in html)


if __name__ == "__main__":
    try:
        main()
    except Exception:
        print("ISOLATED_FRONTEND_CHECK_FAILED")
        raise SystemExit(1)
