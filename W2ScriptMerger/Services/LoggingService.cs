using System.IO;
using System.Text;

namespace W2ScriptMerger.Services;

public class LoggingService
{
    private readonly string _logsDirectory;
    private readonly string _runLogPath;
    private readonly string _currentLogPath;
    private readonly object _lock = new();

    public LoggingService()
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _logsDirectory = Path.Combine(baseDirectory, "log");
        Directory.CreateDirectory(_logsDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _runLogPath = Path.Combine(_logsDirectory, $"log_{timestamp}.txt");
        _currentLogPath = Path.Combine(_logsDirectory, "current.log");
        if (File.Exists(_currentLogPath))
        {
            File.Delete(_currentLogPath);
        }

        var header = $"=== Witcher 2 Script Merger started {DateTime.Now:G} ===";
        WriteLine(header);
    }

    public string LogsDirectory => _logsDirectory;
    public string CurrentLogFile => _runLogPath;

    public void Log(string message)
    {
        WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    private void WriteLine(string line)
    {
        lock (_lock)
        {
            File.AppendAllText(_runLogPath, line + Environment.NewLine, Encoding.UTF8);
            File.AppendAllText(_currentLogPath, line + Environment.NewLine, Encoding.UTF8);
        }
    }
}
