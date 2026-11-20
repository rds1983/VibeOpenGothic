using System.Runtime.InteropServices;
using System.Text;

namespace VibeOpenGothic.Core.Diagnostics;

public static class CrashReport
{
    public static string Build(Exception exception, string logFilePath, string? context = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var assembly = typeof(CrashReport).Assembly.GetName();
        var builder = new StringBuilder();

        builder.AppendLine("VibeOpenGothic crash report");
        builder.AppendLine($"Time: {DateTimeOffset.Now:O}");
        builder.AppendLine($"App: {assembly.Name} {assembly.Version?.ToString(3) ?? "unknown"}");
        builder.AppendLine($"OS: {RuntimeInformation.OSDescription}");
        builder.AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
        builder.AppendLine($"Log: {logFilePath}");

        if (!string.IsNullOrWhiteSpace(context))
        {
            builder.AppendLine($"Context: {context}");
        }

        builder.AppendLine();
        builder.AppendLine(exception.ToString());

        return builder.ToString();
    }

    public static string Write(
        Exception exception,
        string dataDirectory,
        string logFilePath,
        string? context = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        Directory.CreateDirectory(dataDirectory);

        var path = Path.Combine(
            dataDirectory,
            $"crash-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.txt");

        File.WriteAllText(
            path,
            Build(exception, logFilePath, context),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        return path;
    }
}
