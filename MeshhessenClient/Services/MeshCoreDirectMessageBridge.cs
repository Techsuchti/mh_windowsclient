using MeshhessenClient.Protocols.MeshCore;

namespace MeshhessenClient.Services;

/// <summary>Connects MeshCore DM events with the local conversation store and exposes outgoing DMs.</summary>
public sealed class MeshCoreDirectMessageBridge : IDisposable
{
    private readonly MeshCoreCompanionService _companion;
    private readonly MeshCoreMessageStore _store;

    public event EventHandler<MeshCoreMessage>? MessageReceived;

    public MeshCoreDirectMessageBridge(MeshCoreCompanionService companion, MeshCoreMessageStore store)
    {
        _companion = companion ?? throw new ArgumentNullException(nameof(companion));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _companion.MessageReceived += OnMessageReceived;
    }

    public IReadOnlyList<MeshCoreMessageStore.StoredMessage> GetConversation(string peerKeyPrefix)
        => _store.GetDirect(peerKeyPrefix);

    public async Task SendAsync(byte[] destinationPublicKey, string text, CancellationToken cancellationToken = default)
    {
        if (destinationPublicKey.Length != 32)
            throw new ArgumentException("MeshCore public keys must contain 32 bytes.", nameof(destinationPublicKey));

        await _companion.SendDirectMessageAsync(destinationPublicKey, text, cancellationToken);
        _store.Add(new MeshCoreMessage(null, null, text,
            (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(), null, true, 0, 0),
            outgoing: true,
            peerKeyPrefix: Convert.ToHexString(destinationPublicKey));
    }

    private void OnMessageReceived(object? sender, MeshCoreMessage message)
    {
        if (!message.IsDirectMessage) return;

        var peer = message.SenderPublicKeyPrefix is null
            ? null
            : Convert.ToHexString(message.SenderPublicKeyPrefix);
        _store.Add(message, outgoing: false, peerKeyPrefix: peer);
        MessageReceived?.Invoke(this, message);
    }

    public void Dispose() => _companion.MessageReceived -= OnMessageReceived;
}
