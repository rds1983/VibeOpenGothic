namespace VibeOpenGothic.Assets.Animation;

public sealed record AnimationLoadResult(
	IReadOnlyList<AnimationSource> Animations,
	IReadOnlyList<AnimationAliasSource> Aliases,
	IReadOnlyList<string> MissingAnimations,
	IReadOnlyList<string> Warnings);
