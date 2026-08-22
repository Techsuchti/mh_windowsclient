using System.Net.Sockets;

namespace MeshhessenClient.Services;

/// <summary>Raw TCP transport for MeshCore Companion stream connections.</summary>
public sealed class MeshCoreTcpConnectionService : IConnectionService
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private string _displayName = string.Empty;

    public ConnectionType Type => ConnectionType.Tcp;
    public string DisplayName => _displayName;
    public bool IsConnected => _client?.Connected == true && _stream != null;

    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<byte[]>? DataReceived;

    public async Task ConnectAsync(ConnectionParameters parameters)
    {
        if (parameters is not TcpConnectionParameters tcp)
            throw new ArgumentException("Invalid TCP parameters.", nameof(parameters));
        if (string.IsNullOrWhiteSpace(tcp.Hostname))
            throw new ArgumentException("Hostname cannot be empty.", nameof(parameters));
        if (tcp.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(tcp.Port));

        Disconnect();
        _displayName = $"{tcp.Hostname}:{tcp.Port}";
        _client = new TcpClient { NoDelay = true };
        await _client.ConnectAsync(tcp.Hostname, tcp.Port);
        _stream = _client.GetStream();
        _cts = new CancellationTokenSource();
        ConnectionStateChanged?.Invoke(this, true);
        _ = ReceiveLoopAsync(_cts.Token);
    }

    public async Task WriteAsync(byte[] data)
    {
        if (_stream == null || !IsConnected)
            throw new InvalidOperationException("MeshCore TCP is not connected.");
        await _stream.WriteAsync(data, CancellationToken.None);
        await _stream.FlushAsync(CancellationToken.None);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        try
        {
            while (!cancellationToken.IsCancellationRequested && _stream != null)
            {
                var count = await _stream.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (count == 0) break;
                DataReceived?.Invoke(this, buffer.AsSpan(0, count).ToArray());
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            Logger.WriteLine($"[MeshCore TCP] Receive error: {ex.Message}");
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
                ConnectionStateChanged?.Invoke(this, false);
        }
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _stream?.Dispose();
        _stream = null;
        _client?.Dispose();
        _client = null;
    }

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
