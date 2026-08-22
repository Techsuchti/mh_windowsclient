using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using MeshhessenClient.Protocols.MeshCore;
using MeshhessenClient.Services;

namespace MeshhessenClient;

public partial class MeshCoreMainWindow : Window
{
    private readonly ObservableCollection<MeshCoreContact> _contacts = new();
    private readonly ObservableCollection<MeshCoreChannel> _channels = new();
    private readonly ObservableCollection<string> _messages = new();
    private readonly ObservableCollection<MeshCoreNodeListItem> _nodes = new();
    private readonly ObservableCollection<MeshCoreMapListItem> _mapNodes = new();
    private readonly MeshCoreMessageStore _messageStore = new();
    private IConnectionService? _connection;
    private MeshCoreApplicationController? _client;
    private MeshCoreStartupCoordinator? _startup;
    private byte _activeChannel;

    public MeshCoreMainWindow()
    {
        InitializeComponent();
        ContactsListBox.ItemsSource = _contacts;
        ChannelsListBox.ItemsSource = _channels;
        MessagesListBox.ItemsSource = _messages;
        NodesListBox.ItemsSource = _nodes;
        MapNodesListBox.ItemsSource = _mapNodes;
        MeshCoreMap.NodeClicked += MeshCoreMap_NodeClicked;
        RefreshSerialPorts();
        UpdateTransportUi();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (GetTransport() == ConnectionType.Bluetooth) _ = RefreshBluetoothDevicesAsync();
        else if (GetTransport() == ConnectionType.Serial) RefreshSerialPorts();
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
        PortComboBox.Visibility = transport == ConnectionType.Serial || transport == ConnectionType.Bluetooth ? Visibility.Visible : Visibility.Collapsed;
        BaudComboBox.Visibility = transport == ConnectionType.Serial ? Visibility.Visible : Visibility.Collapsed;
        TcpHostTextBox.Visibility = transport == ConnectionType.Tcp ? Visibility.Visible : Visibility.Collapsed;
        TcpPortTextBox.Visibility = transport == ConnectionType.Tcp ? Visibility.Visible : Visibility.Collapsed;
        PortComboBox.Width = transport == ConnectionType.Bluetooth ? 270 : 110;
        if (transport == ConnectionType.Bluetooth) _ = RefreshBluetoothDevicesAsync();
        else if (transport == ConnectionType.Serial) RefreshSerialPorts();
        else PortComboBox.ItemsSource = null;
    }

    private void RefreshSerialPorts()
    {
        PortComboBox.ItemsSource = SerialPort.GetPortNames().OrderBy(x => x).ToArray();
        if (PortComboBox.Items.Count > 0) PortComboBox.SelectedIndex = 0;
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
                .Where(d => d.Name.Contains("MeshCore", StringComparison.OrdinalIgnoreCase) || d.Name.Contains("Companion", StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Name)
                .ToArray();
            PortComboBox.ItemsSource = candidates;
            PortComboBox.DisplayMemberPath = "Name";
            if (candidates.Length > 0) PortComboBox.SelectedIndex = 0;
            StatusText.Text = candidates.Length == 0 ? "No MeshCore BLE device found. Pair the Companion in Windows first." : $"Found {candidates.Length} MeshCore BLE device(s).";
        }
        catch (Exception ex) { StatusText.Text = $"BLE scan failed: {ex.Message}"; }
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_connection?.IsConnected == true) { DisconnectClient(); return; }
            var transport = GetTransport();
            _connection = transport switch
            {
                ConnectionType.Bluetooth => new MeshCoreBluetoothConnectionService(),
                ConnectionType.Tcp => new MeshCoreTcpConnectionService(),
                _ => new SerialConnectionService()
            };
            _connection.ConnectionStateChanged += ConnectionStateChanged;
            _client = new MeshCoreApplicationController(_connection, transport == ConnectionType.Bluetooth ? MeshCoreCompanionTransport.Ble : MeshCoreCompanionTransport.Stream);
            _client.SelfChanged += ClientSelfChanged;
            _client.DeviceChanged += ClientDeviceChanged;
            _client.ContactsChanged += ClientContactsChanged;
            _client.NodesChanged += ClientNodesChanged;
            _client.ChannelReceived += ClientChannelReceived;
            _client.MessageReceived += ClientMessageReceived;
            _client.Error += ClientError;
            _startup = new MeshCoreStartupCoordinator(_client);
            StatusText.Text = "Connecting to MeshCore...";

