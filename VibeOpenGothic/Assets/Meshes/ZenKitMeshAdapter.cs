using Microsoft.Xna.Framework;
using ZenKit;

namespace VibeOpenGothic.Assets.Meshes;

/// <summary>
/// Thin adapters from ZenKit mesh types to the engine-space-independent
/// <see cref="MeshSource"/> DTO. No coordinate conversion happens here; the
/// builder applies it.
/// </summary>
public static class ZenKitMeshAdapter
{
	public static MeshSource FromMesh(IMesh mesh)
	{
		ArgumentNullException.ThrowIfNull(mesh);

		var sourcePositions = mesh.Positions;
		var positions = new Vector3[sourcePositions.Count];
		for (var index = 0; index < positions.Length; index++)
		{
			positions[index] = ToXna(sourcePositions[index]);
		}

		var sourceFeatures = mesh.Features;
		var features = new MeshSourceFeature[sourceFeatures.Count];
		for (var index = 0; index < features.Length; index++)
		{
			features[index] = ToSourceFeature(sourceFeatures[index]);
		}

		var polygons = new List<MeshSourcePolygon>(mesh.PolygonCount);
		foreach (var polygon in mesh.Polygons)
		{
			if (polygon.IsLod)
			{
				continue;
			}

			polygons.Add(new MeshSourcePolygon(
				polygon.MaterialIndex,
				polygon.LightMapIndex,
				polygon.PositionIndices.ToArray(),
				polygon.FeatureIndices.ToArray()));
		}

		return new MeshSource(MeshSourceKind.World, positions, features, polygons);
	}

	/// <summary>
	/// Converts a model mesh. Each submesh carries one material, so
	/// <see cref="MeshSubMesh.MaterialIndex"/> is the submesh index for
	/// <see cref="MeshSourceKind.Model"/> meshes.
	/// </summary>
	public static MeshSource FromMultiResolutionMesh(IMultiResolutionMesh mesh)
	{
		ArgumentNullException.ThrowIfNull(mesh);

		var sourcePositions = mesh.Positions;
		var positions = new Vector3[sourcePositions.Count];
		for (var index = 0; index < positions.Length; index++)
		{
			positions[index] = ToXna(sourcePositions[index]);
		}

		var features = new List<MeshSourceFeature>();
		var polygons = new List<MeshSourcePolygon>();

		var subMeshes = mesh.SubMeshes;
		for (var subMeshIndex = 0; subMeshIndex < subMeshes.Count; subMeshIndex++)
		{
			var subMesh = subMeshes[subMeshIndex];
			var wedges = subMesh.Wedges;
			var triangles = subMesh.Triangles;
			var wedgeBase = features.Count;

			foreach (var wedge in wedges)
			{
				features.Add(ToSourceFeature(wedge.Normal, wedge.Texture));
			}

			foreach (var triangle in triangles)
			{
				if (triangle.Wedge0 >= wedges.Count ||
					triangle.Wedge1 >= wedges.Count ||
					triangle.Wedge2 >= wedges.Count)
				{
					continue;
				}

				polygons.Add(new MeshSourcePolygon(
					subMeshIndex,
					-1,
					new[]
					{
						(int)wedges[triangle.Wedge0].Index,
						(int)wedges[triangle.Wedge1].Index,
						(int)wedges[triangle.Wedge2].Index,
					},
					new[]
					{
						wedgeBase + triangle.Wedge0,
						wedgeBase + triangle.Wedge1,
						wedgeBase + triangle.Wedge2,
					}));
			}
		}

		return new MeshSource(MeshSourceKind.Model, positions, features, polygons);
	}

	private static MeshSourceFeature ToSourceFeature(Vertex vertex)
	{
		return ToSourceFeature(vertex.Normal, vertex.Texture);
	}

	private static MeshSourceFeature ToSourceFeature(
		System.Numerics.Vector3 normal,
		System.Numerics.Vector2 texture)
	{
		return new MeshSourceFeature(
			new Vector3(normal.X, normal.Y, normal.Z),
			new Vector2(texture.X, texture.Y));
	}

	private static Vector3 ToXna(System.Numerics.Vector3 value)
	{
		return new Vector3(value.X, value.Y, value.Z);
	}
}
