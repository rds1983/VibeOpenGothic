using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Assets.Materials;

/// <summary>
/// Engine-side material input, independent of ZenKit. Values are mapped from
/// <c>ZenKit.Material</c> by <c>ZenKitMaterialAdapter</c>.
/// </summary>
public sealed record MaterialSource(
	string Name,
	MaterialGroup Group,
	Color Color,
	float SmoothAngle,
	string Texture,
	Vector2 TextureScale,
	float TextureAnimationFps,
	TextureAnimationMapping TextureAnimationMapping,
	Vector2 TextureAnimationDirection,
	bool DisableCollision,
	bool DisableLightmap,
	bool DontCollapse,
	string DetailObject,
	float DetailObjectScale,
	bool ForceOccluder,
	bool EnvironmentMapping,
	float EnvironmentMappingStrength,
	WaveMode WaveMode,
	WaveSpeed WaveSpeed,
	float WaveAmplitude,
	float WaveGridSize,
	bool IgnoreSun,
	MaterialAlphaFunction AlphaFunction,
	Vector2 DefaultMapping);
