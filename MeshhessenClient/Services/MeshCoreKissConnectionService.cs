using System.IO.Ports;
using MeshhessenClient.Protocols.MeshCore;

namespace MeshhessenClient.Services;

/// <summary>Serial KISS transport for MeshCore modem/radio interfaces.</summary>
public sealed class MeshCoreKissConnectionService : IConnectionService
{
    private SerialPort? _serial;
    private readonly MeshCoreKissCodec _codec = new();

    public ConnectionType Type => ConnectionType.Kiss;
    public string DisplayName => _serial?.PortName ?? "MeshCore KISS";
    public bool IsConnected => _serial?.IsOpen == true;
    public event EventHandler<byte[]>? DataReceived;
    public event EventHandler<bool>? ConnectionStateChanged;

    public Task ConnectAsync(ConnectionParameters parameters)
    {
        if (parameters is not KissConnectionParameters serial)
            throw new ArgumentException("KISS requires KissConnectionParameters.", nameof(parameters));
        if (string.IsNullOrWhiteSpace(serial.PortName))
            throw new ArgumentException("KISS port name cannot be empty.", nameof(parameters));

        Disconnect();
        _serial = new SerialPort(serial.PortName, serial.BaudRate)
        {
            ReadTimeout = 500,
            WriteTimeout = 2000,
            DtrEnable = false,
            RtsEnable = false
        };
        _serial.DataReceived += SerialDataReceived;
        _serial.Open();
        ConnectionStateChanged?.Invoke(this, true);
        return Task.CompletedTask;
    }

    public Task WriteAsync(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!IsConnected) throw new InvalidOperationException("KISS connection is not open.");
        var frame = _codec.Encode(data);
        _serial!.Write(frame, 0, frame.Length);
        return Task.CompletedTask;
    }

    private void SerialDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        if (_serial is null || !_serial.IsOpen) return;
        var count = _serial.BytesToRead;
        if (count <= 0) return;
        var buffer = new byte[count];
        _serial.Read(buffer, 0, count);
        foreach (var frame in _codec.Feed(buffer))
            DataReceived?.Invoke(this, frame);
    }

    public void Disconnect()
    {
        if (_serial is null) return;
        _serial.DataReceived -= SerialDataReceived;
        if (_serial.IsOpen) _serial.Close();
        _serial.Dispose();
        _serial = null;
        ConnectionStateChanged?.Invoke(this, false);
    }

    public void Dispose() => Disconnect();
}
