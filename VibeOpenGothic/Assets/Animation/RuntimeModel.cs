using DigitalRiseModel;
using VibeOpenGothic.Assets.Skins;

namespace VibeOpenGothic.Assets.Animation;

/// <summary>
/// A runtime model with the source MDH order preserved for callers, the
/// DigitalRiseModel bone mapping, one skin and the Gothic animation clips.
/// </summary>
public sealed class RuntimeModel : IDisposable
{
	private bool _disposed;

	public RuntimeModel(
		string name,
		DrModel model,
		SkinSkeleton skeleton,
		DrModelBone[] sourceBones,
		int[] sourceToDrBoneIndex,
		int[] drToSourceBoneIndex,
		DrSkin skin,
		IReadOnlyDictionary<string, GothicAnimationClip> clips)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(model);
		ArgumentNullException.ThrowIfNull(skeleton);
		ArgumentNullException.ThrowIfNull(sourceBones);
		ArgumentNullException.ThrowIfNull(sourceToDrBoneIndex);
		ArgumentNullException.ThrowIfNull(drToSourceBoneIndex);
		ArgumentNullException.ThrowIfNull(skin);
		ArgumentNullException.ThrowIfNull(clips);

		Name = name;
		Model = model;
		Skeleton = skeleton;
		SourceBones = sourceBones;
		SourceToDrBoneIndex = sourceToDrBoneIndex;
		DrToSourceBoneIndex = drToSourceBoneIndex;
		Skin = skin;
		Clips = clips;
	}

	public string Name { get; }

	public DrModel Model { get; }

	public SkinSkeleton Skeleton { get; }

	/// <summary>Bones in MDH order; indices into <see cref="Model.Bones"/> are in <see cref="SourceToDrBoneIndex"/>.</summary>
	public DrModelBone[] SourceBones { get; }

	public int[] SourceToDrBoneIndex { get; }

	/// <summary>Source MDH index for each DrModel bone, or -1 for a synthetic root.</summary>
	public int[] DrToSourceBoneIndex { get; }

	public DrSkin Skin { get; }

	public IReadOnlyDictionary<string, GothicAnimationClip> Clips { get; }

	public DrModelInstance CreateInstance()
	{
		return new DrModelInstance(Model);
	}

	public DrModelBone? FindBone(string name)
	{
		return Model.FindBoneByName(name);
	}

	public GothicAnimationClip? FindClip(string name)
	{
		var normalized = AnimationClipBuilder.NormalizeName(name);
		return Clips.TryGetValue(normalized, out var clip) ? clip : null;
	}

	public int MapSourceBoneIndex(int sourceBoneIndex)
	{
		if ((uint)sourceBoneIndex >= (uint)SourceToDrBoneIndex.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(sourceBoneIndex));
		}

		return SourceToDrBoneIndex[sourceBoneIndex];
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		Model.Dispose();
	}
}
