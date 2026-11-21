using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.Assets.Textures;
using ZenKit;

namespace VibeOpenGothic.Assets.Materials;

public static class MaterialSetBuilder
{
	public static MaterialSet FromMesh(
		IMesh mesh,
		TextureCache textures,
		bool enableAlphaTest = true)
	{
		ArgumentNullException.ThrowIfNull(mesh);
		ArgumentNullException.ThrowIfNull(textures);

		var sourceMaterials = mesh.Materials;
		var materials = new GothicMaterial[sourceMaterials.Count];

		for (var index = 0; index < materials.Length; index++)
		{
			materials[index] = MaterialBuilder.FromSource(
				ZenKitMaterialAdapter.FromMaterial(sourceMaterials[index]),
				textures,
				enableAlphaTest);
		}

		return new MaterialSet(MeshSourceKind.World, materials);
	}

	public static MaterialSet FromMultiResolutionMesh(
		IMultiResolutionMesh mesh,
		TextureCache textures,
		bool enableAlphaTest = true)
	{
		ArgumentNullException.ThrowIfNull(mesh);
		ArgumentNullException.ThrowIfNull(textures);

		var subMeshes = mesh.SubMeshes;
		var materials = new GothicMaterial[subMeshes.Count];

		for (var index = 0; index < materials.Length; index++)
		{
			var material = subMeshes[index].Material;
			materials[index] = material is null
				? GothicMaterial.Default
				: MaterialBuilder.FromSource(
					ZenKitMaterialAdapter.FromMaterial(material),
					textures,
					enableAlphaTest);
		}

		return new MaterialSet(MeshSourceKind.Model, materials);
	}
}
