using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Textures;
using VibeOpenGothic.Tests.Fixtures;

namespace VibeOpenGothic.Tests.Unit;

public sealed class MaterialBuilderTests
{
	private static MaterialSource DefaultSource => new(
		"stone",
		MaterialGroup.Stone,
		Color.White,
		15f,
		string.Empty,
		Vector2.One,
		10f,
		TextureAnimationMapping.None,
		Vector2.Zero,
		false,
		false,
		false,
		string.Empty,
		0f,
		false,
		false,
		0f,
		WaveMode.None,
		WaveSpeed.None,
		0f,
		0f,
		false,
		MaterialAlphaFunction.None,
		Vector2.One);

	[Fact]
	public void FromSource_Solid_WhenTextureHasNoAlpha()
	{
		var cache = CreateCache(hasAlpha: false);

		var material = MaterialBuilder.FromSource(
			DefaultSource with { Texture = "A.TEX", TextureScale = new Vector2(2, 3) },
			cache);

		Assert.Equal(MaterialAlphaMode.Solid, material.AlphaMode);
		Assert.False(material.IsTwoSided);
		Assert.True(material.UsesLightmap);
		Assert.Equal("A.TEX", material.TextureName);
		Assert.NotNull(material.TextureSource);
		Assert.Null(material.Texture);
		Assert.Equal(new Vector2(2, 3), material.TextureScale);
		Assert.Equal(1f, material.AlphaWeight);
	}

	[Fact]
	public void FromSource_AlphaTest_WhenTextureHasAlpha()
	{
		var cache = CreateCache(hasAlpha: true);

		var material = MaterialBuilder.FromSource(
			DefaultSource with { Texture = "A.TEX" },
			cache);

		Assert.Equal(MaterialAlphaMode.AlphaTest, material.AlphaMode);
		Assert.True(material.IsTwoSided);
	}

	[Fact]
	public void FromSource_Blend_IsTransparent()
	{
		var cache = CreateCache(hasAlpha: true);

		var material = MaterialBuilder.FromSource(
			DefaultSource with { Texture = "A.TEX", AlphaFunction = MaterialAlphaFunction.Blend },
			cache);

		Assert.Equal(MaterialAlphaMode.Transparent, material.AlphaMode);
		Assert.True(material.IsTwoSided);
	}

	[Fact]
	public void FromSource_Add_IsAdditive()
	{
		var cache = CreateCache(hasAlpha: true);

		var material = MaterialBuilder.FromSource(
			DefaultSource with { Texture = "A.TEX", AlphaFunction = MaterialAlphaFunction.Add },
			cache);

		Assert.Equal(MaterialAlphaMode.Additive, material.AlphaMode);
	}

	[Theory]
	[InlineData(MaterialAlphaFunction.Multiply, MaterialAlphaMode.Multiply)]
	[InlineData(MaterialAlphaFunction.MultiplyAlt, MaterialAlphaMode.Multiply2)]
	public void FromSource_MultiplyModes_Map(
		MaterialAlphaFunction alphaFunction,
		MaterialAlphaMode expected)
	{
		var cache = CreateCache(hasAlpha: true);

		var material = MaterialBuilder.FromSource(
			DefaultSource with { Texture = "A.TEX", AlphaFunction = alphaFunction },
			cache);

		Assert.Equal(expected, material.AlphaMode);
	}

	[Fact]
	public void FromSource_WaterGroup_OverridesAlphaMode()
	{
		var cache = CreateCache(hasAlpha: false);

		var material = MaterialBuilder.FromSource(
			DefaultSource with { Texture = "A.TEX", Group = MaterialGroup.Water },
			cache);

		Assert.Equal(MaterialAlphaMode.Water, material.AlphaMode);
		Assert.True(material.IsTwoSided);
	}

	[Fact]
	public void FromSource_AlphaTestDisabled_BecomesSolid()
	{
		var cache = CreateCache(hasAlpha: true);

		var material = MaterialBuilder.FromSource(
			DefaultSource with { Texture = "A.TEX" },
			cache,
			enableAlphaTest: false);

		Assert.Equal(MaterialAlphaMode.Solid, material.AlphaMode);
	}

