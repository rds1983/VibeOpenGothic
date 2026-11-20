namespace VibeOpenGothic.Core.Logging;

public sealed class ConsoleLogSink : ILogSink
{
    private readonly LogLevel _minLevel;

    public ConsoleLogSink(LogLevel minLevel = LogLevel.Info)
    {
        _minLevel = minLevel;
    }

    public void Write(LogEntry entry)
    {
        if (entry.Level < _minLevel)
        {
            return;
        }

        var line = LogFormatter.Format(entry);

        if (entry.Level >= LogLevel.Warning)
        {
            Console.Error.WriteLine(line);
            if (entry.Exception is not null)
            {
                Console.Error.WriteLine(entry.Exception);
            }
        }
        else
        {
            Console.Out.WriteLine(line);
            if (entry.Exception is not null)
            {
                Console.Out.WriteLine(entry.Exception);
            }
        }
    }

    public void Flush()
    {
        Console.Out.Flush();
        Console.Error.Flush();
    }

    public void Dispose()
    {
        Flush();
    }
}
