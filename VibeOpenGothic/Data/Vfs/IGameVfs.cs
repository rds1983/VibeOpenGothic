namespace VibeOpenGothic.Data.Vfs;

/// <summary>
/// Read-only view of the mounted game data. Paths and bare file names are
/// resolved case-insensitively, matching the original engine.
/// </summary>
public interface IGameVfs : IDisposable
{
    bool FileExists(string nameOrPath);

    bool DirectoryExists(string path);

    Stream OpenRead(string nameOrPath);

    VfsFileEntry? Find(string nameOrPath);

    IEnumerable<VfsFileEntry> EnumerateFiles();
}
