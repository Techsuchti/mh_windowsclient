namespace MeshhessenClient.Protocols.MeshCore;

public readonly record struct MeshCoreMapTile(int Zoom, int X, int Y);

public static class MeshCoreMapTileMath
{
    public static IReadOnlyList<MeshCoreMapTile> GetVisibleTiles(double latitude, double longitude, int zoom, int radius = 1)
    {
        if (radius is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(radius));
        var center = MeshCoreMapTileUrlProvider.CoordinateToTile(latitude, longitude, zoom);
        var n = 1 << zoom;
        var tiles = new List<MeshCoreMapTile>();
        for (var dy = -radius; dy <= radius; dy++)
        for (var dx = -radius; dx <= radius; dx++)
        {
            var x = Mod(center.X + dx, n);
            var y = center.Y + dy;
            if (y >= 0 && y < n) tiles.Add(new MeshCoreMapTile(zoom, x, y));
        }
        return tiles;
    }

    private static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;
}
