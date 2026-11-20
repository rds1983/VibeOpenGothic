using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Meshes;
using ZenKit;

namespace VibeOpenGothic.Assets.Skins;

public static class ZenKitSkinAdapter
{
	public static SkinnedMeshSource FromSoftSkinMesh(
		ISoftSkinMesh mesh,
		IModelHierarchy hierarchy)
	{
		ArgumentNullException.ThrowIfNull(mesh);
		ArgumentNullException.ThrowIfNull(hierarchy);

		var geometry = ZenKitMeshAdapter.FromMultiResolutionMesh(mesh.Mesh);
		var weightLists = mesh.Weights;
		var influences = new IReadOnlyList<SkinInfluence>[weightLists.Count];

		for (var positionIndex = 0; positionIndex < weightLists.Count; positionIndex++)
		{
			var entries = weightLists[positionIndex];
			var converted = new SkinInfluence[entries.Count];

			for (var index = 0; index < entries.Count; index++)
			{
				var entry = entries[index];
				converted[index] = new SkinInfluence(
					entry.NodeIndex,
					entry.Weight,
					ToXna(entry.Position));
			}

			influences[positionIndex] = converted;
		}

		return new SkinnedMeshSource(
			geometry,
			influences,
			FromModelHierarchy(hierarchy),
			ToXna(hierarchy.RootTranslation));
	}

	public static IReadOnlyList<SkinHierarchyNode> FromModelHierarchy(IModelHierarchy hierarchy)
	{
		ArgumentNullException.ThrowIfNull(hierarchy);

		var sourceNodes = hierarchy.Nodes;
		var nodes = new SkinHierarchyNode[sourceNodes.Count];

		for (var index = 0; index < sourceNodes.Count; index++)
		{
			var node = sourceNodes[index];
			nodes[index] = new SkinHierarchyNode(
				node.Name ?? string.Empty,
				node.ParentIndex,
				ToXna(node.Transform));
		}

		return nodes;
	}

	private static Vector3 ToXna(System.Numerics.Vector3 value)
	{
		return new Vector3(value.X, value.Y, value.Z);
	}

	private static Matrix ToXna(System.Numerics.Matrix4x4 value)
	{
		// ZenKit exposes the hierarchy transform as a column-vector matrix
		// (translation in M14/M24/M34). MonoGame matrices are row-vector, so
		// transpose while copying.
		return new Matrix(
			value.M11, value.M21, value.M31, value.M41,
			value.M12, value.M22, value.M32, value.M42,
			value.M13, value.M23, value.M33, value.M43,
			value.M14, value.M24, value.M34, value.M44);
	}
}
