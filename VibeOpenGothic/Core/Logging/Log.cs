namespace VibeOpenGothic.Core.Logging;

/// <summary>
/// Process-wide logging entry point. Sinks are configured once at startup; all
/// writes are thread-safe and never throw into game code.
/// </summary>
public static class Log
{
	private static readonly object Sync = new();
	private static ILogSink[] _sinks = Array.Empty<ILogSink>();
	private static LogLevel _minLevel = LogLevel.Info;
	private static bool _started;

	public static string LogFilePath { get; private set; } = string.Empty;

	public static bool IsStarted
	{
		get
		{
			lock (Sync)
			{
				return _started;
			}
		}
	}

	public static void Start(string logFilePath, LogLevel minLevel = LogLevel.Info, bool writeToConsole = true)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);

		lock (Sync)
		{
			if (_started)
			{
				throw new InvalidOperationException("Logging is already started.");
			}

			var sinks = new List<ILogSink>();
			if (writeToConsole)
			{
				sinks.Add(new ConsoleLogSink(minLevel));
			}

			sinks.Add(new FileLogSink(logFilePath, LogLevel.Debug));
			_sinks = sinks.ToArray();
			_minLevel = minLevel;
			LogFilePath = Path.GetFullPath(logFilePath);
			_started = true;
		}
	}

	public static void Stop()
	{
		ILogSink[] sinks;

		lock (Sync)
		{
			if (!_started)
			{
				return;
			}

			sinks = _sinks;
			_sinks = Array.Empty<ILogSink>();
			_started = false;
		}

		foreach (var sink in sinks)
		{
			try
			{
				sink.Flush();
				sink.Dispose();
			}
			catch
			{
				// Logging shutdown must never throw.
			}
		}
	}

	public static void Flush()
	{
		ILogSink[] sinks;

		lock (Sync)
		{
			sinks = _sinks;
		}

		foreach (var sink in sinks)
		{
			try
			{
				sink.Flush();
			}
			catch
			{
				// Ignore sink failures.
			}
		}
	}

	public static void Debug(string category, string message)
	{
		Write(LogLevel.Debug, category, message, exception: null);
	}

	public static void Info(string category, string message)
	{
		Write(LogLevel.Info, category, message, exception: null);
	}

	public static void Warning(string category, string message)
	{
		Write(LogLevel.Warning, category, message, exception: null);
	}

	public static void Error(string category, string message, Exception? exception = null)
	{
		Write(LogLevel.Error, category, message, exception);
	}

	private static void Write(LogLevel level, string category, string message, Exception? exception)
	{
		ILogSink[] sinks;
		LogLevel minLevel;

		lock (Sync)
		{
			sinks = _sinks;
			minLevel = _minLevel;
		}

		if (level < minLevel || sinks.Length == 0)
		{
			return;
		}

		var entry = new LogEntry(DateTimeOffset.Now, level, category, message, exception);

		foreach (var sink in sinks)
		{
			try
			{
				sink.Write(entry);
			}
			catch
			{
				// A failing log sink must not take the game down.
			}
		}

		if (level == LogLevel.Error)
		{
			Flush();
		}
	}
}
