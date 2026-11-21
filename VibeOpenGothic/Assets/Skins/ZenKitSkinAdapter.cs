using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.Core;
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
					entry.Position.ToXna());
			}

			influences[positionIndex] = converted;
		}

		return new SkinnedMeshSource(
			geometry,
			influences,
			FromModelHierarchy(hierarchy),
			hierarchy.RootTranslation.ToXna());
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
				node.Transform.ToXna());
		}

		return nodes;
	}
}
