using Microsoft.Xna.Framework;
using ZenKit;

namespace VibeOpenGothic.Assets.Materials;

public static class ZenKitMaterialAdapter
{
	public static MaterialSource FromMaterial(IMaterial material)
	{
		ArgumentNullException.ThrowIfNull(material);

		return new MaterialSource(
			material.Name ?? string.Empty,
			(MaterialGroup)material.Group,
			new Color(material.Color.R, material.Color.G, material.Color.B, material.Color.A),
			material.SmoothAngle,
			material.Texture ?? string.Empty,
			new Vector2(material.TextureScale.X, material.TextureScale.Y),
			material.TextureAnimationFps,
			(TextureAnimationMapping)material.TextureAnimationMapping,
			new Vector2(
				material.TextureAnimationMappingDirection.X,
				material.TextureAnimationMappingDirection.Y),
			material.DisableCollision,
			material.DisableLightmap,
			material.DontCollapse,
			material.DetailObject ?? string.Empty,
			material.DetailObjectScale,
			material.ForceOccluder,
			material.EnvironmentMapping,
			material.EnvironmentMappingStrength,
			(WaveMode)material.WaveMode,
			(WaveSpeed)material.WaveSpeed,
			material.WaveAmplitude,
			material.WaveGridSize,
			material.IgnoreSun,
			(MaterialAlphaFunction)material.AlphaFunction,
			new Vector2(material.DefaultMapping.X, material.DefaultMapping.Y));
	}
}
