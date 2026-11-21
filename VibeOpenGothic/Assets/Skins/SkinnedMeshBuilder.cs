using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.Core;

namespace VibeOpenGothic.Assets.Skins;

/// <summary>
/// Builds a standard-LBS skinned mesh from Gothic four-position inputs. The
/// bind pose is converted to engine space and baked once; geometry keeps UVs and
/// per-wedge normals.
/// </summary>
public static class SkinnedMeshBuilder
{
	private const float DegenerateAreaSquared = 1e-10f;

	public static SkinnedMeshResult Build(
		SkinnedMeshSource source,
		string name,
		IReadOnlyList<int>? sourceToDrBone = null)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		var geometry = source.Geometry;
		if (geometry.Kind != MeshSourceKind.Model)
		{
			throw new ArgumentException("Skinned geometry must be a model mesh.", nameof(source));
		}

		if (geometry.Positions.Count != source.Influences.Count)
		{
			throw new InvalidDataException(
				$"Mesh '{name}' has {geometry.Positions.Count} positions but " +
				$"{source.Influences.Count} influence lists.");
		}

		var skeleton = SkinSkeletonBuilder.Build(source.Hierarchy, source.RootTranslation);
		var engineInfluences = new IReadOnlyList<SkinInfluence>[source.Influences.Count];

		for (var vertexIndex = 0; vertexIndex < source.Influences.Count; vertexIndex++)
		{
			var sourceInfluences = source.Influences[vertexIndex] ?? Array.Empty<SkinInfluence>();
			var converted = new SkinInfluence[sourceInfluences.Count];

			for (var influenceIndex = 0; influenceIndex < sourceInfluences.Count; influenceIndex++)
			{
				var influence = sourceInfluences[influenceIndex];
				converted[influenceIndex] = new SkinInfluence(
					influence.BoneIndex,
					influence.Weight,
					CoordinateSystem.ToEnginePoint(influence.LocalPosition));
			}

			engineInfluences[vertexIndex] = converted;
		}

		var baked = SkinBake.Bake(engineInfluences, skeleton.BindGlobals);
		if (baked.Vertices.Count != geometry.Positions.Count)
		{
			throw new InvalidDataException($"Mesh '{name}' baked an unexpected vertex count.");
		}

		var vertices = new List<SkinnedVertex>(geometry.Positions.Count);
		var vertexMap = new Dictionary<CornerKey, int>(geometry.Positions.Count);
		var groups = new Dictionary<GroupKey, List<Triangle>>();

		var sourceTriangleCount = 0;
		var degenerateTriangleCount = 0;
		var invalidPolygonCount = 0;

		foreach (var polygon in geometry.Polygons)
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
				if ((uint)positionIndices[corner] >= (uint)geometry.Positions.Count ||
					(uint)featureIndices[corner] >= (uint)geometry.Features.Count)
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

				var v0 = baked.Vertices[p0].Position;
				var v1 = baked.Vertices[p1].Position;
				var v2 = baked.Vertices[p2].Position;

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
		var mesh = new SkinnedMeshData(
			name,
			vertices.ToArray(),
			indices.ToArray(),
			subMeshes.ToArray(),
			bounds,
			skeleton);

		var stats = new SkinnedMeshStats(
			geometry.Polygons.Count,
			sourceTriangleCount,
			degenerateTriangleCount,
			invalidPolygonCount,
			vertices.Count,
			indices.Count,
			subMeshes.Count,
			baked.Diagnostics);

		return new SkinnedMeshResult(mesh, stats);

		int GetVertex(int positionIndex, int featureIndex)
		{
			var key = new CornerKey(positionIndex, featureIndex);
			if (vertexMap.TryGetValue(key, out var existing))
			{
				return existing;
			}

			var feature = geometry.Features[featureIndex];
			var bakedVertex = baked.Vertices[positionIndex];
			var vertex = new SkinnedVertex(
				bakedVertex.Position,
				CoordinateSystem.ToEngineNormal(feature.Normal),
				feature.TexCoord,
				bakedVertex.Weights,
				RemapBoneIndices(bakedVertex.BoneIndices, sourceToDrBone));

			var index = vertices.Count;
			vertices.Add(vertex);
			vertexMap[key] = index;
			return index;
		}
	}

	private static Byte4 RemapBoneIndices(
		Byte4 indices,
		IReadOnlyList<int>? sourceToDrBone)
	{
		if (sourceToDrBone is null)
		{
			return indices;
		}

		var packed = indices.PackedValue;
		return new Byte4(
			Remap((byte)(packed & 0xFF), sourceToDrBone),
			Remap((byte)((packed >> 8) & 0xFF), sourceToDrBone),
			Remap((byte)((packed >> 16) & 0xFF), sourceToDrBone),
			Remap((byte)((packed >> 24) & 0xFF), sourceToDrBone));
	}

	private static byte Remap(byte sourceIndex, IReadOnlyList<int> sourceToDrBone)
	{
		if (sourceIndex >= sourceToDrBone.Count)
		{
			return 0;
		}

		var mapped = sourceToDrBone[sourceIndex];
		return (uint)mapped > byte.MaxValue ? (byte)0 : (byte)mapped;
	}

	private static BoundingBox ComputeBounds(List<SkinnedVertex> vertices)
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
