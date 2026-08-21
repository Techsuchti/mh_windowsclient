using System.Buffers.Binary;
using System.Text;
using MeshhessenClient.Services;

namespace MeshhessenClient.Protocols.MeshCore;

public sealed class MeshCoreCompanionService : IDisposable
{
    private readonly IConnectionService _connection;
    private readonly MeshCoreCompanionTransport _transport;
    private readonly MeshCoreCompanionFrameCodec _codec = new();
    private readonly List<MeshCoreContact> _contacts = new();
    private bool _started;

    public MeshCoreDeviceInfo? DeviceInfo { get; private set; }
    public MeshCoreSelfInfo? SelfInfo { get; private set; }
    public IReadOnlyList<MeshCoreContact> Contacts => _contacts;
    public event EventHandler<MeshCoreDeviceInfo>? DeviceInfoReceived;
    public event EventHandler<MeshCoreSelfInfo>? SelfInfoReceived;
    public event EventHandler<IReadOnlyList<MeshCoreContact>>? ContactsSynchronized;
    public event EventHandler<MeshCoreChannel>? ChannelReceived;
    public event EventHandler<MeshCoreMessage>? MessageReceived;
    public event EventHandler<byte[]>? PushReceived;
    public event EventHandler<string>? ProtocolError;

    public MeshCoreCompanionService(IConnectionService connection, MeshCoreCompanionTransport transport)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _transport = transport;
        _connection.DataReceived += OnDataReceived;
        _connection.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_connection.IsConnected) throw new InvalidOperationException("The MeshCore connection is not established.");
        if (_started) return;
        _started = true;
        var appName = Encoding.UTF8.GetBytes("MeshCore Windows Client");
        var payload = new byte[8 + appName.Length];
        payload[0] = MeshCoreProtocolConstants.CmdAppStart;
        appName.CopyTo(payload, 8);
        await SendPayloadAsync(payload, cancellationToken);
    }

    public Task RequestDeviceInfoAsync(CancellationToken cancellationToken = default) => SendPayloadAsync(new byte[] { MeshCoreProtocolConstants.CmdDeviceQuery, 3 }, cancellationToken);

    public Task RequestContactsAsync(uint? since = null, CancellationToken cancellationToken = default)
    {
        var payload = since.HasValue ? new byte[5] : new byte[1];
        payload[0] = MeshCoreProtocolConstants.CmdGetContacts;
        if (since.HasValue) BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(1, 4), since.Value);
        return SendPayloadAsync(payload, cancellationToken);
    }

    public Task RequestChannelAsync(byte channelIndex, CancellationToken cancellationToken = default) => SendPayloadAsync(new[] { MeshCoreProtocolConstants.CmdGetChannel, channelIndex }, cancellationToken);

    public async Task SendChannelMessageAsync(byte channelIndex, string text, DateTimeOffset? timestamp = null, CancellationToken cancellationToken = default)
    {
        ValidateText(text);
        var bytes = Encoding.UTF8.GetBytes(text);
        if (bytes.Length > 133) throw new ArgumentException("MeshCore channel messages are limited to 133 UTF-8 bytes.", nameof(text));
        var payload = new byte[7 + bytes.Length];
        payload[0] = MeshCoreProtocolConstants.CmdSendChannelTextMessage;
        payload[1] = 0;
        payload[2] = channelIndex;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(3, 4), checked((uint)(timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds()));
        bytes.CopyTo(payload, 7);
        await SendPayloadAsync(payload, cancellationToken);
    }

    public async Task SendDirectMessageAsync(ReadOnlySpan<byte> destinationPublicKey, string text, CancellationToken cancellationToken = default)
    {
        if (destinationPublicKey.Length != 32) throw new ArgumentException("MeshCore public keys are 32 bytes.", nameof(destinationPublicKey));
        ValidateText(text);
        var bytes = Encoding.UTF8.GetBytes(text);
        if (bytes.Length > 133) throw new ArgumentException("MeshCore direct messages are limited to 133 UTF-8 bytes.", nameof(text));
        var payload = new byte[34 + bytes.Length];
        payload[0] = MeshCoreProtocolConstants.CmdSendTextMessage;
        destinationPublicKey.CopyTo(payload.AsSpan(1, 32));
        payload[33] = 0;
        bytes.CopyTo(payload, 34);
        await SendPayloadAsync(payload, cancellationToken);
    }

    public Task SyncNextMessageAsync(CancellationToken cancellationToken = default) => SendPayloadAsync(new[] { MeshCoreProtocolConstants.CmdSyncNextMessage }, cancellationToken);

    private async Task SendPayloadAsync(byte[] payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_connection.IsConnected) throw new InvalidOperationException("The MeshCore connection is not established.");
        await _connection.WriteAsync(_codec.EncodeCommand(payload, _transport));
    }

    private void OnConnectionStateChanged(object? sender, bool connected) { if (!connected) _started = false; }

    private void OnDataReceived(object? sender, byte[] data)
    {
        try { foreach (var payload in _codec.Feed(data, _transport)) ParsePayload(payload); }
        catch (Exception ex) { ProtocolError?.Invoke(this, ex.Message); }
    }

    private void ParsePayload(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0) return;
        switch (data[0])
        {
            case MeshCoreProtocolConstants.RespSelfInfo: ParseSelfInfo(data); break;
            case MeshCoreProtocolConstants.RespDeviceInfo: ParseDeviceInfo(data); break;
            case MeshCoreProtocolConstants.RespContactsStart: _contacts.Clear(); break;
            case MeshCoreProtocolConstants.RespContact: ParseContact(data); break;
            case MeshCoreProtocolConstants.RespEndOfContacts: ContactsSynchronized?.Invoke(this, _contacts.ToArray()); break;
            case MeshCoreProtocolConstants.RespChannelInfo: ParseChannel(data); break;
            case MeshCoreProtocolConstants.RespContactMessage:
            case MeshCoreProtocolConstants.RespContactMessageV3:
            case MeshCoreProtocolConstants.RespChannelMessage:
            case MeshCoreProtocolConstants.RespChannelMessageV3: ParseMessage(data); break;
            case MeshCoreProtocolConstants.PushMessageWaiting: PushReceived?.Invoke(this, data.ToArray()); _ = SyncNextMessageAsync(); break;
            case MeshCoreProtocolConstants.RespError: ProtocolError?.Invoke(this, data.Length > 1 ? $"MeshCore error code {data[1]}" : "MeshCore returned an error."); break;
            default: PushReceived?.Invoke(this, data.ToArray()); break;
        }
    }

    private void ParseSelfInfo(ReadOnlySpan<byte> data)
    {
        if (data.Length < 58) throw new InvalidDataException("SELF_INFO packet is too short.");
        var info = new MeshCoreSelfInfo((MeshCoreContactType)data[1], data[2], data[3], data.Slice(4, 32).ToArray(), BinaryPrimitives.ReadInt32LittleEndian(data.Slice(36, 4)) / 1_000_000d, BinaryPrimitives.ReadInt32LittleEndian(data.Slice(40, 4)) / 1_000_000d, BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(48, 4)) / 1000d, BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(52, 4)) / 1000d, data[56], data[57], ReadUtf8(data.Slice(58)));
        SelfInfo = info;
        SelfInfoReceived?.Invoke(this, info);
        _ = RequestDeviceInfoAsync();
    }

    private void ParseDeviceInfo(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2) throw new InvalidDataException("DEVICE_INFO packet is too short.");
        var fw = data[1];
        var maxContacts = data.Length > 2 ? (byte)(data[2] * 2) : (byte)0;
        var maxChannels = data.Length > 3 ? data[3] : (byte)0;
        var pin = data.Length >= 8 ? BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4)) : 0;
        var build = data.Length >= 20 ? ReadFixedUtf8(data.Slice(8, 12)) : string.Empty;
        var model = data.Length >= 60 ? ReadFixedUtf8(data.Slice(20, 40)) : string.Empty;
        var version = data.Length >= 80 ? ReadFixedUtf8(data.Slice(60, 20)) : string.Empty;
        DeviceInfo = new MeshCoreDeviceInfo(fw, maxContacts, maxChannels, pin, build, model, version);
        DeviceInfoReceived?.Invoke(this, DeviceInfo);
    }

    private void ParseContact(ReadOnlySpan<byte> data)
    {
        if (data.Length < 148) throw new InvalidDataException("CONTACT packet is too short.");
        _contacts.Add(new MeshCoreContact(data.Slice(1, 32).ToArray(), (MeshCoreContactType)data[33], data[34], unchecked((sbyte)data[35]), data.Slice(36, 64).ToArray(), ReadFixedUtf8(data.Slice(100, 32)), BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(132, 4)), BinaryPrimitives.ReadInt32LittleEndian(data.Slice(136, 4)) / 1_000_000d, BinaryPrimitives.ReadInt32LittleEndian(data.Slice(140, 4)) / 1_000_000d));
    }

    private void ParseChannel(ReadOnlySpan<byte> data)
    {
        if (data.Length < 50) throw new InvalidDataException("CHANNEL_INFO packet is too short.");
        ChannelReceived?.Invoke(this, new MeshCoreChannel(data[1], ReadFixedUtf8(data.Slice(2, 32)), data.Slice(34, 16).ToArray()));
    }

    private void ParseMessage(ReadOnlySpan<byte> data)
    {
        var v3 = data[0] is MeshCoreProtocolConstants.RespContactMessageV3 or MeshCoreProtocolConstants.RespChannelMessageV3;
        var offset = v3 ? 4 : 1;
        int? snr = v3 ? unchecked((sbyte)data[1]) / 4 : null;
        byte? channel = null;
        if (data[0] is MeshCoreProtocolConstants.RespChannelMessage or MeshCoreProtocolConstants.RespChannelMessageV3)
        {
            if (data.Length < offset + 7) throw new InvalidDataException("CHANNEL_MSG packet is too short.");
            channel = data[offset++];
        }
        else
        {
            if (data.Length < offset + 10) throw new InvalidDataException("CONTACT_MSG packet is too short.");
            offset += 3;
        }
        offset += 2;
        var timestamp = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, 4));
        offset += 4;
        MessageReceived?.Invoke(this, new MeshCoreMessage(null, channel, ReadUtf8(data.Slice(offset)), timestamp, snr, channel is null));
    }

    private static void ValidateText(string text) { if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Message text must not be empty.", nameof(text)); }
    private static string ReadFixedUtf8(ReadOnlySpan<byte> data) => ReadUtf8(data).Trim();
    private static string ReadUtf8(ReadOnlySpan<byte> data) { var end = data.IndexOf((byte)0); if (end < 0) end = data.Length; return Encoding.UTF8.GetString(data[..end]); }
    public void Dispose() { _connection.DataReceived -= OnDataReceived; _connection.ConnectionStateChanged -= OnConnectionStateChanged; }
}
