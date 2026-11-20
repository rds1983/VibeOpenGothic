namespace VibeOpenGothic.Core.Logging;

internal static class LogFormatter
{
	public static string Format(LogEntry entry)
	{
		return $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{LevelText(entry.Level),-7}] [{entry.Category}] {entry.Message}";
	}

	private static string LevelText(LogLevel level)
	{
		return level switch
		{
			LogLevel.Debug => "DEBUG",
			LogLevel.Info => "INFO",
			LogLevel.Warning => "WARNING",
			LogLevel.Error => "ERROR",
			_ => "UNKNOWN",
		};
	}
}
