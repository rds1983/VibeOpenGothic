namespace VibeOpenGothic.Data;

public sealed record GothicInstallLookupResult(
    GothicInstall? Install,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> SearchedPaths)
{
    public bool Success => Install is not null;
}
