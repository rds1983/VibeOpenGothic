namespace VibeOpenGothic.Core.CommandLine;

public sealed record CommandLineParseResult(
    CommandLineOptions Options,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors)
{
    public bool HasErrors => Errors.Count > 0;
}
