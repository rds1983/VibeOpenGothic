using DigitalRiseModel;

namespace VibeOpenGothic.Assets.Animation;

/// <summary>
/// Load-time result: the runtime asset plus source bone order and index maps.
/// The maps are only needed while building meshes and skins; callers that just
/// need the model use <see cref="Model"/>.
/// </summary>
public sealed record GothicModelBuildResult(
	DrModel Model,
	DrModelBone[] SourceBones,
	int[] SourceToDrBoneIndex,
	int[] DrToSourceBoneIndex,
	IReadOnlyList<string> Warnings);
