namespace VibeOpenGothic.Assets.Animation;

/// <summary>One animated bone and its per-frame samples in Gothic space.</summary>
public sealed record AnimationChannelSource(
	int BoneIndex,
	IReadOnlyList<AnimationSampleSource> Samples);
