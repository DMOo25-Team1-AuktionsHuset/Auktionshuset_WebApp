# State

## Goal
- Register devices using the backend's default auction house and only a device number.

## Done (verified)
- Device registration uses AuctionHouseDefaults.DefaultAuctionHouseId; the request and administration form require only a device number.
- Registration still returns the device ID and long secret; API tests confirm an extra auction-house ID cannot change the stored house.

## Active
- Automatic USB transfer of device credentials is not implemented in this milestone.
- Full two-device and web journey through real SignalR, WebSocket, PostgreSQL, RabbitMQ, closure and invoicing remains unverified.

## Blockers
- Docker engine was unreachable; isolated PostgreSQL and RabbitMQ were unavailable. PostgreSQL and external WebSocket tests were skipped.
- No ESP32 was identified; physical hardware and firmware-to-backend integration were not tested.

## Next
- Implement automatic USB credential transfer in a later milestone.
- Run the isolated end-to-end device acceptance and physical ESP32 acceptance when dependencies and hardware are available.

## Verification
- Focused registration handler, device administration authorization, registration API and device admin page tests passed sequentially.
- Full suite and hardware tests for this change: not run.

<!-- AUTO-SNAPSHOT:BEGIN -->
## Repository signal (automatic; NOT proof of completion)
- Branch / HEAD: `Feature/Embedded-Device2.0` / `274a1c9`
- Working tree: 0 changed path(s) outside agent configuration.
- No application-file changes detected by Git.
<!-- AUTO-SNAPSHOT:END -->
