using System.Security.Cryptography;
using MeshhessenClient.Models;
using MeshhessenClient.Protocols.MeshCore;

namespace MeshhessenClient.Services;

/// <summary>Adapts MeshCore direct-message events to the existing WPF DM window model.</summary>
public sealed class MeshCoreDirectMessagesUiAdapter : IDisposable
{
    private readonly MeshCoreDirectMessageBridge _bridge;
    private readonly DirectMessagesWindow _window;

    public MeshCoreDirectMessagesUiAdapter(MeshCoreDirectMessageBridge bridge, DirectMessagesWindow window)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _bridge.MessageReceived += OnMessageReceived;
    }

    private void OnMessageReceived(object? sender, MeshCoreMessage message)
    {
        if (!message.IsDirectMessage) return;

        var peer = message.SenderPublicKeyPrefix ?? Array.Empty<byte>();
        var peerId = StableNodeId(peer);
        var now = DateTime.Now;
        var item = new MessageItem
        {
            Id = StableMessageId(message),
            Time = now.ToString("HH:mm"),
            SortTime = now,
            From = peer.Length == 0 ? "MeshCore" : Convert.ToHexString(peer),
            FromId = peerId,
            ToId = 0,
            Message = message.Text,
            ChannelIndex = 0,
            IsViaMqtt = false,
            IsEncrypted = false
        };

        _window.AddOrUpdateMessage(item);
    }

    private static uint StableNodeId(byte[] key)
    {
        if (key.Length == 0) return 0x4D434F52; // "MCOR"
        var hash = SHA256.HashData(key);
        return BitConverter.ToUInt32(hash, 0);
    }

    private static uint StableMessageId(MeshCoreMessage message)
    {
        var text = message.Text ?? string.Empty;
        var bytes = System.Text.Encoding.UTF8.GetBytes($"{message.Timestamp}:{text}");
        var hash = SHA256.HashData(bytes);
        return BitConverter.ToUInt32(hash, 0);
    }

    public void Dispose() => _bridge.MessageReceived -= OnMessageReceived;
}
