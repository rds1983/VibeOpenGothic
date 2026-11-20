namespace VibeOpenGothic.Assets.Materials;

/// <summary>Gothic material surface groups. Values mirror the game data.</summary>
public enum MaterialGroup
{
	Undefined = 0,
	Metal = 1,
	Stone = 2,
	Wood = 3,
	Earth = 4,
	Water = 5,
	Snow = 6,
}

/// <summary>Gothic alpha function. Values mirror the game data.</summary>
public enum MaterialAlphaFunction
{
	Default = 0,
	None = 1,
	Blend = 2,
	Add = 3,
	Subtract = 4,
	Multiply = 5,
	MultiplyAlt = 6,
}

/// <summary>Render mode derived from the alpha function, group and texture.</summary>
public enum MaterialAlphaMode
{
	Solid,
	AlphaTest,
	Transparent,
	Additive,
	Multiply,
	Multiply2,
	Water,
}

public enum TextureAnimationMapping
{
	None = 0,
	Linear = 1,
}

public enum WaveMode
{
	None = 0,
	GroundAmbient = 1,
	Ground = 2,
	WallAmbient = 3,
	Wall = 4,
	Environment = 5,
	WindAmbient = 6,
	Wind = 7,
}

public enum WaveSpeed
{
	None = 0,
	Slow = 1,
	Normal = 2,
	Fast = 3,
}
