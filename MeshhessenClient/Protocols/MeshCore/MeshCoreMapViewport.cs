namespace MeshhessenClient.Protocols.MeshCore;

public readonly record struct MeshCoreMapPoint(double X, double Y);

public static class MeshCoreMapViewport
{
    public static MeshCoreMapPoint Project(double latitude, double longitude, double centerLatitude, double centerLongitude, int zoom, double width, double height)
    {
        if (zoom is < 0 or > 19) throw new ArgumentOutOfRangeException(nameof(zoom));
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));

        var scale = 256.0 * Math.Pow(2, zoom);
        var center = WorldPixel(centerLatitude, centerLongitude, scale);
        var point = WorldPixel(latitude, longitude, scale);
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        var world = scale;
        if (dx > world / 2) dx -= world;
        if (dx < -world / 2) dx += world;
        return new MeshCoreMapPoint(width / 2 + dx, height / 2 + dy);
    }

    private static MeshCoreMapPoint WorldPixel(double latitude, double longitude, double scale)
    {
        latitude = Math.Clamp(latitude, -85.05112878, 85.05112878);
        var x = (longitude + 180.0) / 360.0 * scale;
        var latRad = latitude * Math.PI / 180.0;
        var y = (1.0 - Math.Asinh(Math.Tan(latRad)) / Math.PI) / 2.0 * scale;
        return new MeshCoreMapPoint(x, y);
    }
}
