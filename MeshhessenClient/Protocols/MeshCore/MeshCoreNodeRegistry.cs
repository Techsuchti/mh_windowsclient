namespace MeshhessenClient.Protocols.MeshCore;

/// <summary>In-memory registry for MeshCore contacts/nodes and their latest telemetry.</summary>
public sealed class MeshCoreNodeRegistry
{
    private readonly Dictionary<string, MeshCoreNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public IReadOnlyList<MeshCoreNode> Snapshot()
    {
        lock (_sync) return _nodes.Values.OrderBy(x => x.Name).ToArray();
    }

    public MeshCoreNode Upsert(MeshCoreContact contact)
    {
        var key = Convert.ToHexString(contact.PublicKey);
        lock (_sync)
        {
            if (_nodes.TryGetValue(key, out var existing))
            {
                existing.Name = string.IsNullOrWhiteSpace(contact.Name) ? existing.Name : contact.Name;
                existing.LastSeenUtc = DateTimeOffset.UtcNow;
                return existing;
            }

            var node = new MeshCoreNode(key, contact.Name, DateTimeOffset.UtcNow);
            _nodes[key] = node;
            return node;
        }
    }

    public void UpdatePosition(string publicKeyHex, double latitude, double longitude, double? altitude = null)
    {
        lock (_sync)
        {
            if (!_nodes.TryGetValue(publicKeyHex, out var node))
            {
                node = new MeshCoreNode(publicKeyHex, publicKeyHex[..Math.Min(12, publicKeyHex.Length)], DateTimeOffset.UtcNow);
                _nodes[publicKeyHex] = node;
            }
            node.Latitude = latitude;
            node.Longitude = longitude;
            node.Altitude = altitude;
            node.LastSeenUtc = DateTimeOffset.UtcNow;
        }
    }
}

public sealed class MeshCoreNode
{
    public MeshCoreNode(string publicKeyHex, string name, DateTimeOffset lastSeenUtc)
    {
        PublicKeyHex = publicKeyHex;
        Name = name;
        LastSeenUtc = lastSeenUtc;
    }

    public string PublicKeyHex { get; }
    public string Name { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Altitude { get; set; }
    public double? SnrDb { get; set; }
    public int? BatteryPercent { get; set; }
}
