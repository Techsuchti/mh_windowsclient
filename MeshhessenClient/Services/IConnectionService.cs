using System;
using System.Threading.Tasks;

namespace MeshhessenClient.Services;

/// <summary>Transport types supported by the MeshCore Windows client.</summary>
public enum ConnectionType
{
    Serial,
    Bluetooth,
    Tcp,
    Kiss
}

/// <summary>Transport-agnostic connection used by MeshCore protocols.</summary>
public interface IConnectionService : IDisposable
{
    ConnectionType Type { get; }
    string DisplayName { get; }
    bool IsConnected { get; }
    event EventHandler<bool>? ConnectionStateChanged;
    event EventHandler<byte[]>? DataReceived;
    Task ConnectAsync(ConnectionParameters parameters);
    void Disconnect();
    Task WriteAsync(byte[] data);
}

public abstract class ConnectionParameters
{
    public ConnectionType Type { get; init; }
}

public class SerialConnectionParameters : ConnectionParameters
{
    public string PortName { get; init; } = string.Empty;
    public int BaudRate { get; init; } = 115200;
    public SerialConnectionParameters() => Type = ConnectionType.Serial;
}

public sealed class BluetoothConnectionParameters : ConnectionParameters
{
    public ulong DeviceAddress { get; init; }
    public string DeviceName { get; init; } = string.Empty;
    public BluetoothConnectionParameters() => Type = ConnectionType.Bluetooth;
}

public sealed class TcpConnectionParameters : ConnectionParameters
{
    public string Hostname { get; init; } = string.Empty;
    public int Port { get; init; } = 4403;
    public TcpConnectionParameters() => Type = ConnectionType.Tcp;
}

public sealed class KissConnectionParameters : SerialConnectionParameters
{
    public KissConnectionParameters() => Type = ConnectionType.Kiss;
}
