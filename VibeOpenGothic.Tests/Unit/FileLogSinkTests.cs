using VibeOpenGothic.Core.Logging;
using VibeOpenGothic.Tests.Fixtures;

namespace VibeOpenGothic.Tests.Unit;

public sealed class FileLogSinkTests
{
    [Fact]
    public void Write_FormatsLevelCategoryAndMessage()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Root, "logs", "test.log");

        using (var sink = new FileLogSink(path))
        {
            sink.Write(new LogEntry(
                DateTimeOffset.Now,
                LogLevel.Info,
                "Test",
                "hello"));
            sink.Flush();
        }

        var text = File.ReadAllText(path);
        Assert.Contains("INFO", text);
        Assert.Contains("[Test]", text);
        Assert.Contains("hello", text);
    }

    [Fact]
    public void Write_Exception_AppendsStackTrace()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Root, "test.log");

        using (var sink = new FileLogSink(path))
        {
            sink.Write(new LogEntry(
                DateTimeOffset.Now,
                LogLevel.Error,
                "Test",
                "failed",
                new InvalidOperationException("boom")));
            sink.Flush();
        }

        var text = File.ReadAllText(path);
        Assert.Contains("failed", text);
        Assert.Contains("InvalidOperationException", text);
        Assert.Contains("boom", text);
    }

    [Fact]
    public void Write_BelowMinimumLevel_IsIgnored()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Root, "test.log");

        using (var sink = new FileLogSink(path, LogLevel.Warning))
        {
            sink.Write(new LogEntry(
                DateTimeOffset.Now,
                LogLevel.Debug,
                "Test",
                "hidden"));
            sink.Flush();
        }

        Assert.Equal(string.Empty, File.ReadAllText(path));
    }
}
