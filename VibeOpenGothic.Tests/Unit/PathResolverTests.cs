using VibeOpenGothic.Data;
using VibeOpenGothic.Tests.Fixtures;

namespace VibeOpenGothic.Tests.Unit;

public sealed class PathResolverTests
{
	[Fact]
	public void FindChild_ResolvesActualCase()
	{
		using var temp = new TempDirectory();
		temp.CreateDirectory("system");

		var result = PathResolver.FindChild(temp.Root, "System");

		Assert.NotNull(result);
		Assert.Equal("system", Path.GetFileName(result));
	}

	[Fact]
	public void ResolveExisting_ResolvesNestedPathWithDifferentCase()
	{
		using var temp = new TempDirectory();
		temp.CreateFile(Path.Combine("system", "Gothic.ini"), "[GAME]\n");

		var result = PathResolver.ResolveExisting(temp.Root, "System/Gothic.ini");

		Assert.NotNull(result);
		Assert.Equal("Gothic.ini", Path.GetFileName(result));
		Assert.Contains("system", result.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries));
	}

	[Fact]
	public void ResolveExisting_MissingPath_ReturnsNull()
	{
		using var temp = new TempDirectory();

		Assert.Null(PathResolver.ResolveExisting(temp.Root, "System/Gothic.ini"));
	}

	[Fact]
	public void Split_AcceptsBothSeparators()
	{
		var parts = PathResolver.Split("_work\\Data/Scripts/_compiled").ToArray();

		Assert.Equal(new[] { "_work", "Data", "Scripts", "_compiled" }, parts);
	}
}
