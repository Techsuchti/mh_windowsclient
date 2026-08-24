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
        if (MapNodesListBox != null) MapNodesListBox.ItemsSource = _mapNodes;
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
        PortComboBox.DisplayMemberPath = string.Empty;
        PortComboBox.ItemsSource = SerialPort.GetPortNames().OrderBy(x => x).ToArray();
        if (PortComboBox.Items.Count > 0) PortComboBox.SelectedIndex = 0;
    }

    private async Task RefreshBluetoothDevicesAsync()
    {
        try
        {
            StatusText.Text = "Scanning for MeshCore BLE devices...";
            PortComboBox.ItemsSource = null;
            PortComboBox.DisplayMemberPath = "Name";

            // Do not restrict the scan to paired devices. MeshCore Companions can be
            // discoverable before Windows pairing and can then be opened by address.
            var selector = BluetoothLEDevice.GetDeviceSelector();
            var devices = await DeviceInformation.FindAllAsync(selector);
            var candidates = devices
                .Where(d => !string.IsNullOrWhiteSpace(d.Name))
                .Where(d => d.Name.Contains("MeshCore", StringComparison.OrdinalIgnoreCase)
                         || d.Name.Contains("Companion", StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Name)
                .ThenBy(d => d.Id)
                .ToArray();

            PortComboBox.ItemsSource = candidates;
            if (candidates.Length > 0)
            {
                PortComboBox.SelectedIndex = 0;
                StatusText.Text = $"Found {candidates.Length} MeshCore BLE device(s).";
            }
            else
            {
                StatusText.Text = "No MeshCore BLE device found. Power on the Companion and enable Bluetooth.";
            }
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
