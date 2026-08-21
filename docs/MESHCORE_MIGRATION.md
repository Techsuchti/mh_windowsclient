# MeshCore-only migration

The `feature/meshcore-client` branch converts the Windows application from a Meshtastic client into a dedicated MeshCore desktop client.

## Architecture

```text
WPF UI
  |
  +-- MeshCore application services
  |      |
  |      +-- Companion Protocol
  |      |      +-- BLE
  |      |      +-- Serial
  |      |      +-- TCP
  |      |
  |      +-- KISS modem protocol (planned)
  |
  +-- Common domain models
         +-- Contacts
         +-- Channels
         +-- Messages
         +-- Repeaters
         +-- Room servers
         +-- Positions
```

## Current phase

- MeshCore Companion constants and framing implemented.
- Companion runtime implemented for BLE/direct payloads and framed serial/TCP transports.
- Device/self information, contacts, channels and message events have dedicated MeshCore models.
- MeshCore BLE uses the Nordic-UART-style Companion service.
- Project branding now identifies the application as MeshCore Windows Client.
- Meshtastic protobuf/package dependencies remain temporarily because the existing WPF UI still references legacy services.

## Next migration steps

1. Replace `MeshtasticProtocolService` references in `MainWindow` with MeshCore application services.
2. Replace Meshtastic-specific node/channel/message models in the view models.
3. Migrate SQLite persistence to MeshCore contact/message/channel schemas.
4. Remove Meshtastic protobuf generation and legacy protocol services.
5. Add MeshCore serial and TCP Companion connection paths.
6. Add KISS modem support.
7. Add repeater/room-server administration and MeshCore route diagnostics.

## Safety rule

Do not merge this branch into `master` until the application builds and the Companion protocol has been tested against a real MeshCore device.
