namespace MeshhessenClient.Protocols.MeshCore;

/// <summary>
/// Builds standard OpenStreetMap tile URLs for the map UI.
/// Kept UI-neutral so the WPF layer can use any tile renderer.
/// </summary>
public static class MeshCoreMapTileUrlProvider
{
    public static string GetUrl(int zoom, int tileX, int tileY)
    {
        if (zoom is < 0 or > 19) throw new ArgumentOutOfRangeException(nameof(zoom));
        var max = (1 << zoom) - 1;
        if (tileX < 0 || tileX > max) throw new ArgumentOutOfRangeException(nameof(tileX));
        if (tileY < 0 || tileY > max) throw new ArgumentOutOfRangeException(nameof(tileY));
        return $"https://tile.openstreetmap.org/{zoom}/{tileX}/{tileY}.png";
    }

    public static (int X, int Y) CoordinateToTile(double latitude, double longitude, int zoom)
    {
        if (latitude is < -85.05112878 or > 85.05112878) throw new ArgumentOutOfRangeException(nameof(latitude));
        if (longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(longitude));
        if (zoom is < 0 or > 19) throw new ArgumentOutOfRangeException(nameof(zoom));

        var n = 1 << zoom;
        var x = (int)Math.Floor((longitude + 180.0) / 360.0 * n);
        var latRad = latitude * Math.PI / 180.0;
        var y = (int)Math.Floor((1.0 - Math.Asinh(Math.Tan(latRad)) / Math.PI) / 2.0 * n);
        return (Math.Clamp(x, 0, n - 1), Math.Clamp(y, 0, n - 1));
    }
}
