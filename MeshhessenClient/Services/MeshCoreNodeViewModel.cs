using System.Collections.ObjectModel;
using MeshhessenClient.Protocols.MeshCore;

namespace MeshhessenClient.Services;

/// <summary>Presentation model for the MeshCore node/repeater list.</summary>
public sealed class MeshCoreNodeViewModel
{
    private readonly MeshCoreNodeRegistry _registry;

    public ObservableCollection<MeshCoreNode> Nodes { get; } = new();

    public MeshCoreNodeViewModel(MeshCoreNodeRegistry registry)
    {
        _registry = registry;
        Refresh();
        _registry.Changed += OnRegistryChanged;
    }

    public void Refresh()
    {
        Nodes.Clear();
        foreach (var node in _registry.Snapshot()) Nodes.Add(node);
    }

    public IReadOnlyList<MeshCoreNode> Repeaters =>
        _registry.GetByType(MeshCoreContactType.Repeater);

    public IReadOnlyList<MeshCoreNode> RoomServers =>
        _registry.GetByType(MeshCoreContactType.RoomServer);

    private void OnRegistryChanged(object? sender, EventArgs e) => Refresh();
}
