using VibeOpenGothic.Assets.Textures;

namespace VibeOpenGothic.Assets.Materials;

public static class MaterialBuilder
{
	public static GothicMaterial FromSource(
		MaterialSource source,
		TextureCache textures,
		bool enableAlphaTest = true)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(textures);

		var texture = textures.Get(source.Texture);
		var alphaMode = MapAlphaMode(source, texture, enableAlphaTest);
		var twoSided = alphaMode != MaterialAlphaMode.Solid;
		var textureName = string.IsNullOrWhiteSpace(source.Texture) ? null : source.Texture;

		return new GothicMaterial(
			source.Name,
			source.Group,
			source.Color,
			textureName,
			texture,
			source.TextureScale,
			alphaMode,
			source.Color.A / 255f,
			twoSided,
			!source.DisableLightmap,
			source.IgnoreSun,
			source.SmoothAngle,
			source.DisableCollision,
			source.DontCollapse,
			source.ForceOccluder,
			string.IsNullOrWhiteSpace(source.DetailObject) ? null : source.DetailObject,
			source.DetailObjectScale,
			source.EnvironmentMapping,
			source.EnvironmentMappingStrength,
			source.TextureAnimationFps,
			source.TextureAnimationMapping,
			source.TextureAnimationDirection,
			source.WaveMode,
			source.WaveSpeed,
			source.WaveAmplitude,
			source.WaveGridSize,
			source.DefaultMapping);
	}

	private static MaterialAlphaMode MapAlphaMode(
		MaterialSource source,
		TextureData? texture,
		bool enableAlphaTest)
	{
		var mode = source.AlphaFunction switch
		{
			MaterialAlphaFunction.Blend => MaterialAlphaMode.Transparent,
			MaterialAlphaFunction.Add or MaterialAlphaFunction.Subtract => MaterialAlphaMode.Additive,
			MaterialAlphaFunction.Multiply => MaterialAlphaMode.Multiply,
			MaterialAlphaFunction.MultiplyAlt => MaterialAlphaMode.Multiply2,
			_ => MaterialAlphaMode.AlphaTest,
		};

		if (source.Group == MaterialGroup.Water)
		{
			mode = MaterialAlphaMode.Water;
		}

		if (mode is MaterialAlphaMode.AlphaTest or MaterialAlphaMode.Transparent &&
			source.Color.A == 255 &&
			(texture is null || !texture.HasAlpha))
		{
			mode = MaterialAlphaMode.Solid;
		}

		if (mode == MaterialAlphaMode.AlphaTest && !enableAlphaTest)
		{
			mode = MaterialAlphaMode.Solid;
		}

		if (source.Color.A != 255 &&
			mode is MaterialAlphaMode.Solid or MaterialAlphaMode.AlphaTest)
		{
			mode = MaterialAlphaMode.Transparent;
		}

		return mode;
	}
}
