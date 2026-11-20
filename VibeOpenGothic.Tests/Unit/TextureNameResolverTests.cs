using VibeOpenGothic.Assets.Textures;
using VibeOpenGothic.Data.Vfs;

namespace VibeOpenGothic.Tests.Unit;

public sealed class TextureNameResolverTests
{
	[Fact]
	public void GetCandidates_TriesOriginalThenGothicCompressedTex()
	{
		var candidates = TextureNameResolver.GetCandidates("Textures\\Armor.TGA");

		Assert.Equal(
			new[] { "Textures/Armor.TGA", "Textures/Armor-C.TEX", "Textures/Armor.TEX" },
			candidates);
	}

	[Fact]
	public void GetCandidates_NoExtension_AddsCompressedAndPlainTex()
	{
		var candidates = TextureNameResolver.GetCandidates("Armor");

		Assert.Contains("Armor", candidates);
		Assert.Contains("Armor-C.TEX", candidates);
		Assert.Contains("Armor.TEX", candidates);
	}

	[Fact]
	public void GetCandidates_Empty_ReturnsEmpty()
	{
		Assert.Empty(TextureNameResolver.GetCandidates(null));
		Assert.Empty(TextureNameResolver.GetCandidates("  "));
	}

	[Fact]
	public void Resolve_FindsGothicCompressedTexWhenMaterialUsesTga()
	{
		using var vfs = new InMemoryGameVfs();
		vfs.Add("/_WORK/DATA/TEXTURES/ARMOR-C.TEX", new byte[] { 1 });

		var entry = TextureNameResolver.Resolve(vfs, "Armor.TGA");

		Assert.NotNull(entry);
		Assert.Equal("/_WORK/DATA/TEXTURES/ARMOR-C.TEX", entry.Path);
	}

	[Fact]
	public void Resolve_FallsBackToPlainTex()
	{
		using var vfs = new InMemoryGameVfs();
		vfs.Add("/_WORK/DATA/TEXTURES/ARMOR.TEX", new byte[] { 1 });

		var entry = TextureNameResolver.Resolve(vfs, "Armor.TGA");

		Assert.NotNull(entry);
		Assert.Equal("/_WORK/DATA/TEXTURES/ARMOR.TEX", entry.Path);
	}

	[Fact]
	public void Resolve_IsCaseInsensitiveAndAcceptsNoExtension()
	{
		using var vfs = new InMemoryGameVfs();
		vfs.Add("/_WORK/DATA/TEXTURES/ARMOR.TEX", new byte[] { 1 });

		var entry = TextureNameResolver.Resolve(vfs, "armor");

		Assert.NotNull(entry);
		Assert.Equal("/_WORK/DATA/TEXTURES/ARMOR.TEX", entry.Path);
	}

	[Fact]
	public void Resolve_Missing_ReturnsNull()
	{
		using var vfs = new InMemoryGameVfs();

		Assert.Null(TextureNameResolver.Resolve(vfs, "missing.TGA"));
	}
}
