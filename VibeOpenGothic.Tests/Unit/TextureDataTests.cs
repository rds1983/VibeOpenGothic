using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Textures;
using VibeOpenGothic.Tests.Fixtures;

namespace VibeOpenGothic.Tests.Unit;

public sealed class TextureDataTests
{
	[Fact]
	public void HasAlpha_DetectsTransparentPixel()
	{
		Assert.False(TestTextures.Create(hasAlpha: false).HasAlpha);
		Assert.True(TestTextures.Create(hasAlpha: true).HasAlpha);
	}

	[Fact]
	public void Constructor_ExposesTopMipDimensions()
	{
		var texture = TestTextures.Create(hasAlpha: false);

		Assert.Equal(1, texture.Width);
		Assert.Equal(1, texture.Height);
		Assert.Single(texture.MipLevels);
		Assert.Equal(new Color(255, 0, 0, 255), texture.AverageColor);
	}

	[Fact]
	public void Constructor_RejectsEmptyMipList()
	{
		Assert.Throws<ArgumentException>(
			() => new TextureData("empty", Array.Empty<TextureMipLevel>(), Color.White));
	}

	[Fact]
	public void Constructor_RejectsWrongRgbaLength()
	{
		var level = new TextureMipLevel(2, 2, new byte[4]);

		Assert.Throws<ArgumentException>(
			() => new TextureData("bad", new[] { level }, Color.White));
	}

	[Fact]
	public void Placeholder_IsMagentaCheckerWithOpaqueAlpha()
	{
		var placeholder = TextureData.CreatePlaceholder();

		Assert.Equal(4, placeholder.Width);
		Assert.Equal(4, placeholder.Height);
		Assert.False(placeholder.HasAlpha);
		Assert.Equal(new Color(255, 0, 255, 255), placeholder.AverageColor);
		Assert.Equal("<placeholder>", placeholder.Name);
	}
}
