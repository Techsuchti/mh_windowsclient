using System.Diagnostics;

namespace MeshhessenClient.Services;

public static class Logger
{
    private static readonly object Lock = new();
    private static readonly string LogFilePath = Path.Combine(AppContext.BaseDirectory, "meshcore-windows-client.log");
    private static StreamWriter? _writer;

    public static event EventHandler<string>? LogMessageReceived;

    static Logger()
    {
        try
        {
            var info = new FileInfo(LogFilePath);
            if (info.Exists && info.Length > 5 * 1024 * 1024)
                info.Delete();
            _writer = new StreamWriter(LogFilePath, append: true) { AutoFlush = true };
            WriteLine("=== MeshCore Windows Client started ===");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Logger initialization failed: {ex.Message}");
        }
    }

    public static void WriteLine(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
        lock (Lock)
        {
            Debug.WriteLine(line);
            try { _writer?.WriteLine(line); } catch { }
            try { LogMessageReceived?.Invoke(null, line); } catch { }
        }
    }

    public static string GetLogFilePath() => LogFilePath;

    public static void Close()
    {
        lock (Lock)
        {
            try
            {
                _writer?.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] === MeshCore Windows Client stopped ===");
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
            }
            catch { }
        }
    }
}
