using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Assets.Skins;

public sealed record SkinBone(
	string Name,
	int ParentIndex,
	Matrix Local,
	Matrix BindGlobal,
	Matrix InverseBindGlobal);

public sealed class SkinSkeleton
{
	public SkinSkeleton(IReadOnlyList<SkinBone> bones, IReadOnlyList<string> warnings)
	{
		ArgumentNullException.ThrowIfNull(bones);
		ArgumentNullException.ThrowIfNull(warnings);

		Bones = bones;
		Warnings = warnings;

		var bindGlobals = new Matrix[bones.Count];
		var locals = new Matrix[bones.Count];
		var inverseBindGlobals = new Matrix[bones.Count];
		for (var index = 0; index < bones.Count; index++)
		{
			locals[index] = bones[index].Local;
			bindGlobals[index] = bones[index].BindGlobal;
			inverseBindGlobals[index] = bones[index].InverseBindGlobal;
		}

		Locals = locals;
		BindGlobals = bindGlobals;
		InverseBindGlobals = inverseBindGlobals;
	}

	public IReadOnlyList<SkinBone> Bones { get; }

	public int BoneCount => Bones.Count;

	public IReadOnlyList<Matrix> Locals { get; }

	public IReadOnlyList<Matrix> BindGlobals { get; }

	public IReadOnlyList<Matrix> InverseBindGlobals { get; }

	public IReadOnlyList<string> Warnings { get; }

	/// <summary>MonoGame's SkinnedEffect supports at most 72 bones.</summary>
	public bool ExceedsSkinnedEffectLimit => BoneCount > 72;

	public int FindBone(string name)
	{
		for (var index = 0; index < Bones.Count; index++)
		{
			if (string.Equals(Bones[index].Name, name, StringComparison.Ordinal))
			{
				return index;
			}
		}

		return -1;
	}
}
