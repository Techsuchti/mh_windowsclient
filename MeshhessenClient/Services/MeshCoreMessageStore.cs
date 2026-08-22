using System.Text.Json;
using MeshhessenClient.Protocols.MeshCore;

namespace MeshhessenClient.Services;

/// <summary>
/// Lightweight local JSON-backed history for the MeshCore-only client.
/// Keeps the core free of an additional database dependency while providing
/// durable channel/direct-message history.
/// </summary>
public sealed class MeshCoreMessageStore
{
    private readonly string _filePath;
    private readonly object _sync = new();
    private List<StoredMessage> _messages = new();

    public MeshCoreMessageStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MeshCoreWindowsClient", "messages.json");
        Load();
    }

    public IReadOnlyList<StoredMessage> GetAll()
    {
        lock (_sync) return _messages.OrderBy(x => x.Timestamp).ToArray();
    }

    public IReadOnlyList<StoredMessage> GetChannel(byte channelIndex)
    {
        lock (_sync) return _messages.Where(x => !x.IsDirect && x.ChannelIndex == channelIndex)
            .OrderBy(x => x.Timestamp).ToArray();
    }

    public IReadOnlyList<StoredMessage> GetDirect(string publicKeyPrefix)
    {
        lock (_sync) return _messages.Where(x => x.IsDirect &&
            string.Equals(x.PeerKeyPrefix, publicKeyPrefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Timestamp).ToArray();
    }

    public void Add(MeshCoreMessage message, bool outgoing = false, string? peerKeyPrefix = null)
    {
        var item = new StoredMessage(
            message.Timestamp == 0 ? (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds() : message.Timestamp,
            message.ChannelIndex,
            message.IsDirectMessage,
            peerKeyPrefix ?? (message.SenderPublicKeyPrefix is null ? null : Convert.ToHexString(message.SenderPublicKeyPrefix)),
            message.Text,
            outgoing);

        lock (_sync)
        {
            _messages.Add(item);
            if (_messages.Count > 5000) _messages = _messages.Skip(_messages.Count - 5000).ToList();
            SaveLocked();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = File.ReadAllText(_filePath);
            _messages = JsonSerializer.Deserialize<List<StoredMessage>>(json) ?? new();
        }
        catch
        {
            _messages = new();
        }
    }

    private void SaveLocked()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(_messages, new JsonSerializerOptions { WriteIndented = true });
        var temp = _filePath + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, _filePath, true);
    }

    public sealed record StoredMessage(
        uint Timestamp,
        byte? ChannelIndex,
        bool IsDirect,
        string? PeerKeyPrefix,
        string Text,
        bool Outgoing);
}
