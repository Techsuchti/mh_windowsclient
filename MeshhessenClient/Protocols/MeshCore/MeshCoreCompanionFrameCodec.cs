using System.Buffers.Binary;

namespace MeshhessenClient.Protocols.MeshCore;

public enum MeshCoreCompanionTransport
{
    Ble,
    Stream
}

/// <summary>
/// Encodes and decodes MeshCore Companion frames.
/// BLE notifications contain the Companion payload directly. Serial/TCP
/// Companion streams wrap app-to-radio frames with 0x3C + uint16 length and
/// radio-to-app frames with 0x3E + uint16 length.
/// </summary>
public sealed class MeshCoreCompanionFrameCodec
{
    private readonly List<byte> _buffer = new();

    public byte[] EncodeCommand(ReadOnlySpan<byte> payload, MeshCoreCompanionTransport transport)
    {
        if (payload.Length > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(payload));

        if (transport == MeshCoreCompanionTransport.Ble)
            return payload.ToArray();

        var frame = new byte[payload.Length + 3];
        frame[0] = MeshCoreProtocolConstants.SerialAppToRadio;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(1, 2), checked((ushort)payload.Length));
        payload.CopyTo(frame.AsSpan(3));
        return frame;
    }

    public IReadOnlyList<byte[]> Feed(ReadOnlySpan<byte> data, MeshCoreCompanionTransport transport)
    {
        if (transport == MeshCoreCompanionTransport.Ble)
            return data.Length == 0 ? Array.Empty<byte[]>() : new[] { data.ToArray() };

        _buffer.AddRange(data.ToArray());
        var result = new List<byte[]>();

        while (true)
        {
            if (_buffer.Count < 3)
                break;

            if (_buffer[0] != MeshCoreProtocolConstants.SerialRadioToApp)
            {
                _buffer.RemoveAt(0);
                continue;
            }

            var length = (ushort)(_buffer[1] | (_buffer[2] << 8));
            if (_buffer.Count < length + 3)
                break;

            result.Add(_buffer.Skip(3).Take(length).ToArray());
            _buffer.RemoveRange(0, length + 3);
        }

        return result;
    }
}
