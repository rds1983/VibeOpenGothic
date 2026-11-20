using VibeOpenGothic.Core.Diagnostics;
using VibeOpenGothic.Tests.Fixtures;

namespace VibeOpenGothic.Tests.Unit;

public sealed class CrashReportTests
{
	[Fact]
	public void Build_ContainsExceptionAndLogPath()
	{
		var exception = new InvalidOperationException("boom");
		var logPath = @"C:\logs\log.txt";

		var text = CrashReport.Build(exception, logPath);

		Assert.Contains("VibeOpenGothic crash report", text);
		Assert.Contains("InvalidOperationException", text);
		Assert.Contains("boom", text);
		Assert.Contains(logPath, text);
	}

	[Fact]
	public void Write_CreatesReportFile()
	{
		using var temp = new TempDirectory();
		var dataDirectory = Path.Combine(temp.Root, "reports");
		var logPath = Path.Combine(temp.Root, "log.txt");

		var path = CrashReport.Write(
			new InvalidOperationException("boom"),
			dataDirectory,
			logPath);

		Assert.True(File.Exists(path));
		var text = File.ReadAllText(path);
		Assert.Contains("boom", text);
		Assert.Contains(logPath, text);
	}

	[Fact]
	public void CrashHandler_Report_WritesCrashReportFile()
	{
		using var temp = new TempDirectory();
		var logPath = Path.Combine(temp.Root, "log.txt");
		CrashHandler.Install(temp.Root, logPath);

		CrashHandler.Report(
			new InvalidOperationException("boom"),
			"test context",
			showDialog: false);

		var files = Directory.GetFiles(temp.Root, "crash-*.txt");
		Assert.Single(files);
		Assert.Contains("test context", File.ReadAllText(files[0]));
	}
}
