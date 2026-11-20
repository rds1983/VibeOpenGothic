using System.Buffers.Binary;
using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Tests.Fixtures;

namespace VibeOpenGothic.Tests.Unit;

public sealed class VdfArchiveTests
{
    [Fact]
    public void TimestampReader_ReadsHeaderValue()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Root, "test.vdf");
        WriteVdf(path, 0x12345678);

        Assert.Equal(0x12345678u, VdfTimestampReader.Read(path));
    }

    [Fact]
    public void TimestampReader_ShortFile_ReturnsZero()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("short.vdf", "not a vdf");

        Assert.Equal(0u, VdfTimestampReader.Read(path));
    }

    [Fact]
    public void TimestampReader_MissingFile_ReturnsZero()
    {
        using var temp = new TempDirectory();

        Assert.Equal(0u, VdfTimestampReader.Read(Path.Combine(temp.Root, "missing.vdf")));
    }

    [Fact]
    public void MountPlanner_OrdersModsThenNewest()
    {
        var archives = new[]
        {
            new VdfArchive("a.vdf", "a.vdf", 100, false, 0, 0),
            new VdfArchive("b.vdf", "b.vdf", 200, false, 1, 0),
            new VdfArchive("m1.mod", "m1.mod", 50, true, 2, 0),
            new VdfArchive("m2.mod", "m2.mod", 300, true, 3, 0),
        };

        var ordered = MountPlanner.Order(archives);

        Assert.Equal(new[] { "m2.mod", "m1.mod", "b.vdf", "a.vdf" }, ordered.Select(x => x.Name));
    }

    [Fact]
    public void MountPlanner_EqualTimestamps_UsesDescendingOrder()
    {
        var archives = new[]
        {
            new VdfArchive("a.vdf", "a.vdf", 100, false, 0, 0),
            new VdfArchive("b.vdf", "b.vdf", 100, false, 1, 0),
        };

        var ordered = MountPlanner.Order(archives);

        Assert.Equal(new[] { "b.vdf", "a.vdf" }, ordered.Select(x => x.Name));
    }

    internal static void WriteVdf(string path, uint timestamp, int size = 300)
    {
        var bytes = new byte[size];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(280), timestamp);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }
}
