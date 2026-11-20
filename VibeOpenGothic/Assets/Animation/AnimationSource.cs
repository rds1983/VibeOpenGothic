using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Assets.Animation;

/// <summary>
/// An MDS animation plus the MAN samples, in Gothic space. The builder converts
/// samples to engine space and creates a DigitalRiseModel clip.
/// </summary>
public sealed record AnimationSource(
	string Name,
	string DataName,
	string? Next,
	int Layer,
	float Fps,
	int FrameCount,
	float BlendInSeconds,
	float BlendOutSeconds,
	GothicAnimationFlags Flags,
	bool Reverse,
	int FirstFrame,
	int LastFrame,
	bool HasComboWindow,
	Vector3 RootMotionDelta,
	bool HasRootMotion,
	IReadOnlyList<AnimationChannelSource> Channels,
	IReadOnlyList<AnimationEventSource> Events);
