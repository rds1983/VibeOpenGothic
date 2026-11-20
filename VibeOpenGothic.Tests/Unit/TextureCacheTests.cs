using VibeOpenGothic.Assets.Textures;
using VibeOpenGothic.Tests.Fixtures;

namespace VibeOpenGothic.Tests.Unit;

public sealed class TextureCacheTests
{
	[Fact]
	public void Get_CachesCaseInsensitively()
	{
		var loader = new FakeTextureLoader();
		var texture = TestTextures.Create(hasAlpha: false);
		loader.Files["A.TEX"] = texture;
		var cache = new TextureCache(loader);

		var first = cache.Get("A.TEX");
		var second = cache.Get("a.tex");

		Assert.Same(texture, first);
		Assert.Same(first, second);
		Assert.Equal(1, loader.Calls);
	}

	[Fact]
	public void Get_Missing_ReturnsPlaceholderAndReportsOnce()
	{
		var loader = new FakeTextureLoader();
		var missing = new List<string>();
		var cache = new TextureCache(loader, missing.Add);

		var first = cache.Get("Missing.TGA");
		var second = cache.Get("missing.tga");

		Assert.Same(cache.Placeholder, first);
		Assert.Same(first, second);
		Assert.Single(missing);
		Assert.Equal("Missing.TGA", missing[0]);
		Assert.Contains("Missing.TGA", cache.MissingTextures);
		Assert.Equal(1, loader.Calls);
	}

	[Fact]
	public void Get_EmptyName_ReturnsNullWithoutLoading()
	{
		var loader = new FakeTextureLoader();
		var cache = new TextureCache(loader);

		Assert.Null(cache.Get(null));
		Assert.Null(cache.Get(" "));
		Assert.Equal(0, loader.Calls);
		Assert.Empty(cache.MissingTextures);
	}

	[Fact]
	public void Clear_ReleasesCachedAndMissingEntries()
	{
		var loader = new FakeTextureLoader();
		loader.Files["A.TEX"] = TestTextures.Create(hasAlpha: false);
		var cache = new TextureCache(loader);

		cache.Get("A.TEX");
		cache.Get("missing");
		cache.Clear();

		cache.Get("A.TEX");
		cache.Get("missing");
		Assert.Equal(4, loader.Calls);
		Assert.Single(cache.MissingTextures);
	}
}