	[Fact]
	public void FromSource_MissingTexture_UsesPlaceholderAndReportsOnce()
	{
		var loader = new FakeTextureLoader();
		var missing = new List<string>();
		var cache = new TextureCache(loader, missing.Add);

		var material = MaterialBuilder.FromSource(
			DefaultSource with { Texture = "Missing.TGA" },
			cache);

		Assert.Same(cache.Placeholder, material.TextureSource);
		Assert.Equal("Missing.TGA", material.TextureName);
		Assert.Equal(MaterialAlphaMode.Solid, material.AlphaMode);
		Assert.Single(missing);
		Assert.Single(cache.MissingTextures);
	}

	[Fact]
	public void FromSource_EmptyTexture_LeavesTextureNull()
	{
		var cache = CreateCache(hasAlpha: false);

		var material = MaterialBuilder.FromSource(DefaultSource, cache);

		Assert.Null(material.TextureSource);
		Assert.Null(material.TextureName);
		Assert.Equal(MaterialAlphaMode.Solid, material.AlphaMode);
	}

	[Fact]
	public void FromSource_ColorAlpha_MakesTransparent()
	{
		var cache = CreateCache(hasAlpha: false);
		var color = new Color(255, 255, 255, 128);

		var material = MaterialBuilder.FromSource(
			DefaultSource with { Color = color },
			cache);

		Assert.Equal(MaterialAlphaMode.Transparent, material.AlphaMode);
		Assert.InRange(material.AlphaWeight, 128f / 255f - 1e-5f, 128f / 255f + 1e-5f);
	}

	[Fact]
	public void FromSource_MapsAllRemainingFields()
	{
		var cache = CreateCache(hasAlpha: false);
		var source = DefaultSource with
		{
			Name = "metal",
			Group = MaterialGroup.Metal,
			Color = new Color(10, 20, 30, 255),
			SmoothAngle = 45f,
			TextureScale = new Vector2(4, 5),
			TextureAnimationFps = 12f,
			TextureAnimationMapping = TextureAnimationMapping.Linear,
			TextureAnimationDirection = new Vector2(0.5f, -0.25f),
			DisableCollision = true,
			DisableLightmap = true,
			DontCollapse = true,
			DetailObject = "DETAIL",
			DetailObjectScale = 2f,
			ForceOccluder = true,
			EnvironmentMapping = true,
			EnvironmentMappingStrength = 0.75f,
			WaveMode = WaveMode.Ground,
			WaveSpeed = WaveSpeed.Fast,
			WaveAmplitude = 3f,
			WaveGridSize = 64f,
			IgnoreSun = true,
			DefaultMapping = new Vector2(0.25f, 0.75f),
		};

		var material = MaterialBuilder.FromSource(source, cache);

		Assert.Equal("metal", material.Name);
		Assert.Equal(MaterialGroup.Metal, material.Group);
		Assert.Equal(new Color(10, 20, 30, 255), material.Color);
		Assert.Equal(45f, material.SmoothAngle);
		Assert.False(material.UsesLightmap);
		Assert.True(material.IgnoreSun);
		Assert.Equal(new Vector2(4, 5), material.TextureScale);
		Assert.Equal(12f, material.TextureAnimationFps);
		Assert.Equal(TextureAnimationMapping.Linear, material.TextureAnimationMapping);
		Assert.Equal(new Vector2(0.5f, -0.25f), material.TextureAnimationDirection);
		Assert.True(material.DisableCollision);
		Assert.True(material.DontCollapse);
		Assert.Equal("DETAIL", material.DetailObject);
		Assert.Equal(2f, material.DetailObjectScale);
		Assert.True(material.ForceOccluder);
		Assert.True(material.EnvironmentMapping);
		Assert.Equal(0.75f, material.EnvironmentMappingStrength);
		Assert.Equal(WaveMode.Ground, material.WaveMode);
		Assert.Equal(WaveSpeed.Fast, material.WaveSpeed);
		Assert.Equal(3f, material.WaveAmplitude);
		Assert.Equal(64f, material.WaveGridSize);
		Assert.Equal(new Vector2(0.25f, 0.75f), material.DefaultMapping);
	}

	[Fact]
	public void FromSource_RejectsNullInputs()
	{
		var cache = CreateCache(hasAlpha: false);

		Assert.Throws<ArgumentNullException>(
			() => MaterialBuilder.FromSource(null!, cache));
		Assert.Throws<ArgumentNullException>(
			() => MaterialBuilder.FromSource(DefaultSource, null!));
	}

	private static TextureCache CreateCache(bool hasAlpha)
	{
		var loader = new FakeTextureLoader();
		loader.Files["A.TEX"] = TestTextures.Create(hasAlpha);
		return new TextureCache(loader);
	}
}
