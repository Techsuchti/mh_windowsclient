namespace MeshhessenClient.Protocols.MeshCore;

/// <summary>
/// Coordinates the MeshCore Companion startup sequence without coupling the UI
/// to protocol timing. The current Companion specification requires commands to
/// be issued sequentially and recommends waiting for the matching response.
/// </summary>
public sealed class MeshCoreStartupCoordinator
{
    private readonly MeshCoreApplicationController _client;
    private readonly TimeSpan _timeout;
    private int _started;

    public MeshCoreStartupCoordinator(MeshCoreApplicationController client, TimeSpan? timeout = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _timeout = timeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            return;

        try
        {
            // APP_START is emitted by MeshCoreApplicationController.StartAsync().
            // SELF_INFO is the protocol acknowledgement and the controller then
            // requests DEVICE_INFO. We wait for DEVICE_CHANGED before proceeding.
            await StartAndWaitForDeviceAsync(cancellationToken).ConfigureAwait(false);

            // Contacts are fetched as one response stream. Wait for the complete
            // synchronization before touching channel slots.
            await WaitForContactsAsync(cancellationToken).ConfigureAwait(false);

            // Channel slots are independent requests, but the Companion protocol
            // still requires only one command in flight. The controller performs
            // these requests sequentially.
            await _client.RefreshChannelsAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            // The firmware can already have queued messages. Drain them one at a
            // time; PUSH_CODE_MSG_WAITING will trigger additional synchronization.
            await _client.SyncNextMessageAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Interlocked.Exchange(ref _started, 0);
            throw;
        }
    }

    private async Task StartAndWaitForDeviceAsync(CancellationToken cancellationToken)
    {
        var tcs = NewSignal<MeshCoreDeviceInfo>();
        EventHandler<MeshCoreDeviceInfo>? handler = (_, value) => tcs.TrySetResult(value);
        _client.DeviceChanged += handler;
        try
        {
            await _client.StartAsync(cancellationToken).ConfigureAwait(false);
            await WaitAsync(tcs.Task, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _client.DeviceChanged -= handler;
        }
    }

    private async Task WaitForContactsAsync(CancellationToken cancellationToken)
    {
        var tcs = NewSignal<IReadOnlyList<MeshCoreContact>>();
        EventHandler<IReadOnlyList<MeshCoreContact>>? handler = (_, value) => tcs.TrySetResult(value);
        _client.ContactsChanged += handler;
        try
        {
            await _client.RefreshContactsAsync(cancellationToken).ConfigureAwait(false);
            await WaitAsync(tcs.Task, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _client.ContactsChanged -= handler;
        }
    }

    private async Task<T> WaitAsync<T>(Task<T> task, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        return await task.WaitAsync(timeout.Token).ConfigureAwait(false);
    }

    private static TaskCompletionSource<T> NewSignal<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
