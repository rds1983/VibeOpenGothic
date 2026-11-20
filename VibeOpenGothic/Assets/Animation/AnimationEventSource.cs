namespace VibeOpenGothic.Assets.Animation;

/// <summary>A frame-indexed MDS event. Times are computed when the clip is built.</summary>
public sealed record AnimationEventSource(
	GothicAnimationEventKind Kind,
	int Frame,
	string Name,
	IReadOnlyList<int> Frames);
