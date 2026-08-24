namespace MeshhessenClient.Protocols.MeshCore;

/// <summary>Projects MeshCore nodes into a UI-neutral map representation.</summary>
public sealed record MeshCoreMapNode(
    string Id,
    string Name,
    MeshCoreContactType Type,
    double Latitude,
    double Longitude,
    DateTimeOffset LastSeenUtc);

public static class MeshCoreMapProjection
{
    public static IReadOnlyList<MeshCoreMapNode> WithPosition(IEnumerable<MeshCoreNode> nodes)
        => nodes
            .Where(n => n.HasPosition)
            .Select(n => new MeshCoreMapNode(
                n.PublicKeyHex,
                n.Name,
                n.Type,
                n.Latitude!.Value,
                n.Longitude!.Value,
                n.LastSeenUtc))
            .OrderBy(n => n.Name)
            .ToArray();
}
