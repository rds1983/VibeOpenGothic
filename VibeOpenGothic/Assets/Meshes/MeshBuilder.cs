using Microsoft.Xna.Framework;
using VibeOpenGothic.Core;

namespace VibeOpenGothic.Assets.Meshes;

/// <summary>
/// Converts a Gothic-space <see cref="MeshSource"/> into engine-space
/// <see cref="MeshData"/>. Polygons are fan-triangulated, winding is reversed,
/// normals are reflected, and vertices are deduplicated by position/feature
/// pair.
/// </summary>
public static class MeshBuilder
{
	private const float DegenerateAreaSquared = 1e-10f;

	public static MeshConversionResult Build(MeshSource source, string name)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		if (source.Positions is null || source.Features is null || source.Polygons is null)
		{
			throw new ArgumentException("Mesh source lists cannot be null.", nameof(source));
		}

		var positions = source.Positions;
		var features = source.Features;
		var vertices = new List<MeshVertex>(positions.Count);
		var vertexMap = new Dictionary<CornerKey, int>(positions.Count);
		var groups = new Dictionary<GroupKey, List<Triangle>>();

		var sourceTriangleCount = 0;
		var degenerateTriangleCount = 0;
		var invalidPolygonCount = 0;

		foreach (var polygon in source.Polygons)
		{
			var positionIndices = polygon.PositionIndices;
			var featureIndices = polygon.FeatureIndices;

			if (positionIndices.Count < 3 || featureIndices.Count != positionIndices.Count)
			{
				invalidPolygonCount++;
				continue;
			}

			var valid = true;
			for (var corner = 0; corner < positionIndices.Count; corner++)
			{
				if ((uint)positionIndices[corner] >= (uint)positions.Count ||
					(uint)featureIndices[corner] >= (uint)features.Count)
				{
					valid = false;
					break;
				}
			}

			if (!valid)
			{
				invalidPolygonCount++;
				continue;
			}

			for (var corner = 1; corner + 1 < positionIndices.Count; corner++)
			{
				sourceTriangleCount++;

				var p0 = positionIndices[0];
				var p1 = positionIndices[corner];
				var p2 = positionIndices[corner + 1];

				if (p0 == p1 || p1 == p2 || p2 == p0)
				{
					degenerateTriangleCount++;
					continue;
				}

				var v0 = CoordinateSystem.ToEnginePoint(positions[p0]);
				var v1 = CoordinateSystem.ToEnginePoint(positions[p1]);
				var v2 = CoordinateSystem.ToEnginePoint(positions[p2]);

				if (Vector3.Cross(v1 - v0, v2 - v0).LengthSquared() <= DegenerateAreaSquared)
				{
					degenerateTriangleCount++;
					continue;
				}

				var f0 = featureIndices[0];
				var f1 = featureIndices[corner];
				var f2 = featureIndices[corner + 1];

				// Reverse winding: (0, 1, 2) -> (0, 2, 1).
				var a = GetVertex(p0, f0);
				var b = GetVertex(p1, f1);
				var c = GetVertex(p2, f2);

				var key = new GroupKey(polygon.MaterialIndex, polygon.LightMapIndex);
				if (!groups.TryGetValue(key, out var group))
				{
					group = new List<Triangle>();
					groups[key] = group;
				}

				group.Add(new Triangle(a, c, b));
			}
		}

		var indices = new List<int>(sourceTriangleCount * 3);
		var subMeshes = new List<MeshSubMesh>();

		foreach (var key in groups.Keys
					 .OrderBy(groupKey => groupKey.Material)
					 .ThenBy(groupKey => groupKey.LightMap))
		{
			var triangleList = groups[key];
			var indexOffset = indices.Count;
			var subMeshBounds = new BoundingBox(
				new Vector3(float.PositiveInfinity),
				new Vector3(float.NegativeInfinity));

			foreach (var triangle in triangleList)
			{
				indices.Add(triangle.A);
				indices.Add(triangle.B);
				indices.Add(triangle.C);

				subMeshBounds = subMeshBounds.Include(vertices[triangle.A].Position);
				subMeshBounds = subMeshBounds.Include(vertices[triangle.B].Position);
				subMeshBounds = subMeshBounds.Include(vertices[triangle.C].Position);
			}

			subMeshes.Add(new MeshSubMesh(
				indexOffset,
				indices.Count - indexOffset,
				key.Material,
				key.LightMap,
				subMeshBounds));
		}

		var bounds = ComputeBounds(vertices);

		var mesh = new MeshData(
			name,
			source.Kind,
			vertices.ToArray(),
			indices.ToArray(),
			subMeshes.ToArray(),
			bounds);

		var stats = new MeshConversionStats(
			source.Polygons.Count,
			sourceTriangleCount,
			degenerateTriangleCount,
			invalidPolygonCount,
			vertices.Count,
			indices.Count,
			subMeshes.Count);

		return new MeshConversionResult(mesh, stats);

		int GetVertex(int positionIndex, int featureIndex)
		{
			var key = new CornerKey(positionIndex, featureIndex);
			if (vertexMap.TryGetValue(key, out var existing))
			{
				return existing;
			}

			var feature = features[featureIndex];
			var vertex = new MeshVertex(
				CoordinateSystem.ToEnginePoint(positions[positionIndex]),
				CoordinateSystem.ToEngineNormal(feature.Normal),
				feature.TexCoord);

			var index = vertices.Count;
			vertices.Add(vertex);
			vertexMap[key] = index;
			return index;
		}
	}

	private static BoundingBox ComputeBounds(List<MeshVertex> vertices)
	{
		if (vertices.Count == 0)
		{
			return new BoundingBox(Vector3.Zero, Vector3.Zero);
		}

		var bounds = new BoundingBox(
			new Vector3(float.PositiveInfinity),
			new Vector3(float.NegativeInfinity));
		foreach (var vertex in vertices)
		{
			bounds = bounds.Include(vertex.Position);
		}

		return bounds;
	}

	private readonly record struct CornerKey(int Position, int Feature);

	private readonly record struct GroupKey(int Material, int LightMap);

	private readonly record struct Triangle(int A, int B, int C);
}
