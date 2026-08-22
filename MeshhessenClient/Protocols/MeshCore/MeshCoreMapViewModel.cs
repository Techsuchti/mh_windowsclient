using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MeshhessenClient.Protocols.MeshCore;

public sealed class MeshCoreMapViewModel : INotifyPropertyChanged
{
    public ObservableCollection<MeshCoreMapNode> Nodes { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Replace(IEnumerable<MeshCoreNode> nodes)
    {
        Nodes.Clear();
        foreach (var node in MeshCoreMapProjection.WithPosition(nodes))
            Nodes.Add(node);
        OnPropertyChanged(nameof(Nodes));
    }

    public MeshCoreMapNode? Select(string id) => Nodes.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
