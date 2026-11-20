namespace VibeOpenGothic.Assets.Animation;

public sealed record GothicAnimationEvent(
	GothicAnimationEventKind Kind,
	int Frame,
	TimeSpan Time,
	string Name,
	IReadOnlyList<int> Frames);
