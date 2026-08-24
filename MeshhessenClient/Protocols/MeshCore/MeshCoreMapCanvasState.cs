namespace MeshhessenClient.Protocols.MeshCore;

public sealed class MeshCoreMapCanvasState
{
    public double CenterLatitude { get; private set; }
    public double CenterLongitude { get; private set; }
    public int Zoom { get; private set; } = 12;

    public void CenterOn(double latitude, double longitude)
    {
        CenterLatitude = Math.Clamp(latitude, -85.05112878, 85.05112878);
        CenterLongitude = NormalizeLongitude(longitude);
    }

    public void ZoomIn() => Zoom = Math.Min(19, Zoom + 1);
    public void ZoomOut() => Zoom = Math.Max(1, Zoom - 1);

    public void PanPixels(double deltaX, double deltaY, double viewportWidth, double viewportHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0) return;
        var scale = 256.0 * Math.Pow(2, Zoom);
        var center = WorldPixel(CenterLatitude, CenterLongitude, scale);
        var target = new MeshCoreMapPoint(center.X - deltaX, center.Y - deltaY);
        var longitude = target.X / scale * 360.0 - 180.0;
        var mercator = Math.PI * (1.0 - 2.0 * target.Y / scale);
        var latitude = 180.0 / Math.PI * Math.Atan(Math.Sinh(mercator));
        CenterOn(latitude, longitude);
    }

    private static MeshCoreMapPoint WorldPixel(double latitude, double longitude, double scale)
    {
        latitude = Math.Clamp(latitude, -85.05112878, 85.05112878);
        var x = (longitude + 180.0) / 360.0 * scale;
        var latRad = latitude * Math.PI / 180.0;
        var y = (1.0 - Math.Asinh(Math.Tan(latRad)) / Math.PI) / 2.0 * scale;
        return new MeshCoreMapPoint(x, y);
    }

    private static double NormalizeLongitude(double longitude)
    {
        while (longitude < -180) longitude += 360;
        while (longitude > 180) longitude -= 360;
        return longitude;
    }
}
