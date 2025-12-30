using System.IO;
using System.Text;

namespace W2ScriptMerger.Services;

public class LoggingService
{
    private readonly string _currentLogPath;
    private string LogsDirectory { get; }
    private string CurrentLogFile { get; }
    private readonly Lock _lock = new();

    public LoggingService()
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        LogsDirectory = Path.Combine(baseDirectory, "log");
        Directory.CreateDirectory(LogsDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        CurrentLogFile = Path.Combine(LogsDirectory, $"log_{timestamp}.txt");
        _currentLogPath = Path.Combine(LogsDirectory, "current.log");
        if (File.Exists(_currentLogPath))
        {
            File.Delete(_currentLogPath);
        }

        var header = $"=== Witcher 2 Script Merger started {DateTime.Now:G} ===";
        WriteLine(header);
    }

    public void Log(string message)
    {
        WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    private void WriteLine(string line)
    {
        lock (_lock)
        {
            File.AppendAllText(CurrentLogFile, line + Environment.NewLine, Encoding.UTF8);
            File.AppendAllText(_currentLogPath, line + Environment.NewLine, Encoding.UTF8);
        }
    }
}