            if (transport == ConnectionType.Serial)
            {
                if (PortComboBox.SelectedItem is not string port) throw new InvalidOperationException("Select a COM port first.");
                if (BaudComboBox.SelectedItem is not ComboBoxItem baudItem || !int.TryParse(baudItem.Tag?.ToString(), out var baud)) throw new InvalidOperationException("Select a valid baud rate.");
                await _connection.ConnectAsync(new SerialConnectionParameters { PortName = port, BaudRate = baud });
            }
            else if (transport == ConnectionType.Bluetooth)
            {
                if (PortComboBox.SelectedItem is not DeviceInformation device) throw new InvalidOperationException("Select a MeshCore BLE device first.");
                var address = await GetBluetoothAddressAsync(device.Id);
                await _connection.ConnectAsync(new BluetoothConnectionParameters { DeviceAddress = address, DeviceName = device.Name });
            }
            else
            {
                if (!int.TryParse(TcpPortTextBox.Text.Trim(), out var port) || port is < 1 or > 65535) throw new InvalidOperationException("Enter a valid TCP port.");
                await _connection.ConnectAsync(new TcpConnectionParameters { Hostname = TcpHostTextBox.Text.Trim(), Port = port });
            }

            await _startup.InitializeAsync();
            ConnectButton.Content = "Disconnect";
            StatusText.Text = $"MeshCore ready – {_contacts.Count} contacts, {_channels.Count} channels";
        }
        catch (Exception ex) { StatusText.Text = $"Connection error: {ex.Message}"; DisconnectClient(); }
    }

    private static async Task<ulong> GetBluetoothAddressAsync(string deviceId)
    {
        using var device = await BluetoothLEDevice.FromIdAsync(deviceId) ?? throw new InvalidOperationException("Could not open the selected BLE device.");
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
        _nodes.Clear();
        _mapNodes.Clear();
        _messages.Clear();
        MeshCoreMap.SetNodes(Array.Empty<MeshCoreMapNode>());
        NodeCountText.Text = "0 nodes";
        ConnectButton.Content = "Connect";
        StatusText.Text = "Disconnected";
        DeviceText.Text = "No MeshCore device";
        DirectMessageCheckBox.IsChecked = false;
    }

    private void ConnectionStateChanged(object? sender, bool connected) => Dispatcher.Invoke(() => StatusText.Text = connected ? "Connected – starting MeshCore Companion" : "Disconnected");
    private void ClientSelfChanged(object? sender, MeshCoreSelfInfo info) => Dispatcher.Invoke(() => StatusText.Text = $"MeshCore ready – {info.Name} ({info.Type})");
    private void ClientDeviceChanged(object? sender, MeshCoreDeviceInfo info) => Dispatcher.Invoke(() => DeviceText.Text = $"{info.Model} | FW {info.SemanticVersion} | {info.MaxContacts} contacts / {info.MaxChannels} channels");

    private void ClientContactsChanged(object? sender, IReadOnlyList<MeshCoreContact> contacts)
    {
        Dispatcher.Invoke(() =>
        {
            _contacts.Clear();
            foreach (var contact in contacts) _contacts.Add(contact);
        });
    }

    private void ClientNodesChanged(object? sender, EventArgs e)
    {
        if (_client == null) return;
        var nodes = _client.NodeRegistry.Snapshot();
        var mapNodes = MeshCoreMapProjection.WithPosition(nodes).ToArray();
        Dispatcher.Invoke(() =>
        {
            _nodes.Clear();
            foreach (var node in nodes) _nodes.Add(new MeshCoreNodeListItem(node));
            _mapNodes.Clear();
            foreach (var mapNode in mapNodes) _mapNodes.Add(new MeshCoreMapListItem(mapNode));
            MeshCoreMap.SetNodes(mapNodes);
            NodeCountText.Text = $"{nodes.Count} nodes | {mapNodes.Length} with GPS";
        });
    }

    private void ClientChannelReceived(object? sender, MeshCoreChannel channel)
    {
        Dispatcher.Invoke(() =>
        {
            var existing = _channels.FirstOrDefault(x => x.Index == channel.Index);
            if (existing != null) _channels.Remove(existing);
            _channels.Add(channel);
            if (ChannelsListBox.SelectedItem is null && channel.Index == 0) ChannelsListBox.SelectedItem = channel;
        });
    }

    private void ClientMessageReceived(object? sender, MeshCoreMessage message)
    {
        Dispatcher.Invoke(() =>
        {
            var prefix = message.IsDirectMessage ? "[DM]" : $"[CH {message.ChannelIndex ?? 0}]";
            _messages.Add($"{prefix} [{DateTimeOffset.FromUnixTimeSeconds(message.Timestamp):HH:mm:ss}] {message.Text}");
            _messageStore.Add(message);
        });
    }

    private void ClientError(object? sender, string error) => Dispatcher.Invoke(() => StatusText.Text = $"MeshCore error: {error}");
    private void ChannelsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (ChannelsListBox.SelectedItem is MeshCoreChannel channel) _activeChannel = channel.Index; }
    private void ContactsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (ContactsListBox.SelectedItem is MeshCoreContact contact) StatusText.Text = $"Selected {contact.Name} ({contact.Type})"; }

    private void NodesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NodesListBox.SelectedItem is MeshCoreNodeListItem node)
        {
            StatusText.Text = $"Selected {node.DisplayName} ({node.Type}) — {node.PositionText}";
            SelectContactByPublicKey(node.PublicKey);
        }
    }

    private void MapNodesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MapNodesListBox.SelectedItem is MeshCoreMapListItem node)
        {
            StatusText.Text = $"Selected {node.DisplayText}";
            SelectContactById(node.Id);
        }
    }

    private void MeshCoreMap_NodeClicked(object? sender, MeshCoreMapNode node)
    {
        StatusText.Text = $"Selected {node.Name} ({node.Type}) — {node.Latitude:F5}, {node.Longitude:F5}";
        SelectContactById(node.Id);
    }

    private void SelectContactByPublicKey(byte[] publicKey)
    {
        var contact = _contacts.FirstOrDefault(c => c.PublicKey.SequenceEqual(publicKey));
        if (contact != null) ContactsListBox.SelectedItem = contact;
    }

    private void SelectContactById(string id)
    {
        try
        {
            var key = Convert.FromHexString(id);
            if (key.Length != 32) return;
            SelectContactByPublicKey(key);
        }
        catch (FormatException)
        {
            // Ignore malformed node IDs.
        }
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e) => await SendMessageAsync();
    private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { e.Handled = true; await SendMessageAsync(); } }

    private async Task SendMessageAsync()
    {
        var text = MessageTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text) || _client == null) return;
        try
        {
            if (DirectMessageCheckBox.IsChecked == true)
            {
                if (ContactsListBox.SelectedItem is not MeshCoreContact contact) throw new InvalidOperationException("Select a contact before sending a direct message.");
                if (contact.PublicKey.Length != 32) throw new InvalidOperationException("The selected contact has no valid 32-byte public key.");
                await _client.SendDirectMessageAsync(contact.PublicKey, text);
                _messageStore.Add(new MeshCoreMessage(contact.PublicKey.Take(6).ToArray(), null, text, (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(), null, true), outgoing: true, peerKeyPrefix: Convert.ToHexString(contact.PublicKey.Take(6).ToArray()));
                _messages.Add($"[DM → {contact.Name}] {text}");
            }
            else
            {
                await _client.SendChannelMessageAsync(_activeChannel, text);
                _messageStore.Add(new MeshCoreMessage(null, _activeChannel, text, (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(), null, false), outgoing: true);
                _messages.Add($"[you / CH {_activeChannel}] {text}");
            }
            MessageTextBox.Clear();
        }
        catch (Exception ex) { StatusText.Text = $"Send failed: {ex.Message}"; }
    }

    protected override void OnClosed(EventArgs e) { DisconnectClient(); base.OnClosed(e); }

    private sealed class MeshCoreNodeListItem
    {
        private readonly MeshCoreNode _node;
        public MeshCoreNodeListItem(MeshCoreNode node) => _node = node;
        public string DisplayName => _node.Name;
        public MeshCoreContactType Type => _node.Type;
        public string PositionText => _node.HasPosition ? $"{_node.Latitude:F5}, {_node.Longitude:F5}" : "No GPS";
        public byte[] PublicKey => Convert.FromHexString(_node.PublicKeyHex);
    }

    private sealed class MeshCoreMapListItem
    {
        private readonly MeshCoreMapNode _node;
        public MeshCoreMapListItem(MeshCoreMapNode node) => _node = node;
        public string Id => _node.Id;
        public string DisplayText => $"{_node.Name} · {_node.Type} · {_node.Latitude:F5}, {_node.Longitude:F5}";
    }
}
