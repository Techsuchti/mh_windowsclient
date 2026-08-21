using System.Buffers.Binary;
using System.Text;
using MeshhessenClient.Services;

namespace MeshhessenClient.Protocols.MeshCore;

/// <summary>
/// MeshCore Companion Protocol runtime. Transport-specific connection code
/// remains in IConnectionService.
/// </summary>
public sealed class MeshCoreCompanionService : IDisposable
{
    private readonly IConnectionService _connection;
    private readonly MeshCoreCompanionTransport _transport;
    private readonly MeshCoreCompanionFrameCodec _codec = new();
    private bool _started;
    private readonly List<MeshCoreContact> _contacts = new();

    public MeshCoreDeviceInfo? DeviceInfo { get; private set; }
    public MeshCoreSelfInfo? SelfInfo { get; private set; }
    public IReadOnlyList<MeshCoreContact> Contacts => _contacts;

    public event EventHandler<MeshCoreDeviceInfo>? DeviceInfoReceived;
    public event EventHandler<MeshCoreSelfInfo>? SelfInfoReceived;
    public event EventHandler<IReadOnlyList<MeshCoreContact>>? ContactsSynchronized;
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
        if (!_connection.IsConnected)
            throw new InvalidOperationException("The MeshCore connection is not established.");
        if (_started)
            return;

