namespace MeshhessenClient.Protocols.MeshCore;

/// <summary>MeshCore channel configuration and display information.</summary>
public sealed record MeshCoreChannel(
    byte Index,
    string Name,
    byte[] Secret);
