namespace MeshhessenClient.Protocols.MeshCore;

/// <summary>In-memory registry for MeshCore contacts/nodes and their latest telemetry.</summary>
public sealed class MeshCoreNodeRegistry
{
    private readonly Dictionary<string, MeshCoreNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public event EventHandler? Changed;

    public IReadOnlyList<MeshCoreNode> Snapshot()
    {
        lock (_sync) return _nodes.Values.OrderBy(x => x.Name).ToArray();
    }

    public IReadOnlyList<MeshCoreNode> GetByType(MeshCoreContactType type)
    {
        lock (_sync) return _nodes.Values.Where(x => x.Type == type).OrderBy(x => x.Name).ToArray();
    }

    public MeshCoreNode Upsert(MeshCoreContact contact)
    {
        var key = Convert.ToHexString(contact.PublicKey);
        MeshCoreNode node;
        lock (_sync)
        {
            if (_nodes.TryGetValue(key, out node!))
            {
                node.Name = string.IsNullOrWhiteSpace(contact.Name) ? node.Name : contact.Name;
                node.Type = contact.Type;
                node.PathLength = contact.PathLength;
                node.LastAdvert = contact.LastAdvert;
                node.Latitude = contact.Latitude;
                node.Longitude = contact.Longitude;
                node.LastSeenUtc = DateTimeOffset.UtcNow;
            }
            else
            {
                node = new MeshCoreNode(key, contact.Name, contact.Type, DateTimeOffset.UtcNow)
                {
                    PathLength = contact.PathLength,
                    LastAdvert = contact.LastAdvert,
                    Latitude = contact.Latitude,
                    Longitude = contact.Longitude
                };
                _nodes[key] = node;
            }
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return node;
    }

    public int UpsertRange(IEnumerable<MeshCoreContact> contacts)
    {
        var count = 0;
        foreach (var contact in contacts)
        {
            Upsert(contact);
            count++;
        }
        return count;
    }

    public bool TryGet(string publicKeyHex, out MeshCoreNode? node)
    {
        lock (_sync) return _nodes.TryGetValue(publicKeyHex, out node);
    }

    public void UpdatePosition(string publicKeyHex, double latitude, double longitude, double? altitude = null)
    {
        lock (_sync)
        {
            if (!_nodes.TryGetValue(publicKeyHex, out var node))
            {
                node = new MeshCoreNode(publicKeyHex, publicKeyHex[..Math.Min(12, publicKeyHex.Length)], MeshCoreContactType.Unknown, DateTimeOffset.UtcNow);
                _nodes[publicKeyHex] = node;
            }
            node.Latitude = latitude;
            node.Longitude = longitude;
            node.Altitude = altitude;
            node.LastSeenUtc = DateTimeOffset.UtcNow;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

public sealed class MeshCoreNode
{
    public MeshCoreNode(string publicKeyHex, string name, MeshCoreContactType type, DateTimeOffset lastSeenUtc)
    {
        PublicKeyHex = publicKeyHex;
        Name = string.IsNullOrWhiteSpace(name) ? publicKeyHex[..Math.Min(12, publicKeyHex.Length)] : name;
        Type = type;
        LastSeenUtc = lastSeenUtc;
    }

    public string PublicKeyHex { get; }
    public string Name { get; set; }
    public MeshCoreContactType Type { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public uint LastAdvert { get; set; }
    public sbyte PathLength { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Altitude { get; set; }
    public double? SnrDb { get; set; }
    public int? BatteryPercent { get; set; }
    public bool HasPosition => Latitude.HasValue && Longitude.HasValue;
}
