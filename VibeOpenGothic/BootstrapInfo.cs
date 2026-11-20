namespace VibeOpenGothic;

/// <summary>
/// Build-visible identity used by the shell and smoke tests.
/// </summary>
public static class BootstrapInfo
{
	public const string Name = "VibeOpenGothic";

	public static string Version { get; } =
		typeof(BootstrapInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
