using System.Net.Http;

namespace MeshhessenClient.Protocols.MeshCore;

public sealed class MeshCoreMapTileCache : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly SemaphoreSlim _gate = new(4, 4);

    public MeshCoreMapTileCache(string? cacheDirectory = null)
    {
        _cacheDirectory = cacheDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeshhessenClient", "MapTiles");
        Directory.CreateDirectory(_cacheDirectory);
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MeshhessenClient/1.0 (OpenStreetMap tile client)");
    }

    public async Task<byte[]?> GetAsync(MeshCoreMapTile tile, CancellationToken cancellationToken = default)
    {
        var path = GetPath(tile);
        if (File.Exists(path)) return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(path)) return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            var url = MeshCoreMapTileUrlProvider.GetUrl(tile.Zoom, tile.X, tile.Y);
            var data = await _httpClient.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(path, data, cancellationToken).ConfigureAwait(false);
            return data;
        }
        catch (HttpRequestException) { return null; }
        finally { _gate.Release(); }
    }

    private string GetPath(MeshCoreMapTile tile)
    {
        var zoom = Path.Combine(_cacheDirectory, tile.Zoom.ToString());
        var x = Path.Combine(zoom, tile.X.ToString());
        Directory.CreateDirectory(x);
        return Path.Combine(x, $"{tile.Y}.png");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _gate.Dispose();
    }
}
