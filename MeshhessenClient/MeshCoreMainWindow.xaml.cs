using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows;
using System.Windows.Input;
using MeshhessenClient.Protocols.MeshCore;
using MeshhessenClient.Services;

namespace MeshhessenClient;

public partial class MeshCoreMainWindow : Window
{
    private readonly ObservableCollection<MeshCoreContact> _contacts = new();
    private readonly ObservableCollection<MeshCoreChannel> _channels = new();
    private readonly ObservableCollection<string> _messages = new();
    private SerialConnectionService? _connection;
    private MeshCoreApplicationController? _client;
    private MeshCoreStartupCoordinator? _startup;
    private byte _activeChannel;

    public MeshCoreMainWindow()
    {
        InitializeComponent();
        ContactsListBox.ItemsSource = _contacts;
        ChannelsListBox.ItemsSource = _channels;
        MessagesListBox.ItemsSource = _messages;
        RefreshPorts();
    }

    private void RefreshPortsButton_Click(object sender, RoutedEventArgs e) => RefreshPorts();

    private void RefreshPorts()
    {
        PortComboBox.ItemsSource = SerialPort.GetPortNames().OrderBy(x => x).ToArray();
        if (PortComboBox.Items.Count > 0)
            PortComboBox.SelectedIndex = 0;
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

            if (PortComboBox.SelectedItem is not string port)
            {
                StatusText.Text = "Select a COM port first.";
                return;
            }

            if (BaudComboBox.SelectedItem is not ComboBoxItem baudItem || !int.TryParse(baudItem.Tag?.ToString(), out var baud))
            {
                StatusText.Text = "Select a valid baud rate.";
                return;
            }

            _connection = new SerialConnectionService();
            _connection.ConnectionStateChanged += ConnectionStateChanged;
            _client = new MeshCoreApplicationController(_connection, MeshCoreCompanionTransport.Stream);
            _client.SelfChanged += ClientSelfChanged;
            _client.DeviceChanged += ClientDeviceChanged;
            _client.ContactsChanged += ClientContactsChanged;
            _client.ChannelReceived += ClientChannelReceived;
            _client.MessageReceived += ClientMessageReceived;
            _client.Error += ClientError;

            _startup = new MeshCoreStartupCoordinator(_client);
            StatusText.Text = $"Connecting to MeshCore on {port}...";
            await _connection.ConnectAsync(new SerialConnectionParameters { PortName = port, BaudRate = baud });
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

    private void ChannelsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ChannelsListBox.SelectedItem is MeshCoreChannel channel)
            _activeChannel = channel.Index;
    }

    private void ContactsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { }

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
