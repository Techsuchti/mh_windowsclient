using System.Collections.Generic;

namespace MeshhessenClient.Protocols.MeshCore;

/// <summary>MeshCore KISS modem framing for stream transports.</summary>
public sealed class MeshCoreKissCodec
{
    public const byte Fend = 0xC0;
    public const byte Fesc = 0xDB;
    public const byte Tfec = 0xDC;
    public const byte Tfesc = 0xDD;

    private readonly List<byte> _buffer = new();
    private bool _inFrame;
    private bool _escaped;

    public byte[] Encode(ReadOnlySpan<byte> payload, byte port = 0)
    {
        var output = new List<byte>(payload.Length + 4) { Fend, port };
        foreach (var b in payload)
        {
            if (b == Fend) { output.Add(Fesc); output.Add(Tfec); }
            else if (b == Fesc) { output.Add(Fesc); output.Add(Tfesc); }
            else output.Add(b);
        }
        output.Add(Fend);
        return output.ToArray();
    }

    public IEnumerable<byte[]> Feed(ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
        {
            if (b == Fend)
            {
                if (_inFrame && _buffer.Count > 1)
                {
                    var frame = _buffer.Skip(1).ToArray();
                    if (frame.Length > 0) yield return frame;
                }
                _buffer.Clear();
                _escaped = false;
                _inFrame = true;
                continue;
            }

            if (!_inFrame) continue;

            if (_escaped)
            {
                _buffer.Add(b switch
                {
                    Tfec => Fend,
                    Tfesc => Fesc,
                    _ => b
                });
                _escaped = false;
                continue;
            }

            if (b == Fesc) { _escaped = true; continue; }
            _buffer.Add(b);
        }
    }
}
