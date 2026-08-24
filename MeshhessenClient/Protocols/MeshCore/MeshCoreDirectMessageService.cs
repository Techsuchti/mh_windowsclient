using MeshhessenClient.Services;

namespace MeshhessenClient.Protocols.MeshCore;

/// <summary>
/// Direct-message orchestration kept separate from the WPF layer.
/// The actual Companion command encoding is delegated to the application controller.
/// </summary>
public sealed class MeshCoreDirectMessageService
{
    private readonly MeshCoreApplicationController _client;
    private readonly MeshCoreMessageStore _store;

    public MeshCoreDirectMessageService(MeshCoreApplicationController client, MeshCoreMessageStore store)
    {
        _client = client;
        _store = store;
    }

    public IReadOnlyList<MeshCoreMessageStore.StoredMessage> GetHistory(string peerKeyPrefix)
        => _store.GetDirect(peerKeyPrefix);

    public void RecordIncoming(MeshCoreMessage message, string peerKeyPrefix)
        => _store.Add(message, outgoing: false, peerKeyPrefix);

    public void RecordOutgoing(string text, string peerKeyPrefix)
    {
        var message = new MeshCoreMessage(
            SenderPublicKeyPrefix: null,
            ChannelIndex: null,
            Text: text,
            Timestamp: (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            SnrDb: null,
            IsDirectMessage: true);
        _store.Add(message, outgoing: true, peerKeyPrefix);
    }
}
