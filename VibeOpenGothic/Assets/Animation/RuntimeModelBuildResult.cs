namespace VibeOpenGothic.Assets.Animation;

public sealed record RuntimeModelBuildResult(
	RuntimeModel Model,
	IReadOnlyList<string> Warnings);
