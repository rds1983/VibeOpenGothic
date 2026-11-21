using DigitalRiseModel.Animation;
using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Assets.Animation;

/// <summary>
/// Gothic-specific animation metadata stored in <see cref="AnimationClip.Tag"/>.
/// The runtime asset remains a plain DigitalRiseModel clip; this tag adds the
/// source names, loop/transition flags, blend times, events and root motion.
/// </summary>
public sealed record AnimationClipTag(
	string SourceName,
	string DataName,
	int FrameCount,
	float Fps,
	int FirstFrame,
	int LastFrame,
	int Layer,
	GothicAnimationFlags Flags,
	TimeSpan BlendIn,
	TimeSpan BlendOut,
	bool Reverse,
	bool IsLoop,
	bool HasComboWindow,
	string? Next,
	Vector3 RootMotionDelta,
	bool HasRootMotion,
	IReadOnlyList<GothicAnimationEvent> Events,
	AnimationFlags PlaybackFlags)
{
	public bool IsTransition => !IsLoop;
}
