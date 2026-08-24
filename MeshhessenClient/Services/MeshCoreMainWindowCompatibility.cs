using System.Windows.Controls;
using MeshhessenClient.Protocols.MeshCore;

namespace MeshhessenClient;

/// <summary>
/// Compatibility shim for the current MeshCore window code while the map/node list
/// is being consolidated into the main XAML layout.
/// </summary>
public partial class MeshCoreMainWindow
{
    private ListBox MapNodesListBox { get; } = new();

    // MeshCoreMapCanvas exposes Action<MeshCoreMapNode>, so provide the exact
    // delegate signature expected by the event subscription in the window.
    private void MeshCoreMap_NodeClicked(MeshCoreMapNode node)
    {
        StatusText.Text = $"Selected {node.Name} ({node.Type}) — {node.Latitude:F5}, {node.Longitude:F5}";
        SelectContactById(node.Id);
    }
}
