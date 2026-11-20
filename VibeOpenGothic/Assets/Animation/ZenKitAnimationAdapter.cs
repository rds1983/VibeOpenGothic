using System.Globalization;
using Microsoft.Xna.Framework;
using VibeOpenGothic.Data.Vfs;
using ZenKit;
using ZkAnimationFlags = ZenKit.AnimationFlags;

namespace VibeOpenGothic.Assets.Animation;

/// <summary>
/// Thin adapter from MDS animations and their MAN keyframes to the Gothic-space
/// <see cref="AnimationSource"/> DTO. No coordinate conversion happens here.
/// </summary>
public static class ZenKitAnimationAdapter
{
	private const float RootMotionThresholdCentimetres = 0.4f;

	public static AnimationLoadResult Load(
		IGameVfs vfs,
		string mdsPath,
		IModelScript script)
	{
		ArgumentNullException.ThrowIfNull(vfs);
		ArgumentException.ThrowIfNullOrWhiteSpace(mdsPath);
		ArgumentNullException.ThrowIfNull(script);

		var animations = script.Animations;
		var aliases = script.AnimationAliases;
		var stem = Path.GetFileNameWithoutExtension(mdsPath);
		var result = new List<AnimationSource>(animations.Count);
		var missing = new List<string>();
		var warnings = new List<string>();

		foreach (var animation in animations)
		{
			var manName = stem + "-" + animation.Name + ".MAN";
			var entry = vfs.Find(manName);
			if (entry is null)
			{
				missing.Add(manName);
				continue;
			}

			var readBytes = ReadBytes(vfs, entry.Path);
			var read = new Read(readBytes);
			var man = new ModelAnimation(read);
			var frames = man.FrameCount;
			var nodeIndices = man.NodeIndices;
			var samples = man.Samples;
			var nodeCount = nodeIndices.Count;

			if (frames <= 0 || nodeCount == 0 || samples.Count != frames * nodeCount)
			{
				warnings.Add(
					$"{entry.Path}: invalid sample table " +
					$"(frames={frames}, nodes={nodeCount}, samples={samples.Count}).");
				GC.KeepAlive(man);
				GC.KeepAlive(read);
				continue;
			}

			var channels = new AnimationChannelSource[nodeCount];
			for (var node = 0; node < nodeCount; node++)
			{
				var boneSamples = new AnimationSampleSource[frames];
				for (var frame = 0; frame < frames; frame++)
				{
					var sample = samples[frame * nodeCount + node];
					boneSamples[frame] = new AnimationSampleSource(
						new Vector3(sample.Position.X, sample.Position.Y, sample.Position.Z),
						new Quaternion(
							sample.Rotation.X,
							sample.Rotation.Y,
							sample.Rotation.Z,
							sample.Rotation.W));
				}

				channels[node] = new AnimationChannelSource(nodeIndices[node], boneSamples);
			}

			var (rootMotion, hasRootMotion) = ComputeRootMotion(nodeIndices, samples, frames, nodeCount);
			var events = ReadEvents(animation, out var hasComboWindow);

			result.Add(new AnimationSource(
				animation.Name,
				string.IsNullOrWhiteSpace(man.Name) ? animation.Name : man.Name,
				string.IsNullOrWhiteSpace(animation.Next) ? null : animation.Next,
				animation.Layer,
				man.Fps,
				frames,
				animation.BlendIn,
				animation.BlendOut,
				ToFlags(animation.Flags),
				animation.Direction == AnimationDirection.Backward,
				animation.FirstFrame,
				animation.LastFrame,
				hasComboWindow,
				rootMotion,
				hasRootMotion,
				channels,
				events));

			GC.KeepAlive(man);
			GC.KeepAlive(read);
		}

		var aliasSources = new AnimationAliasSource[aliases.Count];
		for (var index = 0; index < aliases.Count; index++)
		{
			var alias = aliases[index];
			aliasSources[index] = new AnimationAliasSource(
				alias.Name,
				alias.Alias,
				string.IsNullOrWhiteSpace(alias.Next) ? null : alias.Next,
				alias.Layer,
				alias.BlendIn,
				alias.BlendOut,
				ToFlags(alias.Flags),
				alias.Direction == AnimationDirection.Backward);
		}

		return new AnimationLoadResult(result, aliasSources, missing, warnings);
	}

