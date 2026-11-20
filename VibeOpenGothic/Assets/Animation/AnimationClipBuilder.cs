using DigitalRiseModel;
using DigitalRiseModel.Animation;
using Microsoft.Xna.Framework;
using VibeOpenGothic.Core;

namespace VibeOpenGothic.Assets.Animation;

/// <summary>
/// Converts Gothic-space animation sources into DigitalRiseModel clips.
/// Keyframes are stored once per MAN frame; looped clips get a closing key that
/// repeats frame zero so the last interval interpolates back to the start.
/// </summary>
public static class AnimationClipBuilder
{
	public static GothicAnimationClip? Build(
		AnimationSource source,
		IReadOnlyList<int> sourceToDrBone,
		ICollection<string> warnings)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(sourceToDrBone);
		ArgumentNullException.ThrowIfNull(warnings);

		if (!float.IsFinite(source.Fps) || source.Fps <= 0f)
		{
			warnings.Add($"Animation '{source.Name}' has invalid FPS {source.Fps}; skipped.");
			return null;
		}

		if (source.FrameCount <= 0)
		{
			warnings.Add($"Animation '{source.Name}' has no frames; skipped.");
			return null;
		}

		var name = NormalizeName(source.Name);
		if (name.Length == 0)
		{
			name = NormalizeName(source.DataName);
		}

		if (name.Length == 0)
		{
			warnings.Add("Animation has no name; skipped.");
			return null;
		}

		var dataName = NormalizeName(source.DataName);
		var next = NormalizeString(source.Next);
		var isLoop = !source.HasComboWindow &&
					 next is not null &&
					 (next == name || next == dataName);

		var keyCount = source.FrameCount + (isLoop ? 1 : 0);
		var channels = new List<AnimationChannel>(source.Channels.Count);

		foreach (var channel in source.Channels)
		{
			if (channel.Samples.Count < source.FrameCount)
			{
				warnings.Add(
					$"Animation '{name}' bone {channel.BoneIndex} has " +
					$"{channel.Samples.Count}/{source.FrameCount} samples; skipped.");
				continue;
			}

			if ((uint)channel.BoneIndex >= (uint)sourceToDrBone.Count)
			{
				warnings.Add(
					$"Animation '{name}' references missing bone {channel.BoneIndex}; skipped.");
				continue;
			}

			var drBone = sourceToDrBone[channel.BoneIndex];
			if (drBone < 0)
			{
				warnings.Add(
					$"Animation '{name}' references unmapped bone {channel.BoneIndex}; skipped.");
				continue;
			}

			var keyframes = new AnimationChannelKeyframe[keyCount];
			for (var frame = 0; frame < keyCount; frame++)
			{
				var sample = channel.Samples[frame % source.FrameCount];
				keyframes[frame] = new AnimationChannelKeyframe(
					FrameToTime(frame, source.Fps),
					new SrtTransform(
						CoordinateSystem.ToEnginePoint(sample.Translation),
						CoordinateSystem.ToEngineAnimationQuaternion(sample.Rotation),
						Vector3.One));
			}

			channels.Add(new AnimationChannel(drBone, keyframes)
			{
				TranslationMode = InterpolationMode.Linear,
				RotationMode = InterpolationMode.Spherical,
				ScaleMode = InterpolationMode.None,
			});
		}

		if (channels.Count == 0)
		{
			warnings.Add($"Animation '{name}' has no valid channels; skipped.");
			return null;
		}

		var clip = new AnimationClip(name, FrameToTime(source.FrameCount, source.Fps), channels.ToArray());
		var events = new GothicAnimationEvent[source.Events.Count];
		for (var index = 0; index < source.Events.Count; index++)
		{
			var sourceEvent = source.Events[index];
			events[index] = new GothicAnimationEvent(
				sourceEvent.Kind,
				sourceEvent.Frame,
				FrameToTime(sourceEvent.Frame, source.Fps),
				sourceEvent.Name,
				sourceEvent.Frames);
		}

		return new GothicAnimationClip(
			name,
			source.Name,
			source.DataName,
			clip,
			source.FrameCount,
			source.Fps,
			source.FirstFrame,
			source.LastFrame,
			source.Layer,
			source.Flags,
			SecondsToTimeSpan(source.BlendInSeconds),
			SecondsToTimeSpan(source.BlendOutSeconds),
			source.Reverse,
			isLoop,
			source.HasComboWindow,
			next,
			CoordinateSystem.ToEnginePoint(source.RootMotionDelta),
			source.HasRootMotion,
			events);
	}

	public static GothicAnimationClip? BuildAlias(
		AnimationAliasSource alias,
		GothicAnimationClip baseClip,
		ICollection<string> warnings)
	{
		ArgumentNullException.ThrowIfNull(alias);
		ArgumentNullException.ThrowIfNull(baseClip);
		ArgumentNullException.ThrowIfNull(warnings);

		var name = NormalizeName(alias.Name);
		if (name.Length == 0)
		{
			warnings.Add("Animation alias has no name; skipped.");
			return null;
		}

		var next = NormalizeString(alias.Next);
		var isLoop = !baseClip.HasComboWindow && next == name;

		// Channels are already remapped; share them with the alias clip.
		var clip = new AnimationClip(name, baseClip.Duration, baseClip.Clip.Channels);

		return new GothicAnimationClip(
			name,
			alias.Name,
			baseClip.DataName,
			clip,
			baseClip.FrameCount,
			baseClip.Fps,
			baseClip.FirstFrame,
			baseClip.LastFrame,
			alias.Layer,
			alias.Flags,
			SecondsToTimeSpan(alias.BlendInSeconds),
			SecondsToTimeSpan(alias.BlendOutSeconds),
			alias.Reverse,
			isLoop,
			baseClip.HasComboWindow,
			next,
			baseClip.RootMotionDelta,
			baseClip.HasRootMotion,
			baseClip.Events);
	}

	public static string NormalizeName(string? name)
	{
		return name is null ? string.Empty : name.Trim().ToUpperInvariant();
	}

	private static string? NormalizeString(string? name)
	{
		var normalized = NormalizeName(name);
		return normalized.Length == 0 ? null : normalized;
	}

	private static TimeSpan FrameToTime(int frame, float fps)
	{
		var ticks = (long)Math.Round(
			frame * (double)TimeSpan.TicksPerSecond / fps,
			MidpointRounding.AwayFromZero);
		return TimeSpan.FromTicks(ticks);
	}

	private static TimeSpan SecondsToTimeSpan(float seconds)
	{
		return TimeSpan.FromSeconds(float.IsFinite(seconds) ? Math.Max(0f, seconds) : 0f);
	}
}
