using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MeshhessenClient.Protocols.MeshCore;

namespace MeshhessenClient.Views;

public partial class MeshCoreMapView : UserControl
{
    private MeshCoreNodeRegistry? _registry;

    public MeshCoreMapView()
    {
        InitializeComponent();
    }

    public void Bind(MeshCoreNodeRegistry registry)
    {
        if (_registry != null) _registry.Changed -= RegistryChanged;
        _registry = registry;
        _registry.Changed += RegistryChanged;
        Render();
    }

    private void RegistryChanged(object? sender, EventArgs e)
        => Dispatcher.Invoke(Render);

    private void OnMapSizeChanged(object sender, SizeChangedEventArgs e) => Render();

    private void Render()
    {
        MapCanvas.Children.Clear();
        if (_registry == null || MapCanvas.ActualWidth < 20 || MapCanvas.ActualHeight < 20) return;

        var nodes = _registry.Snapshot().Where(n => n.Latitude.HasValue && n.Longitude.HasValue).ToArray();
        if (nodes.Length == 0)
        {
            MapCanvas.Children.Add(new TextBlock { Text = "Keine Nodes mit GPS-Position vorhanden.", Foreground = Brushes.White, Margin = new Thickness(12) });
            return;
        }

        var minLat = nodes.Min(n => n.Latitude!.Value);
        var maxLat = nodes.Max(n => n.Latitude!.Value);
        var minLon = nodes.Min(n => n.Longitude!.Value);
        var maxLon = nodes.Max(n => n.Longitude!.Value);
        var latSpan = Math.Max(maxLat - minLat, 0.0001);
        var lonSpan = Math.Max(maxLon - minLon, 0.0001);

        foreach (var node in nodes)
        {
            var x = 20 + (node.Longitude!.Value - minLon) / lonSpan * Math.Max(1, MapCanvas.ActualWidth - 40);
            var y = 20 + (maxLat - node.Latitude!.Value) / latSpan * Math.Max(1, MapCanvas.ActualHeight - 40);
            var marker = new Ellipse { Width = 12, Height = 12, Fill = Brushes.DeepSkyBlue, Stroke = Brushes.White, StrokeThickness = 1 };
            Canvas.SetLeft(marker, x - 6);
            Canvas.SetTop(marker, y - 6);
            MapCanvas.Children.Add(marker);

            var label = new TextBlock { Text = node.Name, Foreground = Brushes.White, FontSize = 11 };
            Canvas.SetLeft(label, x + 8);
            Canvas.SetTop(label, y - 8);
            MapCanvas.Children.Add(label);
        }
    }
}
