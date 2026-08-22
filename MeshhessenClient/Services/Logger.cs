using System.Diagnostics;
using System.IO;

namespace MeshhessenClient.Services;

public static class Logger
{
    private static readonly object _lock = new();
    private static string? _logFilePath;
    private static StreamWriter? _logWriter;

    public static event EventHandler<string>? LogMessageReceived;

    static Logger()
    {
        try
        {
            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            _logFilePath = Path.Combine(appPath, "meshcore-windows-client.log");

            if (File.Exists(_logFilePath) && new FileInfo(_logFilePath).Length > 5 * 1024 * 1024)
                File.Delete(_logFilePath);

            _logWriter = new StreamWriter(_logFilePath, append: true) { AutoFlush = true };
            WriteLine("=== MeshCore Windows Client gestartet ===");
            WriteLine($"Log-Datei: {_logFilePath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Logger initialization failed: {ex.Message}");
        }
    }

    public static void WriteLine(string message)
    {
        string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
        lock (_lock)
        {
            try
            {
                Debug.WriteLine(logMessage);
                _logWriter?.WriteLine(logMessage);
                LogMessageReceived?.Invoke(null, logMessage);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Logger write failed: {ex.Message}");
            }
        }
    }

    public static string? GetLogFilePath() => _logFilePath;

    public static void Close()
    {
        lock (_lock)
        {
            try
            {
                _logWriter?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] === MeshCore Windows Client beendet ===");
                _logWriter?.Flush();
                _logWriter?.Dispose();
                _logWriter = null;
            }
            catch
            {
                // Ignore shutdown logging failures.
            }
        }
    }
}
