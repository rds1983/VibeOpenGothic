namespace VibeOpenGothic.Assets.Animation;

[Flags]
public enum GothicAnimationFlags
{
	None = 0,
	Move = 1,
	Rotate = 2,
	Queue = 4,
	Fly = 8,
	Idle = 16,
	InPlace = 32,
}
