using MeshhessenClient.Protocols.MeshCore;

namespace MeshhessenClient.Services;

/// <summary>Coordinates the MeshCore application controller with automatic transport recovery.</summary>
public sealed class MeshCoreSupervisedApplicationController : IAsyncDisposable
{
    private readonly MeshCoreApplicationController _controller;
    private readonly MeshCoreConnectionSupervisor _supervisor;

    public MeshCoreApplicationController Controller => _controller;
    public bool IsConnected => _supervisor.IsRunning && _controller.NodeRegistry.Nodes.Count >= 0;

    public event EventHandler<bool>? ConnectionStateChanged;

    public MeshCoreSupervisedApplicationController(
        MeshCoreApplicationController controller,
        IConnectionService connection,
        Func<Task> connect,
        TimeSpan? retryDelay = null)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _supervisor = new MeshCoreConnectionSupervisor(connection, connect, retryDelay);
        _supervisor.ConnectionStateChanged += (_, connected) => ConnectionStateChanged?.Invoke(this, connected);
    }

    public Task StartAsync()
    {
        _supervisor.Start();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _supervisor.DisposeAsync().ConfigureAwait(false);
        _controller.Dispose();
    }
}
