using VibeOpenGothic.Core.Settings;
using VibeOpenGothic.Tests.Fixtures;

namespace VibeOpenGothic.Tests.Unit;

public sealed class AppPathsTests
{
	[Fact]
	public void Create_ExplicitDirectory_UsesFullPath()
	{
		using var temp = new TempDirectory();

		var paths = AppPaths.Create(temp.Root);

		Assert.Equal(Path.GetFullPath(temp.Root), paths.DataDirectory);
		Assert.Equal(Path.Combine(paths.DataDirectory, "settings.json"), paths.SettingsFilePath);
		Assert.Equal(Path.Combine(paths.DataDirectory, "log.txt"), paths.LogFilePath);
		Assert.Equal(Path.Combine(paths.DataDirectory, "saves"), paths.SavesDirectory);
	}

	[Fact]
	public void Create_DefaultDirectory_EndsWithApplicationName()
	{
		var paths = AppPaths.Create(null);

		Assert.EndsWith("VibeOpenGothic", paths.DataDirectory, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void EnsureCreated_CreatesDataAndSaveDirectories()
	{
		using var temp = new TempDirectory();
		var paths = AppPaths.Create(Path.Combine(temp.Root, "data"));

		paths.EnsureCreated();

		Assert.True(Directory.Exists(paths.DataDirectory));
		Assert.True(Directory.Exists(paths.SavesDirectory));
	}
}
