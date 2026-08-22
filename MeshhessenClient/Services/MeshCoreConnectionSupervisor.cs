namespace MeshhessenClient.Services;

/// <summary>Supervises a MeshCore transport and retries transient connection failures.</summary>
public sealed class MeshCoreConnectionSupervisor : IAsyncDisposable
{
    private readonly IConnectionService _connection;
    private readonly Func<Task> _connect;
    private readonly TimeSpan _retryDelay;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public MeshCoreConnectionSupervisor(IConnectionService connection, Func<Task> connect, TimeSpan? retryDelay = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _connect = connect ?? throw new ArgumentNullException(nameof(connect));
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(3);
    }

    public bool IsRunning => _loop is { IsCompleted: false };

    public event EventHandler<bool>? ConnectionStateChanged;

    public void Start()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        _connection.ConnectionStateChanged += OnConnectionStateChanged;
        _loop = Task.Run(() => RunAsync(_cts.Token));
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        if (_loop is not null)
        {
            try { await _loop.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }
        _connection.ConnectionStateChanged -= OnConnectionStateChanged;
        _cts?.Dispose();
        _gate.Dispose();
    }

    public void Stop()
    {
        _cts?.Cancel();
        if (_connection.IsConnected) _connection.Disconnect();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_connection.IsConnected)
            {
                try
                {
                    await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        if (!_connection.IsConnected)
                            await _connect().ConfigureAwait(false);
                    }
                    finally { _gate.Release(); }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                catch { }
            }

            try { await Task.Delay(_retryDelay, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
        }
    }

    private void OnConnectionStateChanged(object? sender, bool connected) => ConnectionStateChanged?.Invoke(this, connected);
}
