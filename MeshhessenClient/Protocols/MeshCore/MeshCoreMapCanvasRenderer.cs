namespace MeshhessenClient.Protocols.MeshCore;

public sealed record MeshCoreMapMarker(
    string Id,
    string Label,
    MeshCoreContactType Type,
    double X,
    double Y,
    double Latitude,
    double Longitude);

/// <summary>
/// Produces render-ready marker coordinates for a WPF Canvas.
/// The actual WPF controls remain in the UI project/code-behind.
/// </summary>
public static class MeshCoreMapCanvasRenderer
{
    public static IReadOnlyList<MeshCoreMapMarker> Render(
        IEnumerable<MeshCoreMapNode> nodes,
        double centerLatitude,
        double centerLongitude,
        int zoom,
        double width,
        double height)
    {
        return nodes.Select(node =>
        {
            var point = MeshCoreMapViewport.Project(
                node.Latitude,
                node.Longitude,
                centerLatitude,
                centerLongitude,
                zoom,
                width,
                height);

            return new MeshCoreMapMarker(
                node.Id,
                node.Name,
                node.Type,
                point.X,
                point.Y,
                node.Latitude,
                node.Longitude);
        }).ToArray();
    }
}
