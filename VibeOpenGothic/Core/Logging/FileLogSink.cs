using System.Text;

namespace VibeOpenGothic.Core.Logging;

public sealed class FileLogSink : ILogSink
{
    private readonly object _sync = new();
    private readonly StreamWriter _writer;
    private readonly LogLevel _minLevel;

    public FileLogSink(string path, LogLevel minLevel = LogLevel.Debug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(
            fullPath,
            append: false,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
        _minLevel = minLevel;
    }

    public void Write(LogEntry entry)
    {
        if (entry.Level < _minLevel)
        {
            return;
        }

        lock (_sync)
        {
            _writer.WriteLine(LogFormatter.Format(entry));
            if (entry.Exception is not null)
            {
                _writer.WriteLine(entry.Exception);
            }
        }
    }

    public void Flush()
    {
        lock (_sync)
        {
            _writer.Flush();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _writer.Dispose();
        }
    }
}
