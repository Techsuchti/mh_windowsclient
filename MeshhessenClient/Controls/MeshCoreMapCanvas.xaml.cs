using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MeshhessenClient.Protocols.MeshCore;

namespace MeshhessenClient.Controls;

public partial class MeshCoreMapCanvas : UserControl
{
    private readonly MeshCoreMapCanvasState _state = new();
    private readonly Dictionary<string, MeshCoreMapNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private Point _dragStart;
    private bool _dragging;

    public event Action<MeshCoreMapNode>? NodeClicked;

    public MeshCoreMapCanvas()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Render();
    }

    public void SetNodes(IEnumerable<MeshCoreMapNode> nodes)
    {
        _nodes.Clear();
        foreach (var node in nodes) _nodes[node.Id] = node;
        if (_nodes.Count > 0 && _state.CenterLatitude == 0 && _state.CenterLongitude == 0)
        {
            var first = _nodes.Values.First();
            _state.CenterOn(first.Latitude, first.Longitude);
        }
        Render();
    }

    private void Render()
    {
        if (!IsInitialized || ActualWidth <= 0 || ActualHeight <= 0) return;
        MapCanvas.Children.Clear();
        foreach (var marker in MeshCoreMapCanvasRenderer.Render(_nodes.Values, _state.CenterLatitude, _state.CenterLongitude, _state.Zoom, ActualWidth, ActualHeight))
        {
            var ellipse = new Ellipse { Width = 18, Height = 18, Fill = BrushFor(marker.Type), Stroke = Brushes.White, StrokeThickness = 2, Tag = marker.Id, ToolTip = $"{marker.Label}\n{marker.Type}\n{marker.Latitude:F5}, {marker.Longitude:F5}" };
            ellipse.MouseLeftButtonDown += Marker_MouseLeftButtonDown;
            Canvas.SetLeft(ellipse, marker.X - 9);
            Canvas.SetTop(ellipse, marker.Y - 9);
            MapCanvas.Children.Add(ellipse);
        }
        MapStatusText.Text = $"{_nodes.Count} GPS node(s) · Zoom {_state.Zoom}";
    }

    private static Brush BrushFor(MeshCoreContactType type) => type switch
    {
        MeshCoreContactType.Repeater => Brushes.LimeGreen,
        MeshCoreContactType.RoomServer => Brushes.MediumPurple,
        _ => Brushes.DodgerBlue
    };

    private void Marker_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string id } && _nodes.TryGetValue(id, out var node))
        {
            NodeClicked?.Invoke(node);
            e.Handled = true;
        }
    }

    private void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragging = true;
        MapCanvas.CaptureMouse();
    }

    private void MapCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        MapCanvas.ReleaseMouseCapture();
    }

    private void MapCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var current = e.GetPosition(this);
        var delta = current - _dragStart;
        _dragStart = current;
        _state.PanPixels(delta.X, delta.Y, ActualWidth, ActualHeight);
        Render();
    }

    private void MapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta > 0) _state.ZoomIn(); else _state.ZoomOut();
        Render();
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) { _state.ZoomIn(); Render(); }
    private void ZoomOut_Click(object sender, RoutedEventArgs e) { _state.ZoomOut(); Render(); }
}
