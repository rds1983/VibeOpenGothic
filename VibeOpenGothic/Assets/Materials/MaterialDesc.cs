using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Textures;

namespace VibeOpenGothic.Assets.Materials;

/// <summary>
/// CPU-side material description used by the renderer. Contains no ZenKit or
/// GraphicsDevice types.
/// </summary>
public sealed record MaterialDesc(
	string Name,
	MaterialGroup Group,
	Color Color,
	string? TextureName,
	TextureData? Texture,
	Vector2 TextureScale,
	MaterialAlphaMode AlphaMode,
	float AlphaWeight,
	bool IsTwoSided,
	bool UsesLightmap,
	bool IgnoreSun,
	float SmoothAngle,
	bool DisableCollision,
	bool DontCollapse,
	bool ForceOccluder,
	string? DetailObject,
	float DetailObjectScale,
	bool EnvironmentMapping,
	float EnvironmentMappingStrength,
	float TextureAnimationFps,
	TextureAnimationMapping TextureAnimationMapping,
	Vector2 TextureAnimationDirection,
	WaveMode WaveMode,
	WaveSpeed WaveSpeed,
	float WaveAmplitude,
	float WaveGridSize,
	Vector2 DefaultMapping)
{
	public static MaterialDesc Default { get; } = new(
		string.Empty,
		MaterialGroup.Undefined,
		Microsoft.Xna.Framework.Color.White,
		null,
		null,
		Vector2.One,
		MaterialAlphaMode.Solid,
		1f,
		false,
		true,
		false,
		0f,
		false,
		false,
		false,
		null,
		0f,
		false,
		0f,
		0f,
		TextureAnimationMapping.None,
		Vector2.Zero,
		WaveMode.None,
		WaveSpeed.None,
		0f,
		0f,
		Vector2.One);
}
