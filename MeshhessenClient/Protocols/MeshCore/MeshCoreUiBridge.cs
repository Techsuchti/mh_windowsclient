using System.Collections.ObjectModel;

namespace MeshhessenClient.Protocols.MeshCore;

/// <summary>
/// Adapter between MeshCore protocol state and the existing WPF presentation layer.
/// Keeps the existing MainWindow/Mapsui views independent of transport details.
/// </summary>
public sealed class MeshCoreUiBridge : IDisposable
{
    private readonly MeshCoreNodeRegistry _registry;

    public ObservableCollection<MeshCoreNode> Nodes { get; } = new();
    public ObservableCollection<MeshCoreNode> Repeaters { get; } = new();
    public ObservableCollection<MeshCoreNode> RoomServers { get; } = new();

    public event EventHandler? Changed;

    public MeshCoreUiBridge(MeshCoreNodeRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _registry.Changed += RegistryChanged;
        Refresh();
    }

    public void Refresh()
    {
        Replace(Nodes, _registry.Snapshot());
        Replace(Repeaters, _registry.GetByType(MeshCoreContactType.Repeater));
        Replace(RoomServers, _registry.GetByType(MeshCoreContactType.RoomServer));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<MeshCoreNode> MappedNodes =>
        _registry.Snapshot().Where(x => x.HasPosition).ToArray();

    private void RegistryChanged(object? sender, EventArgs e) => Refresh();

    private static void Replace(ObservableCollection<MeshCoreNode> target, IEnumerable<MeshCoreNode> source)
    {
        target.Clear();
        foreach (var item in source) target.Add(item);
    }

    public void Dispose() => _registry.Changed -= RegistryChanged;
}
