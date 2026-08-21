namespace MeshhessenClient.Protocols.MeshCore;

public enum MeshCoreContactType : byte
{
    Unknown = 0,
    Companion = 1,
    Repeater = 2,
    RoomServer = 3
}

public sealed record MeshCoreDeviceInfo(
    byte FirmwareProtocolVersion,
    byte MaxContacts,
    byte MaxChannels,
    uint BlePin,
    string FirmwareBuild,
    string Model,
    string SemanticVersion);

public sealed record MeshCoreSelfInfo(
    MeshCoreContactType Type,
    int TxPowerDbm,
    int MaxTxPowerDbm,
    byte[] PublicKey,
    double? Latitude,
    double? Longitude,
    double RadioFrequencyMHz,
    double RadioBandwidthKHz,
    byte SpreadingFactor,
    byte CodingRate,
    string Name);

public sealed record MeshCoreContact(
    byte[] PublicKey,
    MeshCoreContactType Type,
    byte Flags,
    sbyte PathLength,
    byte[] Path,
    string Name,
    uint LastAdvert,
    double? Latitude,
    double? Longitude);

public sealed record MeshCoreChannel(
    byte Index,
    string Name,
    byte[] Secret);

public sealed record MeshCoreMessage(
    byte[]? SenderPublicKey,
    byte? ChannelIndex,
    string Text,
    uint Timestamp,
    int? SnrDb,
    bool IsDirectMessage);
