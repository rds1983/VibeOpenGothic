using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VibeOpenGothic.Assets.Textures;

namespace VibeOpenGothic.Assets.Materials;

/// <summary>
/// Material description shared by CPU-side conversion and the renderer.
/// <see cref="TextureSource"/> is the decoded texture data produced by the
/// headless loader; the rendering layer converts it to <see cref="Texture"/>
/// once and clears the source on the GPU-side copy.
/// </summary>
public sealed record GothicMaterial(
	string Name,
	MaterialGroup Group,
	Color Color,
	string? TextureName,
	TextureData? TextureSource,
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
	public Texture2D? Texture { get; init; }

	public static GothicMaterial Default { get; } = new(
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
