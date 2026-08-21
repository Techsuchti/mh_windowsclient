using System.IO;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace MeshhessenClient.Services;

/// <summary>
/// BLE transport for MeshCore Companion devices.
/// MeshCore uses a Nordic-UART-style service: RX is app-to-radio and TX is
/// radio-to-app notifications. Unlike Meshtastic BLE there is no FROMNUM queue.
/// </summary>
public sealed class MeshCoreBluetoothConnectionService : IConnectionService
{
    public static readonly Guid ServiceUuid = Guid.Parse("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");
    public static readonly Guid RxCharacteristicUuid = Guid.Parse("6E400002-B5A3-F393-E0A9-E50E24DCCA9E");
    public static readonly Guid TxCharacteristicUuid = Guid.Parse("6E400003-B5A3-F393-E0A9-E50E24DCCA9E");

    private BluetoothLEDevice? _device;
    private GattCharacteristic? _rx;
    private GattCharacteristic? _tx;
    private bool _disposed;
    private string _displayName = string.Empty;

    public ConnectionType Type => ConnectionType.Bluetooth;
    public string DisplayName => _displayName;
    public bool IsConnected => _device?.ConnectionStatus == BluetoothConnectionStatus.Connected;

    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<byte[]>? DataReceived;

    public async Task ConnectAsync(ConnectionParameters parameters)
    {
        if (parameters is not BluetoothConnectionParameters bt || bt.DeviceAddress == 0)
            throw new ArgumentException("Invalid MeshCore Bluetooth parameters.", nameof(parameters));

        _displayName = string.IsNullOrWhiteSpace(bt.DeviceName) ? "MeshCore BLE" : bt.DeviceName;
        _device = await BluetoothLEDevice.FromBluetoothAddressAsync(bt.DeviceAddress)
            ?? throw new IOException("MeshCore BLE device could not be opened.");
        _device.ConnectionStatusChanged += OnConnectionStatusChanged;

        var services = await _device.GetGattServicesForUuidAsync(ServiceUuid, BluetoothCacheMode.Uncached);
        if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0)
            throw new IOException("MeshCore Companion BLE service was not found.");

        var service = services.Services[0];
        var rxResult = await service.GetCharacteristicsForUuidAsync(RxCharacteristicUuid, BluetoothCacheMode.Uncached);
        var txResult = await service.GetCharacteristicsForUuidAsync(TxCharacteristicUuid, BluetoothCacheMode.Uncached);
        if (rxResult.Status != GattCommunicationStatus.Success || rxResult.Characteristics.Count == 0)
            throw new IOException("MeshCore Companion RX characteristic was not found.");
        if (txResult.Status != GattCommunicationStatus.Success || txResult.Characteristics.Count == 0)
            throw new IOException("MeshCore Companion TX characteristic was not found.");

        _rx = rxResult.Characteristics[0];
        _tx = txResult.Characteristics[0];

        if (!_tx.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))
            throw new IOException("MeshCore Companion TX characteristic does not support notifications.");

        var cccd = await _tx.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.Notify);
        if (cccd != GattCommunicationStatus.Success)
            throw new IOException($"Could not enable MeshCore BLE notifications: {cccd}");

        _tx.ValueChanged += OnTxValueChanged;
        ConnectionStateChanged?.Invoke(this, true);
    }

    public async Task WriteAsync(byte[] data)
    {
        if (_rx == null || !IsConnected)
            throw new InvalidOperationException("MeshCore BLE is not connected.");
        if (data.Length == 0)
            return;

        using var writer = new DataWriter();
        writer.WriteBytes(data);
        var buffer = writer.DetachBuffer();
        await _rx.WriteValueAsync(buffer, GattWriteOption.WriteWithResponse);
    }

    public void Disconnect()
    {
        if (_tx != null)
            _tx.ValueChanged -= OnTxValueChanged;
        if (_device != null)
            _device.ConnectionStatusChanged -= OnConnectionStatusChanged;

        var wasConnected = IsConnected;
        _rx = null;
        _tx = null;
        _device?.Dispose();
        _device = null;
        if (wasConnected)
            ConnectionStateChanged?.Invoke(this, false);
    }

    private void OnTxValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        try
        {
            using var reader = DataReader.FromBuffer(args.CharacteristicValue);
            var data = new byte[reader.UnconsumedBufferLength];
            reader.ReadBytes(data);
            if (data.Length > 0)
                DataReceived?.Invoke(this, data);
        }
        catch (Exception ex)
        {
            Logger.WriteLine($"[MeshCore BLE] RX error: {ex.Message}");
        }
    }

    private void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            ConnectionStateChanged?.Invoke(this, false);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Disconnect();
    }
}
