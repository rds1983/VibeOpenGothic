using DigitalRiseModel;

namespace VibeOpenGothic.Assets.Animation;

public sealed record AnimationModelLoadResult(
	DrModel Model,
	AnimationLoadResult Animations,
	IReadOnlyList<string> Warnings);
