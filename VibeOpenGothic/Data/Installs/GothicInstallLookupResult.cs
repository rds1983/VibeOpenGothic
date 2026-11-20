namespace VibeOpenGothic.Data.Installs;

public sealed record GothicInstallLookupResult(
	GothicInstall? Install,
	IReadOnlyList<string> Errors,
	IReadOnlyList<string> Warnings,
	IReadOnlyList<string> SearchedPaths)
{
	public bool Success => Install is not null;
}
