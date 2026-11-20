using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Assets.Animation;

/// <summary>A single MAN keyframe in Gothic space.</summary>
public readonly record struct AnimationSampleSource(Vector3 Translation, Quaternion Rotation);
