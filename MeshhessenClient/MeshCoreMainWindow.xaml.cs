using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Windows.Devices.Enumeration;
using MeshhessenClient.Protocols.MeshCore;
using MeshhessenClient.Services;

namespace MeshhessenClient;

public partial class MeshCoreMainWindow : Window
{
    private readonly ObservableCollection<MeshCoreContact> _contacts = new();
    private readonly ObservableCollection<MeshCoreChannel> _channels = new();
    private readonly ObservableCollection<string> _messages = new();
    private IConnectionService? _connection;
    private MeshCoreApplicationController? _client;
    private MeshCoreStartupCoordinator? _startup;
    private byte _activeChannel;
    private ulong _selectedBluetoothAddress;
    private string _selectedBluetoothName = string.Empty;

    public MeshCoreMainWindow()
    {
        InitializeComponent();
        ContactsListBox.ItemsSource = _contacts;
        ChannelsListBox.ItemsSource = _channels;
        MessagesListBox.ItemsSource = _messages;
        RefreshSerialPorts();
        UpdateTransportUi();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetTransport() == ConnectionType.Bluetooth)
            _ = RefreshBluetoothDevicesAsync();
        else if (GetTransport() == ConnectionType.Serial)
            RefreshSerialPorts();
    }

    private void TransportComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized) return;
        UpdateTransportUi();
    }

    private ConnectionType GetTransport()
    {
        var tag = (TransportComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        return tag switch
        {
            "Bluetooth" => ConnectionType.Bluetooth,
            "Tcp" => ConnectionType.Tcp,
            _ => ConnectionType.Serial
        };
    }

    private void UpdateTransportUi()
    {
        var transport = GetTransport();
        PortComboBox.Visibility = transport == ConnectionType.Serial || transport == ConnectionType.Bluetooth
            ? Visibility.Visible : Visibility.Collapsed;
        BaudComboBox.Visibility = transport == ConnectionType.Serial ? Visibility.Visible : Visibility.Collapsed;
        TcpHostTextBox.Visibility = transport == ConnectionType.Tcp ? Visibility.Visible : Visibility.Collapsed;
        TcpPortTextBox.Visibility = transport == ConnectionType.Tcp ? Visibility.Visible : Visibility.Collapsed;
        PortComboBox.Width = transport == ConnectionType.Bluetooth ? 270 : 110;

        if (transport == ConnectionType.Bluetooth)
            _ = RefreshBluetoothDevicesAsync();
        else if (transport == ConnectionType.Serial)
            RefreshSerialPorts();
        else
            PortComboBox.ItemsSource = null;
    }

    private void RefreshSerialPorts()
    {
        PortComboBox.ItemsSource = SerialPort.GetPortNames().OrderBy(x => x).ToArray();
        if (PortComboBox.Items.Count > 0)
            PortComboBox.SelectedIndex = 0;
    }

    private async Task RefreshBluetoothDevicesAsync()
    {
        try
        {
            StatusText.Text = "Scanning for MeshCore BLE devices...";
            var selector = BluetoothLEDevice.GetDeviceSelectorFromPairingState(false);
            var devices = await DeviceInformation.FindAllAsync(selector);
            var candidates = devices
                .Where(d => !string.IsNullOrWhiteSpace(d.Name))
                .Where(d => d.Name.Contains("MeshCore", StringComparison.OrdinalIgnoreCase)
                         || d.Name.Contains("Companion", StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Name)
                .ToArray();

            PortComboBox.ItemsSource = candidates;
            PortComboBox.DisplayMemberPath = "Name";
            if (candidates.Length > 0)
                PortComboBox.SelectedIndex = 0;

            StatusText.Text = candidates.Length == 0
                ? "No MeshCore BLE device found. Pair the Companion in Windows first."
                : $"Found {candidates.Length} MeshCore BLE device(s).";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"BLE scan failed: {ex.Message}";
        }
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_connection?.IsConnected == true)
            {
                DisconnectClient();
                return;
            }

            var transport = GetTransport();
            _connection = transport switch
            {
                ConnectionType.Bluetooth => CreateBluetoothConnection(),
                ConnectionType.Tcp => new MeshCoreTcpConnectionService(),
                _ => new SerialConnectionService()
            };

            _connection.ConnectionStateChanged += ConnectionStateChanged;
            _client = new MeshCoreApplicationController(
                _connection,
                transport == ConnectionType.Bluetooth
                    ? MeshCoreCompanionTransport.Ble
                    : MeshCoreCompanionTransport.Stream);
            _client.SelfChanged += ClientSelfChanged;
            _client.DeviceChanged += ClientDeviceChanged;
            _client.ContactsChanged += ClientContactsChanged;
            _client.ChannelReceived += ClientChannelReceived;
            _client.MessageReceived += ClientMessageReceived;
            _client.Error += ClientError;

            _startup = new MeshCoreStartupCoordinator(_client);
            StatusText.Text = "Connecting to MeshCore...";

            if (transport == ConnectionType.Serial)
            {
                if (PortComboBox.SelectedItem is not string port)
                    throw new InvalidOperationException("Select a COM port first.");
                if (BaudComboBox.SelectedItem is not ComboBoxItem baudItem || !int.TryParse(baudItem.Tag?.ToString(), out var baud))
                    throw new InvalidOperationException("Select a valid baud rate.");
                await _connection.ConnectAsync(new SerialConnectionParameters { PortName = port, BaudRate = baud });
            }
            else if (transport == ConnectionType.Bluetooth)
            {
                if (PortComboBox.SelectedItem is not DeviceInformation device)
                    throw new InvalidOperationException("Select a MeshCore BLE device first.");
                var address = await GetBluetoothAddressAsync(device.Id);
                _selectedBluetoothAddress = address;
                _selectedBluetoothName = device.Name;
                await _connection.ConnectAsync(new BluetoothConnectionParameters { DeviceAddress = address, DeviceName = device.Name });
            }
            else
            {
                if (!int.TryParse(TcpPortTextBox.Text.Trim(), out var port) || port is < 1 or > 65535)
                    throw new InvalidOperationException("Enter a valid TCP port.");
                await _connection.ConnectAsync(new TcpConnectionParameters { Hostname = TcpHostTextBox.Text.Trim(), Port = port });
            }

            await _startup.InitializeAsync();
            ConnectButton.Content = "Disconnect";
            StatusText.Text = $"MeshCore ready – {_contacts.Count} contacts, {_channels.Count} channels";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Connection error: {ex.Message}";
            DisconnectClient();
        }
    }

    private IConnectionService CreateBluetoothConnection() => new MeshCoreBluetoothConnectionService();

    private static async Task<ulong> GetBluetoothAddressAsync(string deviceId)
    {
        using var device = await Windows.Devices.Bluetooth.BluetoothLEDevice.FromIdAsync(deviceId)
            ?? throw new InvalidOperationException("Could not open the selected BLE device.");
        return device.BluetoothAddress;
    }

    private void DisconnectClient()
    {
        _startup = null;
        _client?.Dispose();
        _client = null;
        _connection?.Disconnect();
        _connection?.Dispose();
        _connection = null;
        _contacts.Clear();
        _channels.Clear();
        _messages.Clear();
        ConnectButton.Content = "Connect";
        StatusText.Text = "Disconnected";
        DeviceText.Text = "No MeshCore device";
    }

    private void ConnectionStateChanged(object? sender, bool connected)
        => Dispatcher.Invoke(() => StatusText.Text = connected ? "Connected – starting MeshCore Companion" : "Disconnected");

    private void ClientSelfChanged(object? sender, MeshCoreSelfInfo info)
        => Dispatcher.Invoke(() => StatusText.Text = $"MeshCore ready – {info.Name} ({info.Type})");

    private void ClientDeviceChanged(object? sender, MeshCoreDeviceInfo info)
        => Dispatcher.Invoke(() => DeviceText.Text = $"{info.Model}  |  FW {info.SemanticVersion}  |  {info.MaxContacts} contacts / {info.MaxChannels} channels");

    private void ClientContactsChanged(object? sender, IReadOnlyList<MeshCoreContact> contacts)
    {
        Dispatcher.Invoke(() =>
        {
            _contacts.Clear();
            foreach (var contact in contacts) _contacts.Add(contact);
        });
    }

    private void ClientChannelReceived(object? sender, MeshCoreChannel channel)
    {
        Dispatcher.Invoke(() =>
        {
            var existing = _channels.FirstOrDefault(x => x.Index == channel.Index);
            if (existing != null) _channels.Remove(existing);
            _channels.Add(channel);
            if (ChannelsListBox.SelectedItem is null && channel.Index == 0)
                ChannelsListBox.SelectedItem = channel;
        });
    }

    private void ClientMessageReceived(object? sender, MeshCoreMessage message)
        => Dispatcher.Invoke(() => _messages.Add($"[{DateTimeOffset.FromUnixTimeSeconds(message.Timestamp):HH:mm:ss}] {message.Text}"));

    private void ClientError(object? sender, string error)
        => Dispatcher.Invoke(() => StatusText.Text = $"MeshCore error: {error}");

    private void ChannelsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChannelsListBox.SelectedItem is MeshCoreChannel channel)
            _activeChannel = channel.Index;
    }

    private void ContactsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private async void SendButton_Click(object sender, RoutedEventArgs e) => await SendMessageAsync();

    private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await SendMessageAsync();
        }
    }

    private async Task SendMessageAsync()
    {
        var text = MessageTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text) || _client == null) return;
        try
        {
            await _client.SendChannelMessageAsync(_activeChannel, text);
            _messages.Add($"[you] {text}");
            MessageTextBox.Clear();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Send failed: {ex.Message}";
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        DisconnectClient();
        base.OnClosed(e);
    }
}