        _started = true;
        var appName = Encoding.UTF8.GetBytes("Meshhessen MeshCore Client");
        var payload = new byte[8 + appName.Length];
        payload[0] = MeshCoreProtocolConstants.CmdAppStart;
        payload[1] = 1;
        appName.CopyTo(payload, 8);
        await SendPayloadAsync(payload, cancellationToken);
    }

    public Task RequestContactsAsync(CancellationToken cancellationToken = default) =>
        SendPayloadAsync(new[] { MeshCoreProtocolConstants.CmdGetContacts }, cancellationToken);

    public Task RequestDeviceInfoAsync(CancellationToken cancellationToken = default) =>
        SendPayloadAsync(new byte[] { MeshCoreProtocolConstants.CmdDeviceQuery, 1 }, cancellationToken);

    public async Task SendChannelMessageAsync(byte channelIndex, string text, DateTimeOffset? timestamp = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("Message text must not be empty.", nameof(text));

        var textBytes = Encoding.UTF8.GetBytes(text);
        var payload = new byte[7 + textBytes.Length];
        payload[0] = MeshCoreProtocolConstants.CmdSendChannelTextMessage;
        payload[1] = 0;
        payload[2] = channelIndex;
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(3, 4), checked((uint)(timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds()));
        textBytes.CopyTo(payload, 7);
        await SendPayloadAsync(payload, cancellationToken);
    }

    public async Task SendDirectMessageAsync(ReadOnlySpan<byte> destinationPublicKey, string text, CancellationToken cancellationToken = default)
    {
        if (destinationPublicKey.Length != 32)
            throw new ArgumentException("MeshCore public keys are 32 bytes.", nameof(destinationPublicKey));
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("Message text must not be empty.", nameof(text));

        var textBytes = Encoding.UTF8.GetBytes(text);
        var payload = new byte[33 + textBytes.Length];
        payload[0] = MeshCoreProtocolConstants.CmdSendTextMessage;
        destinationPublicKey.CopyTo(payload.AsSpan(1, 32));
        textBytes.CopyTo(payload, 33);
        await SendPayloadAsync(payload, cancellationToken);
    }

    private async Task SendPayloadAsync(byte[] payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _connection.WriteAsync(_codec.EncodeCommand(payload, _transport));
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        if (!connected)
            _started = false;
    }

    private void OnDataReceived(object? sender, byte[] data)
    {
        try
        {
            foreach (var payload in _codec.Feed(data, _transport))
                ParsePayload(payload);
        }
        catch (Exception ex)
        {
            ProtocolError?.Invoke(this, ex.Message);
        }
    }

    private void ParsePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length == 0)
            return;

        switch (payload[0])
        {
            case MeshCoreProtocolConstants.RespSelfInfo: ParseSelfInfo(payload); break;
            case MeshCoreProtocolConstants.RespDeviceInfo: ParseDeviceInfo(payload); break;
            case MeshCoreProtocolConstants.RespContactsStart:
                _contacts.Clear();
                ContactsSynchronized?.Invoke(this, _contacts.ToArray());
                break;
            case MeshCoreProtocolConstants.RespContact: ParseContact(payload); break;
            case MeshCoreProtocolConstants.RespContactMessage:
            case MeshCoreProtocolConstants.RespContactMessageV3:
            case MeshCoreProtocolConstants.RespChannelMessage:
            case MeshCoreProtocolConstants.RespChannelMessageV3: ParseMessage(payload); break;
            case MeshCoreProtocolConstants.PushMessageWaiting:
                _ = RequestNextMessageAsync();
                PushReceived?.Invoke(this, payload.ToArray());
                break;
            case MeshCoreProtocolConstants.PushAdvert:
            case MeshCoreProtocolConstants.PushPathUpdated:
            case MeshCoreProtocolConstants.PushSendConfirmed:
            case MeshCoreProtocolConstants.PushRawData:
            case MeshCoreProtocolConstants.PushLoginSuccess:
            case MeshCoreProtocolConstants.PushLoginFail:
            case MeshCoreProtocolConstants.PushTraceData:
                PushReceived?.Invoke(this, payload.ToArray());
                break;
            case MeshCoreProtocolConstants.RespError:
                ProtocolError?.Invoke(this, payload.Length > 1 ? $"MeshCore error code {payload[1]}" : "MeshCore returned an error.");
                break;
        }
    }

    private async Task RequestNextMessageAsync()
    {
        try { await SendPayloadAsync(new[] { MeshCoreProtocolConstants.CmdSyncNextMessage }, CancellationToken.None); }
        catch (Exception ex) { ProtocolError?.Invoke(this, ex.Message); }
    }

    private void ParseSelfInfo(ReadOnlySpan<byte> data)
    {
        // code + type + tx power + max tx power + key + lat/lon + reserved + telemetry + manual + freq + bw + sf + cr + name
        if (data.Length < 58)
            throw new InvalidDataException("MeshCore SELF_INFO packet is too short.");

        var type = (MeshCoreContactType)data[1];
        var txPower = data[2];
        var maxTxPower = data[3];
        var publicKey = data.Slice(4, 32).ToArray();
        var lat = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(36, 4));
        var lon = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(40, 4));
        var freq = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(48, 4)) / 1000d;
        var bandwidth = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(52, 4)) / 1000d;
        var sf = data[56];
        var cr = data[57];
        var name = data.Length > 58 ? ReadNullTerminatedUtf8(data.Slice(58)) : string.Empty;

        SelfInfo = new MeshCoreSelfInfo(type, txPower, maxTxPower, publicKey, lat / 1_000_000d, lon / 1_000_000d, freq, bandwidth, sf, cr, name);
        SelfInfoReceived?.Invoke(this, SelfInfo);
    }

    private void ParseDeviceInfo(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2)
            throw new InvalidDataException("MeshCore DEVICE_INFO packet is too short.");

        var fw = data[1];
        var maxContacts = data.Length > 2 ? (byte)(data[2] * 2) : (byte)0;
        var maxChannels = data.Length > 3 ? data[3] : (byte)0;
        var pin = data.Length >= 8 ? BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4)) : 0;
        var build = data.Length >= 20 ? ReadFixedAscii(data.Slice(8, 12)) : string.Empty;
        var model = data.Length >= 60 ? ReadFixedAscii(data.Slice(20, 40)) : string.Empty;
        var version = data.Length >= 80 ? ReadFixedAscii(data.Slice(60, 20)) : string.Empty;

        DeviceInfo = new MeshCoreDeviceInfo(fw, maxContacts, maxChannels, pin, build, model, version);
        DeviceInfoReceived?.Invoke(this, DeviceInfo);
    }

    private void ParseContact(ReadOnlySpan<byte> data)
    {
        if (data.Length < 144)
            throw new InvalidDataException("MeshCore CONTACT packet is too short.");

        var key = data.Slice(1, 32).ToArray();
        var type = (MeshCoreContactType)data[33];
        var flags = data[34];
        var pathLength = unchecked((sbyte)data[35]);
        var path = data.Slice(36, 64).ToArray();
        var name = ReadFixedAscii(data.Slice(100, 32));
        var lastAdvert = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(132, 4));
        var lat = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(136, 4));
        var lon = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(140, 4));

        var contact = new MeshCoreContact(key, type, flags, pathLength, path, name, lastAdvert, lat / 1_000_000d, lon / 1_000_000d);
        _contacts.Add(contact);
        ContactsSynchronized?.Invoke(this, _contacts.ToArray());
    }

    private void ParseMessage(ReadOnlySpan<byte> data)
    {
        // Message layouts differ between protocol revisions. Raw packets are
        // always exposed through PushReceived; this event is intentionally conservative.
        var isDm = data[0] is MeshCoreProtocolConstants.RespContactMessage or MeshCoreProtocolConstants.RespContactMessageV3;
        MessageReceived?.Invoke(this, new MeshCoreMessage(null, null, string.Empty, (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(), null, isDm));
        PushReceived?.Invoke(this, data.ToArray());
    }

    private static string ReadFixedAscii(ReadOnlySpan<byte> data)
    {
        var length = data.IndexOf((byte)0);
        if (length < 0) length = data.Length;
        return Encoding.ASCII.GetString(data[..length]).Trim();
    }

    private static string ReadNullTerminatedUtf8(ReadOnlySpan<byte> data)
    {
        var length = data.IndexOf((byte)0);
        if (length < 0) length = data.Length;
        return Encoding.UTF8.GetString(data[..length]);
    }

    public void Dispose()
    {
        _connection.DataReceived -= OnDataReceived;
        _connection.ConnectionStateChanged -= OnConnectionStateChanged;
    }
}
