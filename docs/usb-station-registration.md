# USB registration through the staff page

The staff page at `/admin/devices` uses the existing BillingStaff policy. Its
Blazor circuit calls the authenticated backend API. JavaScript in the employee's
browser calls the station at `http://127.0.0.1:<port>`; neither web server opens
a USB port. The page never renders the secret or puts it in browser storage.

## Configure the station and backend

Initialize the Windows station using the instructions in
`Auktionshuset_Embedded/HardwareTest/UsbStation/README.md`. Set its approved
origin to the exact HTTPS origin of the staff website. In the backend secret
store, set these configuration values (for example with environment variables):

| Backend setting | Value |
| --- | --- |
| `UsbStation__StationId` | Station ID printed by `station.py init` (lowercase UUID). |
| `UsbStation__ApprovedOrigin` | Exact HTTPS origin configured at the station, without trailing slash. |
| `UsbStation__GrantKeyHex` | The same 32-byte shared key entered at the station, as 64 hex characters. Keep it out of source control and logs. |

The backend currently supports one configured station. On the web frontend,
set `UsbStation__LocalUrl` to `http://127.0.0.1:<port>` if the station uses a
port other than the default `8765`. The browser module accepts only loopback
HTTP URLs. The backend returns 503 when its station configuration is absent or
invalid. Configure the frontend and station on the same approved HTTPS origin.
Keep the station computer and backend clocks synchronized; grants expire after
90 seconds. Configure TLS and a valid CA certificate at the station; a device
with TLS disabled cannot pass setup verification.

## Registration and retries

Connect an unconfigured device to the station's fixed USB port, enter its
number, and click **Registrér**. The browser reads the station's ephemeral
`connectionId`. The backend creates an inactive, pending device with
`AuctionHouseDefaults.DefaultAuctionHouseId` and a 64-character secret, then
issues a 90-second `AHSTATION/1` grant for that connection and identity. The
station status also supplies a short-lived signed presence grant. Backend
requires this proof of the current physical USB identity for registration,
resume and rotation; a browser-supplied hardware ID alone cannot authorize a
credential transfer. The station rechecks the USB descriptor immediately
before sending the serial command. The browser forwards the credential and
grant directly to the loopback station.
After its acknowledgement, the station asks the device to verify Wi-Fi, clock,
backend TCP, TLS certificate/hostname and its saved credential through the
credential-only backend endpoint. The page reads status again and compares the device
ID, number, connection ID and successful verification, then sends the station's
short-lived signed verification grant to activate the backend credential before
showing **Klar**. Pending devices do not appear in the available list. The
station adds its protected Wi-Fi, backend and TLS settings to the serial transfer.
The signed grant includes the NVS credential version; an earlier successful
check cannot complete a later credential rotation.

The firmware uses `AHCFG/1 VERIFY` for the check. It sends its stored ID and
secret directly to `POST /api/device/verify-setup` over TLS. This endpoint uses
the existing credential validator, including pending setup credentials, and
returns only verification, device ID and number. It issues no access token and
requires no customer or auction session. Normal WebSocket authentication and
the **Udlever** flow continue to require the existing session checks. A
deactivated device and a wrong credential both receive a generic 401. The
station exposes safe verification codes (`wifi`, `clock`, `backend`, `tls`,
`credential`, `rate_limit`) for staff feedback without returning credentials.

The station reports a stable HMAC-based `hardwareId` for the USB serial number.
The first backend registration derives a device ID from the station and current
connection, then stores `Provisioning:<hardwareId>` with an inactive credential.
Repeated requests on that connection return the same ID and secret. After an
unplug, power loss, lost acknowledgement or station restart, the page shows
the pending backend record separately from the device's saved configuration
and connection verification. **Genoptag registrering** explicitly returns
the same ID and secret for that hardware ID; a different device cannot resume
it. A configured device is recognized by its saved ID and number. Neither a
station restart nor a reconnect creates another backend device automatically.

For a lost secret on a registered device, **Rotér credential og overskriv
konfiguration** is a separate staff action. The same explicit repair is
available when SET was saved but the first network verification failed; it
keeps the existing device ID and number. Backend rejects rotation while a
customer session is active. Rotation keeps the device ID, increments the
credential version, disables the old credential immediately and records a
pending repair. **Genoptag reparation** recovers the same new secret after an
interruption. The station accepts repair only for the matching physical USB
connection and existing device identity. Firmware writes the new full record
to its alternate NVS slot and leaves the last complete record intact after a
failed write. A device with an old on-device secret cannot authenticate until
the explicit repair completes. No automatic NVS reset is provided.

The backend derives retryable credentials from the protected grant key and
device ID/version. Keep that key stable while registrations may need retrying;
rotate it only after reconciling pending attempts. This setup has not been
verified with physical hardware or the approved production browser. Acceptance
must cover serial disconnects, browser private-network permission, NTP availability,
TLS hostname/certificate rejection and status reconciliation after a lost acknowledgement.

## Bid recovery during ordinary use

After setup, USB serial is not needed for bidding. Firmware stores the complete
`placeBid` request in two alternating NVS records before sending. Following a
restart it queries backend over the authenticated device WebSocket for a recorded
result, using the original request ID, session, lot, auction revision and amount.
The lookup never places a new bid. A physical retry sends the saved bytes only
when the original session is still active. A new customer cannot inherit that
request. The screen shows the request ID, amount and receipt; encoder press
acknowledges a final receipt. An unknown request remains locked.

Staff can enter the device ID and request ID under **Afklar bud efter mistet svar**
on `/admin/devices`. The staff-only API reads the persisted bid command key and
shows an accepted/rejected result or the exact unresolved request reported by
the device. If the original session has ended and no result exists, an employee
can explicitly confirm that no bid was registered. Backend inserts a rejected
idempotency key under the auction lock; a delayed copy of that request cannot
create a bid. The device fetches that final result at reconnection or when its
bid button is pressed. Staff must not use the action while the original session
is active. A new `DeviceBidRecoveries` migration stores reports and the staff
audit; apply it before using this flow in production.

Focused checks:

```powershell
dotnet test Auktionshuset.Api.Test/Auktionshuset.Api.Test.csproj --filter 'FullyQualifiedName~StationRegistration_RepeatsSameIdentityAndBindsGrantToConnection|FullyQualifiedName~DeviceAdministrationEndpointsRequireStaffRole'
dotnet test Auktionshuset.Api.Test/Auktionshuset.Api.Test.csproj --filter 'FullyQualifiedName~DeviceAuthenticationEndpointTests|FullyQualifiedName~DeviceWebSocketInProcessTests'
dotnet test Auktionshuset.Tests/Auktionshuset.Tests.csproj --filter 'FullyQualifiedName~DeviceAdminPageTests'
node --test tests/usb-station.test.mjs
```
