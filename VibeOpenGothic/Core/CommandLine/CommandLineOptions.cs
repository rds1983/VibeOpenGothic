namespace VibeOpenGothic.Core.CommandLine;

public sealed record CommandLineOptions
{
	public string? GothicPath { get; init; }

	public string? ModFile { get; init; }

	public string? StartupWorld { get; init; }

	public string? SaveToLoad { get; init; }

	public string? DataDirectory { get; init; }

	public string? ScreenshotPath { get; init; }

	public bool ShowHelp { get; init; }

	public bool ShowVersion { get; init; }

	public bool Probe { get; init; }

	public bool CheckMeshes { get; init; }

	public bool CheckTextures { get; init; }

	public bool CheckModels { get; init; }

	public bool CheckAnimations { get; init; }

	public bool CheckWorld { get; init; }

	public bool DevMode { get; init; }

	public bool Windowed { get; init; }

	public GameVersionOverride VersionOverride { get; init; } = GameVersionOverride.Auto;
}
