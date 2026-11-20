namespace VibeOpenGothic.Assets.Animation;

/// <summary>An MDS <c>aniAlias</c> entry; it reuses the aliased animation's samples.</summary>
public sealed record AnimationAliasSource(
	string Name,
	string Alias,
	string? Next,
	int Layer,
	float BlendInSeconds,
	float BlendOutSeconds,
	GothicAnimationFlags Flags,
	bool Reverse);
