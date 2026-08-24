# MeshCore Windows Client

Native Windows desktop client for **MeshCore Companion** radios. Built with WPF and .NET 8.

> This branch is the MeshCore-only migration of the former Meshhessen/Meshtastic Windows client.

## Features

- MeshCore Companion protocol
- USB/Serial Companion transport
- MeshCore Companion BLE transport
- Device and self information
- Contact synchronization
- Channel synchronization
- Channel messaging
- Direct-message API
- Message synchronization / push handling
- Offline-capable local application architecture
- Existing Mapsui, SQLite and Windows desktop infrastructure retained for the next UI phases
- Windows x64 self-contained publishing

## MeshCore Companion

The implementation follows the current MeshCore Companion protocol documentation. Companion devices use a simple binary packet protocol. BLE uses the Nordic-UART-style MeshCore service; stream transports use Companion framing. Commands are serialized and asynchronous push packets are handled separately.

Official specification: https://github.com/meshcore-dev/MeshCore/blob/main/docs/companion_protocol.md

## Transport

### USB / Serial

MeshCore Companion serial firmware can be connected through a Windows COM port. The current UI exposes 115200 and 230400 baud rates.

### Bluetooth LE

MeshCore Companion BLE uses:

- Service: `6E400001-B5A3-F393-E0A9-E50E24DCCA9E`
- RX: `6E400002-B5A3-F393-E0A9-E50E24DCCA9E`
- TX: `6E400003-B5A3-F393-E0A9-E50E24DCCA9E`

The dedicated BLE transport subscribes to TX notifications and writes commands to RX.

## Architecture

```text
MeshCore Windows Client
        |
        +-- WPF UI
        |
        +-- MeshCoreApplicationController
        |      +-- Device
        |      +-- Contacts
        |      +-- Channels
        |      +-- Messages
        |
        +-- MeshCore Companion
        |      +-- Frame codec
        |      +-- Startup coordinator
        |      +-- Command handling
        |
        +-- Transports
               +-- USB/Serial
               +-- BLE
               +-- TCP (planned)
```

## Development status

The `feature/meshcore-client` branch is the migration branch. The legacy Meshtastic implementation remains in the repository until the remaining UI, persistence, TCP, KISS and network-management migrations are complete. It is intentionally not used by the MeshCore application startup.

### Roadmap

- [x] MeshCore Companion protocol foundation
- [x] Serial Companion connection
- [x] BLE Companion transport
- [x] Device / contact / channel models
- [x] Channel messaging foundation
- [x] Direct-message command foundation
- [ ] Full response-matched command queue
- [ ] TCP Companion transport
- [ ] Complete DM UI and persistence
- [ ] Map integration for MeshCore contacts/repeaters
- [ ] Repeater management
- [ ] Room Server support
- [ ] MeshCore KISS modem protocol
- [ ] Remove remaining Meshtastic source/protobuf dependencies

## Build

```powershell
dotnet restore MeshhessenClient.sln
dotnet build MeshhessenClient.sln -c Release
dotnet test MeshhessenClient.sln -c Release
dotnet publish MeshhessenClient/MeshhessenClient.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false
```

## License

See `LICENSE` in this repository.
