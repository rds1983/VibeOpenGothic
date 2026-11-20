using DigitalRiseModel.Animation;
using Microsoft.Xna.Framework;
using DrAnimationFlags = DigitalRiseModel.Animation.AnimationFlags;

namespace VibeOpenGothic.Assets.Animation;

/// <summary>
/// A DigitalRiseModel clip plus the Gothic script metadata needed by the
/// animation solver: loop/transition state, next animation, blend times, events
/// and root motion. Names are normalized to upper case for lookup.
/// </summary>
public sealed class GothicAnimationClip
{
	public GothicAnimationClip(
		string name,
		string sourceName,
		string dataName,
		AnimationClip clip,
		int frameCount,
		float fps,
		int firstFrame,
		int lastFrame,
		int layer,
		GothicAnimationFlags flags,
		TimeSpan blendIn,
		TimeSpan blendOut,
		bool reverse,
		bool isLoop,
		bool hasComboWindow,
		string? next,
		Vector3 rootMotionDelta,
		bool hasRootMotion,
		IReadOnlyList<GothicAnimationEvent> events)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(clip);
		ArgumentNullException.ThrowIfNull(events);

		Name = name;
		SourceName = sourceName;
		DataName = dataName;
		Clip = clip;
		FrameCount = frameCount;
		Fps = fps;
		FirstFrame = firstFrame;
		LastFrame = lastFrame;
		Layer = layer;
		Flags = flags;
		BlendIn = blendIn;
		BlendOut = blendOut;
		Reverse = reverse;
		IsLoop = isLoop;
		HasComboWindow = hasComboWindow;
		Next = next;
		RootMotionDelta = rootMotionDelta;
		HasRootMotion = hasRootMotion;
		Events = events;

		var playback = DrAnimationFlags.None;
		if (isLoop)
		{
			playback |= DrAnimationFlags.Looped;
		}

		if (reverse)
		{
			playback |= DrAnimationFlags.PlayBackwards;
		}

		PlaybackFlags = playback;
	}

	public string Name { get; }

	public string SourceName { get; }

	public string DataName { get; }

	public AnimationClip Clip { get; }

	public TimeSpan Duration => Clip.Duration;

	public int FrameCount { get; }

	public float Fps { get; }

	public int FirstFrame { get; }

	public int LastFrame { get; }

	public int Layer { get; }

	public GothicAnimationFlags Flags { get; }

	public TimeSpan BlendIn { get; }

	public TimeSpan BlendOut { get; }

	public bool Reverse { get; }

	public bool IsLoop { get; }

	public bool IsTransition => !IsLoop;

	public bool HasComboWindow { get; }

	public string? Next { get; }

	public Vector3 RootMotionDelta { get; }

	public bool HasRootMotion { get; }

	public IReadOnlyList<GothicAnimationEvent> Events { get; }

	public DrAnimationFlags PlaybackFlags { get; }
}