	private static (Vector3 Delta, bool HasMotion) ComputeRootMotion(
		IReadOnlyList<int> nodeIndices,
		IReadOnlyList<AnimationSample> samples,
		int frames,
		int nodeCount)
	{
		if (nodeIndices.Count == 0 || nodeIndices[0] != 0)
		{
			return (Vector3.Zero, false);
		}

		var first = ToXna(samples[0].Position);
		var last = ToXna(samples[(frames - 1) * nodeCount].Position);
		var hasMotion = false;
		for (var frame = 0; frame < frames; frame++)
		{
			var position = ToXna(samples[frame * nodeCount].Position);
			if (Vector3.DistanceSquared(position, first) >
				RootMotionThresholdCentimetres * RootMotionThresholdCentimetres)
			{
				hasMotion = true;
				break;
			}
		}

		return (last - first, hasMotion);
	}

	private static IReadOnlyList<AnimationEventSource> ReadEvents(
		IAnimation animation,
		out bool hasComboWindow)
	{
		hasComboWindow = false;
		var events = new List<AnimationEventSource>();
		var tags = animation.EventTags;
		foreach (var tag in tags)
		{
			if (tag.Type == EventType.ComboWindow)
			{
				hasComboWindow = true;
			}

			var name = tag.Type.ToString();
			if (!string.IsNullOrWhiteSpace(tag.Item))
			{
				name += ":" + tag.Item;
			}

			events.Add(new AnimationEventSource(
				GothicAnimationEventKind.Tag,
				tag.Frame,
				name,
				tag.Frames));
		}

		foreach (var sound in animation.SoundEffects)
		{
			events.Add(new AnimationEventSource(
				GothicAnimationEventKind.Sound,
				sound.Frame,
				sound.Name ?? string.Empty,
				Array.Empty<int>()));
		}

		foreach (var sound in animation.SoundEffectsGround)
		{
			events.Add(new AnimationEventSource(
				GothicAnimationEventKind.GroundSound,
				sound.Frame,
				sound.Name ?? string.Empty,
				Array.Empty<int>()));
		}

		foreach (var particle in animation.ParticleEffects)
		{
			events.Add(new AnimationEventSource(
				GothicAnimationEventKind.Particle,
				particle.Frame,
				particle.Name ?? string.Empty,
				Array.Empty<int>()));
		}

		foreach (var stop in animation.ParticleEffectsStop)
		{
			events.Add(new AnimationEventSource(
				GothicAnimationEventKind.ParticleStop,
				stop.Frame,
				stop.Index.ToString(CultureInfo.InvariantCulture),
				Array.Empty<int>()));
		}

		foreach (var morph in animation.MorphAnimations)
		{
			events.Add(new AnimationEventSource(
				GothicAnimationEventKind.Morph,
				morph.Frame,
				morph.Animation ?? string.Empty,
				Array.Empty<int>()));
		}

		foreach (var tremor in animation.CameraTremors)
		{
			events.Add(new AnimationEventSource(
				GothicAnimationEventKind.CameraTremor,
				tremor.Frame,
				$"{tremor.Field1},{tremor.Field2},{tremor.Field3},{tremor.Field4}",
				Array.Empty<int>()));
		}

		return events;
	}

	private static GothicAnimationFlags ToFlags(ZkAnimationFlags flags)
	{
		var result = GothicAnimationFlags.None;
		if ((flags & ZkAnimationFlags.Move) != 0)
		{
			result |= GothicAnimationFlags.Move;
		}

		if ((flags & ZkAnimationFlags.Rotate) != 0)
		{
			result |= GothicAnimationFlags.Rotate;
		}

		if ((flags & ZkAnimationFlags.Queue) != 0)
		{
			result |= GothicAnimationFlags.Queue;
		}

		if ((flags & ZkAnimationFlags.Fly) != 0)
		{
			result |= GothicAnimationFlags.Fly;
		}

		if ((flags & ZkAnimationFlags.Idle) != 0)
		{
			result |= GothicAnimationFlags.Idle;
		}

		if ((flags & ZkAnimationFlags.InPlace) != 0)
		{
			result |= GothicAnimationFlags.InPlace;
		}

		return result;
	}

	private static Vector3 ToXna(System.Numerics.Vector3 value)
	{
		return new Vector3(value.X, value.Y, value.Z);
	}

	private static byte[] ReadBytes(IGameVfs vfs, string name)
	{
		using var stream = vfs.OpenRead(name);
		using var memory = new MemoryStream();
		stream.CopyTo(memory);
		return memory.ToArray();
	}
}
