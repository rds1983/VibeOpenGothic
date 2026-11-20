namespace VibeOpenGothic.Assets.Animation;

public sealed record AnimationModelLoadResult(
	RuntimeModel Model,
	AnimationLoadResult Animations,
	IReadOnlyList<string> Warnings);
