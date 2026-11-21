using System.Diagnostics;
using DigitalRiseModel;
using DigitalRiseModel.Animation;
using Microsoft.Xna.Framework;
using VibeOpenGothic.Data.Installs;
using VibeOpenGothic.Data.Vfs;

namespace VibeOpenGothic.Assets.Animation;

/// <summary>
/// Developer diagnostic for <c>--check-animations</c>: loads every MDS, builds
/// its runtime skeleton and clips, and poses it at frame zero and mid-animation.
/// </summary>
public static class AnimationCheckTool
{
	public static int Run(GothicInstall install, IGameVfs vfs, Action<string>? output = null)
	{
		ArgumentNullException.ThrowIfNull(install);
		ArgumentNullException.ThrowIfNull(vfs);

		output ??= Console.WriteLine;

		var scriptCount = 0;
		var animationCount = 0;
		var loopCount = 0;
		var aliasCount = 0;
		var missingManCount = 0;
		var warningCount = 0;
		var failureCount = 0;
		var keyframeCount = 0;
		var maxBones = 0;
		var overBoneLimit = 0;
		var poseEvaluations = 0;
		var nonFiniteTransforms = 0;
		var timer = Stopwatch.StartNew();
		var missingSamples = new List<string>();
		var warningSamples = new List<string>();

		foreach (var file in vfs.EnumerateFiles())
		{
			if (!file.Name.EndsWith(".MDS", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			try
			{
				var loaded = ZenKitAnimationModelLoader.Load(vfs, file.Path);
				scriptCount++;
				missingManCount += loaded.Animations.MissingAnimations.Count;
				warningCount += loaded.Warnings.Count;

				foreach (var missing in loaded.Animations.MissingAnimations)
				{
					if (missingSamples.Count < 20)
					{
						missingSamples.Add($"{file.Path}: {missing}");
					}
				}

				foreach (var warning in loaded.Warnings)
				{
					if (warningSamples.Count < 20)
					{
						warningSamples.Add($"{file.Path}: {warning}");
					}
				}

				var model = loaded.Model;
				var clips = model.Animations.Values
					.Distinct()
					.ToList();
				var scriptKeyframes = 0;
				foreach (var clip in clips)
				{
					foreach (var channel in clip.Channels)
					{
						scriptKeyframes += channel.Keyframes.Length;
					}
				}

				animationCount += clips.Count;
				loopCount += clips.Count(clip =>
					clip.Tag is AnimationClipTag tag && tag.IsLoop);
				aliasCount += loaded.Animations.Aliases.Count;
				keyframeCount += scriptKeyframes;
				maxBones = Math.Max(maxBones, model.Bones.Length);
				if (model.Bones.Length > 72)
				{
					overBoneLimit++;
				}

				var instance = new DrModelInstance(model);
				var controller = new AnimationController(instance);
				var scriptPoses = 0;
				var scriptNonFinite = 0;

				foreach (var clip in clips)
				{
					if (clip.Duration <= TimeSpan.Zero)
					{
						continue;
					}

					controller.StartClip(
						clip,
						clip.Tag is AnimationClipTag tag
							? tag.PlaybackFlags
							: DigitalRiseModel.Animation.AnimationFlags.None);
					EvaluateAt(TimeSpan.FromTicks(clip.Duration.Ticks / 2));
					EvaluateAt(TimeSpan.Zero);

					void EvaluateAt(TimeSpan time)
					{
						controller.Time = time;
						for (var bone = 0; bone < model.Bones.Length; bone++)
						{
							var transform = instance.GetBoneGlobalTransform(bone);
							if (!IsFinite(transform))
							{
								scriptNonFinite++;
							}
						}

						scriptPoses++;
					}
				}

				poseEvaluations += scriptPoses;
				nonFiniteTransforms += scriptNonFinite;

				if (scriptCount <= 20 || scriptNonFinite > 0)
				{
					output(
						$"{file.Path}: {clips.Count} animation(s), " +
						$"{scriptKeyframes} keys, {model.Bones.Length} bones, " +
						$"{scriptPoses} pose(s), {scriptNonFinite} non-finite");
				}

				if (scriptNonFinite > 0)
				{
					failureCount++;
				}

				model.Dispose();
			}
			catch (Exception exception)
			{
				failureCount++;
				output($"FAIL {file.Path}: {exception.GetType().Name}: {exception.Message}");
			}
		}

		timer.Stop();

		output(
			$"Checked {scriptCount} MDS script(s), {animationCount} animation(s) " +
			$"({loopCount} loop, {animationCount - loopCount} transition), " +
			$"{aliasCount} alias(es).");
		output(
			$"Keyframes: {keyframeCount}; max bones {maxBones}; " +
			$"over-72 models {overBoneLimit}; missing MAN files {missingManCount}; " +
			$"warnings {warningCount}.");
		output(
			$"Posed at frame 0 and midpoint: {poseEvaluations} evaluation(s), " +
			$"{nonFiniteTransforms} non-finite transform(s), " +
			$"{failureCount} failure(s) in {timer.Elapsed.TotalSeconds:0.0}s.");

		foreach (var missing in missingSamples)
		{
			output($"MISSING {missing}");
		}

		foreach (var warning in warningSamples)
		{
			output($"WARN {warning}");
		}

		return failureCount == 0 && scriptCount > 0 ? 0 : 1;
	}

	private static bool IsFinite(Matrix matrix)
	{
		return float.IsFinite(matrix.M11) && float.IsFinite(matrix.M12) &&
			   float.IsFinite(matrix.M13) && float.IsFinite(matrix.M14) &&
			   float.IsFinite(matrix.M21) && float.IsFinite(matrix.M22) &&
			   float.IsFinite(matrix.M23) && float.IsFinite(matrix.M24) &&
			   float.IsFinite(matrix.M31) && float.IsFinite(matrix.M32) &&
			   float.IsFinite(matrix.M33) && float.IsFinite(matrix.M34) &&
			   float.IsFinite(matrix.M41) && float.IsFinite(matrix.M42) &&
			   float.IsFinite(matrix.M43) && float.IsFinite(matrix.M44);
	}
}
