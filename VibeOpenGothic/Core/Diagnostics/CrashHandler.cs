namespace VibeOpenGothic.Core.Diagnostics;

public static class CrashHandler
{
    private static int _handling;
    private static string _dataDirectory = string.Empty;
    private static string _logFilePath = string.Empty;

    public static void Install(string dataDirectory, string logFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);

        _dataDirectory = dataDirectory;
        _logFilePath = logFilePath;

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public static void Report(Exception exception, string context, bool showDialog)
    {
        if (Interlocked.Exchange(ref _handling, 1) != 0)
        {
            return;
        }

        try
        {
            Logging.Log.Error("Crash", context, exception);

            string? reportPath = null;
            try
            {
                reportPath = CrashReport.Write(
                    exception,
                    _dataDirectory,
                    _logFilePath,
                    context);
            }
            catch (Exception reportException)
            {
                Logging.Log.Warning(
                    "Crash",
                    $"Unable to write crash report: {reportException.Message}");
            }

            Logging.Log.Flush();

            if (showDialog)
            {
                var reportLine = reportPath is null ? string.Empty : $"\nReport: {reportPath}";
                NativeMessageBox.ShowError(
                    "VibeOpenGothic - fatal error",
                    $"{context}\n\n{exception.GetType().Name}: {exception.Message}\n\nLog: {_logFilePath}{reportLine}");
            }
        }
        catch
        {
            // A crash handler must never throw.
        }
        finally
        {
            Interlocked.Exchange(ref _handling, 0);
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception exception)
        {
            Report(exception, "Unhandled exception", showDialog: true);
        }
        else
        {
            Logging.Log.Error("Crash", $"Unhandled non-exception object: {args.ExceptionObject}");
            Logging.Log.Flush();
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        args.SetObserved();

        if (args.Exception.InnerException is not null)
        {
            Report(args.Exception.InnerException, "Unobserved task exception", showDialog: false);
        }
        else
        {
            Report(args.Exception, "Unobserved task exception", showDialog: false);
        }
    }
}
