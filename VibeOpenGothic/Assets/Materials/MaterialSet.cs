using VibeOpenGothic.Assets.Meshes;

namespace VibeOpenGothic.Assets.Materials;

/// <summary>
/// Material list indexed the same way as <see cref="MeshSubMesh.MaterialIndex"/>:
/// world/static meshes use the source material list; model meshes use the
/// multi-resolution submesh index.
/// </summary>
public sealed class MaterialSet
{
	public MaterialSet(MeshSourceKind sourceKind, IReadOnlyList<GothicMaterial> materials)
	{
		ArgumentNullException.ThrowIfNull(materials);

		SourceKind = sourceKind;
		Materials = materials;
	}

	public MeshSourceKind SourceKind { get; }

	public IReadOnlyList<GothicMaterial> Materials { get; }

	public GothicMaterial Get(int index)
	{
		return (uint)index < (uint)Materials.Count ? Materials[index] : GothicMaterial.Default;
	}
}
