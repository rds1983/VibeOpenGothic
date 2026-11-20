namespace VibeOpenGothic.Core.Logging;

public interface ILogSink : IDisposable
{
	void Write(LogEntry entry);

	void Flush();
}
