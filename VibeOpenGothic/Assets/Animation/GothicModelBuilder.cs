using DigitalRiseModel;
using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Skins;

namespace VibeOpenGothic.Assets.Animation;

/// <summary>
/// Builds a DigitalRiseModel skeleton and clips from the converted MDH skeleton
/// and MDS/MAN animation sources. The returned asset is a plain <see cref="DrModel"/>;
/// the source bone order and index maps are only needed while meshes and skins
/// are created.
/// </summary>
public static class GothicModelBuilder
{
	public static GothicModelBuildResult Build(
		string name,
		SkinSkeleton skeleton,
		AnimationLoadResult animations,
		DrMesh? rootMesh = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(skeleton);
		ArgumentNullException.ThrowIfNull(animations);

		if (skeleton.BoneCount == 0)
		{
			throw new InvalidDataException($"Model '{name}' has no bones.");
		}

		var warnings = new List<string>();
		warnings.AddRange(skeleton.Warnings);
		warnings.AddRange(animations.Warnings);

		var boneCount = skeleton.BoneCount;
		var parents = new int[boneCount];
		for (var index = 0; index < boneCount; index++)
		{
			var parent = skeleton.Bones[index].ParentIndex;
			if (parent >= boneCount || parent == index)
			{
				warnings.Add(
					$"Bone '{skeleton.Bones[index].Name}' has invalid parent {parent}; " +
					"treating it as a root.");
				parent = -1;
			}

			parents[index] = parent;
		}

		BreakCycles(parents, skeleton, warnings);
		var roots = Enumerable.Range(0, boneCount).Where(index => parents[index] < 0).ToList();
		if (roots.Count == 0)
		{
			parents[0] = -1;
			roots.Add(0);
			warnings.Add("Skeleton has no root after cycle repair; using bone 0 as root.");
		}

		var children = new List<int>[boneCount];
		for (var index = 0; index < boneCount; index++)
		{
			children[index] = new List<int>();
		}

		for (var index = 0; index < boneCount; index++)
		{
			if (parents[index] >= 0)
			{
				children[parents[index]].Add(index);
			}
		}

		var bones = new DrModelBone[boneCount];
		for (var index = 0; index < boneCount; index++)
		{
			var bone = skeleton.Bones[index];
			var mesh = roots.Count == 1 && roots[0] == index ? rootMesh : null;
			bones[index] = new DrModelBone(bone.Name, mesh)
			{
				DefaultPose = ToDefaultPose(bone.Local, bone.Name, warnings),
			};
		}

		for (var index = 0; index < boneCount; index++)
		{
			bones[index].Children = children[index].Select(child => bones[child]).ToArray();
		}

		DrModelBone root;
		if (roots.Count == 1)
		{
			root = bones[roots[0]];
		}
		else
		{
			var syntheticRoot = new DrModelBone("<ROOT>", rootMesh);
			syntheticRoot.Children = roots.Select(index => bones[index]).ToArray();
			root = syntheticRoot;
			warnings.Add($"Skeleton has {roots.Count} roots; added a synthetic root bone.");
		}

		var model = new DrModel(root);

		var sourceToDr = new int[boneCount];
		var identity = true;
		for (var index = 0; index < boneCount; index++)
		{
			sourceToDr[index] = bones[index].Index;
			identity &= sourceToDr[index] == index;
		}

		if (!identity)
		{
			warnings.Add(
				"MDH bone order is not depth-first; mesh weights and clip channels " +
				"use the source-to-DrModel bone map.");
		}

		var drToSource = Enumerable.Repeat(-1, model.Bones.Length).ToArray();
		for (var index = 0; index < boneCount; index++)
		{
			drToSource[sourceToDr[index]] = index;
		}

		var clips = new Dictionary<string, GothicAnimationClip>(StringComparer.OrdinalIgnoreCase);
		var baseClips = new Dictionary<string, GothicAnimationClip>(StringComparer.OrdinalIgnoreCase);

		foreach (var source in animations.Animations)
		{
			var clip = AnimationClipBuilder.Build(source, sourceToDr, warnings);
			if (clip is null)
			{
				continue;
			}

			AddClip(clip, clips, baseClips, warnings, isAlias: false);
		}

		foreach (var alias in animations.Aliases)
		{
			var aliasName = AnimationClipBuilder.NormalizeName(alias.Alias);
			if (!baseClips.TryGetValue(aliasName, out var baseClip))
			{
				warnings.Add($"Animation alias '{alias.Name}' targets missing '{alias.Alias}'.");
				continue;
			}

			var clip = AnimationClipBuilder.BuildAlias(alias, baseClip, warnings);
			if (clip is null)
			{
				continue;
			}

			AddClip(clip, clips, baseClips, warnings, isAlias: true);
		}

		var modelAnimations = new Dictionary<string, DigitalRiseModel.Animation.AnimationClip>(
			StringComparer.OrdinalIgnoreCase);
		foreach (var pair in clips)
		{
			modelAnimations[pair.Key] = pair.Value;
		}

		model.Animations = modelAnimations;

		return new GothicModelBuildResult(
			model,
			bones,
			sourceToDr,
			drToSource,
			warnings);
	}

	private static void BreakCycles(int[] parents, SkinSkeleton skeleton, ICollection<string> warnings)
	{
		var state = new byte[parents.Length];

		for (var index = 0; index < parents.Length; index++)
		{
			Visit(index);
		}

		void Visit(int index)
		{
			if (state[index] == 2)
			{
				return;
			}

			if (state[index] == 1)
			{
				parents[index] = -1;
				warnings.Add(
					$"Cycle in skeleton hierarchy at bone '{skeleton.Bones[index].Name}'; " +
					"treated as a root.");
				return;
			}

			state[index] = 1;
			var parent = parents[index];
			if (parent >= 0)
			{
				Visit(parent);
			}

			state[index] = 2;
		}
	}

	private static SrtTransform ToDefaultPose(
		Matrix matrix,
		string boneName,
		ICollection<string> warnings)
	{
		if (!matrix.Decompose(out var scale, out var rotation, out var translation) ||
			!IsFinite(scale) ||
			!IsFinite(rotation) ||
			!IsFinite(translation))
		{
			warnings.Add($"Bone '{boneName}' has a non-decomposable bind transform; using identity.");
			return SrtTransform.Identity;
		}

		return new SrtTransform(translation, rotation, scale);
	}

	private static void AddClip(
		GothicAnimationClip clip,
		Dictionary<string, GothicAnimationClip> clips,
		Dictionary<string, GothicAnimationClip> baseClips,
		ICollection<string> warnings,
		bool isAlias)
	{
		Add(clip.Name, clip);

		var dataName = AnimationClipBuilder.NormalizeName(clip.DataName);
		if (!isAlias &&
			dataName.Length > 0 &&
			!dataName.Equals(clip.Name, StringComparison.OrdinalIgnoreCase))
		{
			Add(dataName, clip);
		}

		void Add(string key, GothicAnimationClip value)
		{
			if (clips.TryAdd(key, value))
			{
				if (!isAlias)
				{
					baseClips.TryAdd(key, value);
				}
			}
			else
			{
				warnings.Add(
					$"Animation name '{key}' is duplicated; keeping " +
					$"'{clips[key].SourceName}' and ignoring '{value.SourceName}'.");
			}
		}
	}

	private static bool IsFinite(Vector3 value)
	{
		return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
	}

	private static bool IsFinite(Quaternion value)
	{
		return float.IsFinite(value.X) && float.IsFinite(value.Y) &&
			   float.IsFinite(value.Z) && float.IsFinite(value.W);
	}
}
