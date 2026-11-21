using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Models;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// Typed payloads for our DigitalRiseModel usage. Materials live directly on
/// <see cref="DrMeshPartTag"/> as CPU-side <see cref="GothicMaterial"/> values;
/// the renderer resolves textures through <see cref="GpuTextureCache"/>.
/// </summary>
public sealed record DrModelTag(GothicModelAsset Asset);

public sealed record DrMeshPartTag(
	GothicMaterial Material,
	int? BoneIndex = null);
