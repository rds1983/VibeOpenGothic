using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Tests.Fixtures;

namespace VibeOpenGothic.Tests.Unit;

public sealed class ModManifestTests
{
	[Fact]
	public void Load_ParsesAllFields()
	{
		using var temp = new TempDirectory();
		var system = temp.CreateDirectory("system");
		temp.CreateFile(
			Path.Combine("system", "mod.ini"),
			"""
            [FILES]
            GAME=MODGAME
            VDF=mod1.mod mod2.mod
            OUTPUTUNITS=OU

            [SETTINGS]
            WORLD=Worlds\MyWorld.zen
            PLAYER=PC_HERO
            """);

		var manifest = ModManifest.Load("mod.ini", system, out var error);

		Assert.Null(error);
		Assert.Equal("MODGAME.DAT", manifest.GameDat);
		Assert.Equal("MyWorld.zen", manifest.World);
		Assert.Equal("PC_HERO", manifest.Player);
		Assert.Equal("OU", manifest.OutputUnits);
		Assert.True(manifest.HasManifest);
		Assert.True(manifest.HasVdfFilter);
		Assert.Equal(new[] { "mod1.mod", "mod2.mod" }, manifest.VdfFiles);
		Assert.True(manifest.IncludesModArchive("mod1.mod"));
		Assert.False(manifest.IncludesModArchive("mod3.mod"));
	}

	[Fact]
	public void Load_WithoutVdfKey_IncludesAllMods()
	{
		using var temp = new TempDirectory();
		var system = temp.CreateDirectory("system");
		temp.CreateFile(Path.Combine("system", "mod.ini"), "[FILES]\nGAME=MODGAME\n");

		var manifest = ModManifest.Load("mod.ini", system, out var error);

		Assert.Null(error);
		Assert.True(manifest.HasManifest);
		Assert.False(manifest.HasVdfFilter);
		Assert.True(manifest.IncludesModArchive("anything.mod"));
	}

	[Fact]
	public void Load_MissingFile_ReturnsError()
	{
		using var temp = new TempDirectory();
		var system = temp.CreateDirectory("system");

		var manifest = ModManifest.Load("missing.ini", system, out var error);

		Assert.NotNull(error);
		Assert.Same(ModManifest.Empty, manifest);
	}

	[Fact]
	public void Load_NoPath_ReturnsEmpty()
	{
		var manifest = ModManifest.Load(null, "unused", out var error);

		Assert.Null(error);
		Assert.Same(ModManifest.Empty, manifest);
		Assert.False(manifest.HasManifest);
		Assert.False(manifest.IncludesModArchive("anything.mod"));
	}
}
