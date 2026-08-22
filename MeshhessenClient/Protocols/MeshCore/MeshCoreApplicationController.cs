using MeshhessenClient.Services;

namespace MeshhessenClient.Protocols.MeshCore;

public sealed class MeshCoreApplicationController : IDisposable
{
    private readonly IConnectionService _connection;
    private readonly MeshCoreCompanionService _companion;
    private bool _disposed;

    public MeshCoreDeviceInfo? Device => _companion.DeviceInfo;
    public MeshCoreSelfInfo? Self => _companion.SelfInfo;
    public IReadOnlyList<MeshCoreContact> Contacts => _companion.Contacts;
    public MeshCoreNodeRegistry NodeRegistry { get; } = new();

    public event EventHandler<MeshCoreDeviceInfo>? DeviceChanged;
    public event EventHandler<MeshCoreSelfInfo>? SelfChanged;
    public event EventHandler<IReadOnlyList<MeshCoreContact>>? ContactsChanged;
    public event EventHandler<IReadOnlyList<MeshCoreNode>>? NodesChanged;
    public event EventHandler<MeshCoreChannel>? ChannelReceived;
    public event EventHandler<MeshCoreMessage>? MessageReceived;
    public event EventHandler<byte[]>? PushReceived;
    public event EventHandler<string>? Error;

    public MeshCoreApplicationController(IConnectionService connection, MeshCoreCompanionTransport transport)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _companion = new MeshCoreCompanionService(connection, transport);
        _companion.DeviceInfoReceived += (_, value) => DeviceChanged?.Invoke(this, value);
        _companion.SelfInfoReceived += (_, value) => SelfChanged?.Invoke(this, value);
        _companion.ContactsSynchronized += OnContactsSynchronized;
        _companion.ChannelReceived += (_, value) => ChannelReceived?.Invoke(this, value);
        _companion.MessageReceived += (_, value) => MessageReceived?.Invoke(this, value);
        _companion.PushReceived += (_, value) => PushReceived?.Invoke(this, value);
        _companion.ProtocolError += (_, value) => Error?.Invoke(this, value);
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => _companion.StartAsync(cancellationToken);
    public Task RefreshContactsAsync(CancellationToken cancellationToken = default) => _companion.RequestContactsAsync(cancellationToken: cancellationToken);

    public async Task RefreshChannelsAsync(int maxChannels = 8, CancellationToken cancellationToken = default)
    {
        if (maxChannels is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(maxChannels));
        for (byte index = 0; index < maxChannels; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _companion.RequestChannelAsync(index, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task SendChannelMessageAsync(byte channelIndex, string text, CancellationToken cancellationToken = default) => _companion.SendChannelMessageAsync(channelIndex, text, cancellationToken: cancellationToken);
    public Task SendDirectMessageAsync(byte[] destinationPublicKey, string text, CancellationToken cancellationToken = default) => _companion.SendDirectMessageAsync(destinationPublicKey, text, cancellationToken);
    public Task SyncNextMessageAsync(CancellationToken cancellationToken = default) => _companion.SyncNextMessageAsync(cancellationToken);

    private void OnContactsSynchronized(object? sender, IReadOnlyList<MeshCoreContact> contacts)
    {
        NodeRegistry.UpsertRange(contacts);
        ContactsChanged?.Invoke(this, contacts);
        NodesChanged?.Invoke(this, NodeRegistry.Snapshot());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _companion.Dispose();
    }
}
