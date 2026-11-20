namespace VibeOpenGothic.Data.Installs;

public sealed record VdfArchive(
    string Path,
    string Name,
    uint Timestamp,
    bool IsMod,
    int Order,
    long SizeBytes);
