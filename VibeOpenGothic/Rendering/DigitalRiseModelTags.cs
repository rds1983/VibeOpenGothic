using DigitalRiseModel;
using DigitalRiseModel.Animation;
using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Models;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// Typed payloads stored in DigitalRiseModel <c>Tag</c> properties. Keeping the
/// types explicit makes the object graph readable and avoids casting anonymous
/// tuples or raw indexes between the renderer and the model containers.
/// </summary>
public sealed record DrModelTag(GothicModelAsset Asset);

public sealed record DrModelInstanceTag(
	Matrix Transform,
	AnimationController? Controller);

public sealed record DrMeshPartTag(int BoneIndex);

public sealed record DrMaterialTag(MaterialDesc Descriptor);
