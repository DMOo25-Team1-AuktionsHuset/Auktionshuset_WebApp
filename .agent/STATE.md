# State

## Goal
- Perform isolated acceptance of backend and firmware device-auction flow.

## Done (verified)
- Backend regression suite and firmware host checks passed; firmware compiled for ESP32-S3 using example configuration.
- Existing tests cover portions of authorization, SignalR, WebSocket, bidding and billing separately; no complete two-device/web journey was established.

## Active
- Full two-device and web journey through real SignalR, WebSocket, PostgreSQL, RabbitMQ, closure and invoicing remains unverified.

## Blockers
- Docker engine was unreachable; isolated PostgreSQL and RabbitMQ were unavailable. PostgreSQL and external WebSocket tests were skipped.
- No ESP32 was identified; physical hardware and firmware-to-backend integration were not tested.

## Next
- Start isolated PostgreSQL and RabbitMQ and run the complete acceptance journey, including failures, restart and billing; run physical ESP32 acceptance when available.

## Verification
- Backend `dotnet test`: 492 passed, 47 skipped, 0 failed (42 PostgreSQL and 5 external WebSocket tests skipped).
- Firmware: 37 host checks passed with mocked hardware/network; ESP32-S3 Arduino build succeeded with core 3.3.11. No hardware test.

<!-- AUTO-SNAPSHOT:BEGIN -->
## Repository signal (automatic; NOT proof of completion)
- Branch / HEAD: `Feature/Embedded-Device2.0` / `4ffbeaa`
- Working tree: 0 changed path(s) outside agent configuration.
- No application-file changes detected by Git.
<!-- AUTO-SNAPSHOT:END -->
