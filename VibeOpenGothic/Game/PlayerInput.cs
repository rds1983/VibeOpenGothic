using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Game;

public readonly record struct PlayerInput(
	float Forward,
	float Right,
	float Vertical,
	bool Run,
	bool Sneak,
	float LookX,
	float LookY)
{
	public static PlayerInput None => default;
}

public enum CameraMode
{
	FirstPerson,
	ThirdPerson,
	Free,
}

public readonly record struct CameraPose(Vector3 Position, Vector3 Target);
