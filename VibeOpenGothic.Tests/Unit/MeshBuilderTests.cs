using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.Core;

namespace VibeOpenGothic.Tests.Unit;

public sealed class MeshBuilderTests
{
	[Fact]
	public void Build_Triangle_ConvertsToRhsAndReversesWinding()
	{
		var positions = new[]
		{
			new Vector3(0, 0, 2),
			new Vector3(1, 0, 2),
			new Vector3(0, 1, 2),
		};
		var features = Features(3);
		var source = Source(positions, features, Poly(new[] { 0, 1, 2 }, new[] { 0, 1, 2 }, 2, 3));

		var result = MeshBuilder.Build(source, "triangle");

		var mesh = result.Mesh;
		Assert.Equal(MeshSourceKind.World, mesh.SourceKind);
		Assert.Equal(3, mesh.VertexCount);
		Assert.Equal(3, mesh.IndexCount);
		Assert.Equal(new[] { 0, 2, 1 }, mesh.Indices.ToArray());
		Assert.Equal(new Vector3(0, 0, -2), mesh.Vertices.Span[0].Position);
		Assert.Equal(new Vector3(0, 0, -1), mesh.Vertices.Span[0].Normal);
		Assert.Equal(new Vector2(0, 1), mesh.Vertices.Span[0].TexCoord);

		var subMesh = Assert.Single(mesh.SubMeshes);
		Assert.Equal(0, subMesh.IndexOffset);
		Assert.Equal(3, subMesh.IndexCount);
		Assert.Equal(2, subMesh.MaterialIndex);
		Assert.Equal(3, subMesh.LightMapIndex);
		AssertBounds(new BoundingBox(new Vector3(0, 0, -2), new Vector3(1, 1, -2)), mesh.Bounds);

		Assert.Equal(1, result.Stats.SourcePolygonCount);
		Assert.Equal(1, result.Stats.SourceTriangleCount);
		Assert.Equal(0, result.Stats.DegenerateTriangleCount);
		Assert.Equal(0, result.Stats.InvalidPolygonCount);
		Assert.Equal(3, result.Stats.VertexCount);
		Assert.Equal(1, result.Stats.SubMeshCount);
	}

	[Fact]
	public void Build_Quad_FansIntoTwoTriangles()
	{
		var positions = new[]
		{
			new Vector3(0, 0, 0),
			new Vector3(1, 0, 0),
			new Vector3(1, 1, 0),
			new Vector3(0, 1, 0),
		};
		var source = Source(
			positions,
			Features(4),
			Poly(new[] { 0, 1, 2, 3 }, new[] { 0, 1, 2, 3 }));

		var result = MeshBuilder.Build(source, "quad");

		Assert.Equal(4, result.Mesh.VertexCount);
		Assert.Equal(new[] { 0, 2, 1, 0, 3, 2 }, result.Mesh.Indices.ToArray());
		Assert.Equal(2, result.Stats.SourceTriangleCount);
		Assert.Equal(6, result.Stats.IndexCount);
	}

	[Fact]
	public void Build_FiveGon_FansIntoThreeTriangles()
	{
		var positions = new[]
		{
			new Vector3(0, 0, 0),
			new Vector3(1, 0, 0),
			new Vector3(1, 1, 0),
			new Vector3(0.5f, 1.5f, 0),
			new Vector3(0, 1, 0),
		};
		var source = Source(
			positions,
			Features(5),
			Poly(new[] { 0, 1, 2, 3, 4 }, new[] { 0, 1, 2, 3, 4 }));

		var result = MeshBuilder.Build(source, "pentagon");

		Assert.Equal(5, result.Mesh.VertexCount);
		Assert.Equal(9, result.Mesh.IndexCount);
		Assert.Equal(3, result.Mesh.TriangleCount);
		Assert.Equal(3, result.Stats.SourceTriangleCount);
	}

	[Fact]
	public void Build_KeepsUvSeamsAsSeparateVertices()
	{
		var positions = new[]
		{
			new Vector3(0, 0, 0),
			new Vector3(1, 0, 0),
			new Vector3(0, 1, 0),
		};
		var features = Features(6);
		var source = Source(
			positions,
			features,
			Poly(new[] { 0, 1, 2 }, new[] { 0, 1, 2 }),
			Poly(new[] { 0, 1, 2 }, new[] { 3, 4, 5 }));

		var result = MeshBuilder.Build(source, "seam");

		Assert.Equal(6, result.Mesh.VertexCount);
		Assert.Equal(6, result.Mesh.IndexCount);
		Assert.Single(result.Mesh.SubMeshes);
	}

	[Fact]
	public void Build_GroupsTrianglesByMaterialAndLightMap()
	{
		var positions = new[]
		{
			new Vector3(0, 0, 0),
			new Vector3(1, 0, 0),
			new Vector3(0, 1, 0),
		};
		var source = Source(
			positions,
			Features(3),
			Poly(new[] { 0, 1, 2 }, new[] { 0, 1, 2 }, 1, 0),
			Poly(new[] { 0, 1, 2 }, new[] { 0, 1, 2 }, 0, 1),
			Poly(new[] { 0, 1, 2 }, new[] { 0, 1, 2 }, 0, 0));

		var result = MeshBuilder.Build(source, "groups");

		Assert.Equal(
			new[] { (0, 0), (0, 1), (1, 0) },
			result.Mesh.SubMeshes.Select(subMesh => (subMesh.MaterialIndex, subMesh.LightMapIndex)));
		Assert.Equal(
			new[] { 0, 3, 6 },
			result.Mesh.SubMeshes.Select(subMesh => subMesh.IndexOffset));
		Assert.All(result.Mesh.SubMeshes, subMesh => Assert.Equal(3, subMesh.IndexCount));
		Assert.Equal(3, result.Mesh.VertexCount);
		Assert.Equal(9, result.Mesh.IndexCount);
	}

	[Fact]
	public void Build_SkipsDegenerateTriangles()
	{
		var positions = new[]
		{
			new Vector3(0, 0, 0),
			new Vector3(1, 0, 0),
			new Vector3(2, 0, 0),
		};
		var source = Source(
			positions,
			Features(3),
			Poly(new[] { 0, 0, 1 }, new[] { 0, 0, 1 }),
			Poly(new[] { 0, 1, 2 }, new[] { 0, 1, 2 }));

		var result = MeshBuilder.Build(source, "degenerate");

		Assert.Equal(2, result.Stats.SourceTriangleCount);
		Assert.Equal(2, result.Stats.DegenerateTriangleCount);
		Assert.Equal(0, result.Stats.InvalidPolygonCount);
		Assert.Equal(0, result.Mesh.VertexCount);
		Assert.Equal(0, result.Mesh.IndexCount);
		Assert.Empty(result.Mesh.SubMeshes);
		AssertBounds(new BoundingBox(Vector3.Zero, Vector3.Zero), result.Mesh.Bounds);
	}

	[Fact]
	public void Build_SkipsInvalidPolygons()
	{
		var positions = new[]
		{
			new Vector3(0, 0, 0),
			new Vector3(1, 0, 0),
			new Vector3(0, 1, 0),
		};
		var source = Source(
			positions,
			Features(3),
			Poly(new[] { 0, 1 }, new[] { 0, 1 }),
			Poly(new[] { 0, 1, 2 }, new[] { 0, 1 }),
			Poly(new[] { 0, 1, 9 }, new[] { 0, 1, 2 }));

		var result = MeshBuilder.Build(source, "invalid");

		Assert.Equal(3, result.Stats.SourcePolygonCount);
		Assert.Equal(0, result.Stats.SourceTriangleCount);
		Assert.Equal(3, result.Stats.InvalidPolygonCount);
		Assert.Equal(0, result.Mesh.VertexCount);
		Assert.Empty(result.Mesh.SubMeshes);
	}

	[Fact]
	public void Build_NormalMatchesConvertedWinding()
	{
		var positions = new[]
		{
			new Vector3(0, 0, 2),
			new Vector3(1, 0, 2),
			new Vector3(0, 1, 3),
		};
		var gothicNormal = Vector3.Normalize(
			Vector3.Cross(positions[1] - positions[0], positions[2] - positions[0]));
		var features = new[]
		{
			new MeshSourceFeature(gothicNormal, new Vector2(0, 1)),
			new MeshSourceFeature(gothicNormal, new Vector2(1, 2)),
			new MeshSourceFeature(gothicNormal, new Vector2(2, 3)),
		};
		var source = Source(positions, features, Poly(new[] { 0, 1, 2 }, new[] { 0, 1, 2 }));

		var result = MeshBuilder.Build(source, "normal");
		var mesh = result.Mesh;
		var indices = mesh.Indices.Span;
		var a = mesh.Vertices.Span[indices[0]].Position;
		var b = mesh.Vertices.Span[indices[1]].Position;
		var c = mesh.Vertices.Span[indices[2]].Position;
		var geometric = Vector3.Normalize(Vector3.Cross(b - a, c - a));
		var convertedNormal = CoordinateSystem.ToEngineNormal(gothicNormal);

		AssertNear(convertedNormal, geometric);
		AssertNear(geometric, mesh.Vertices.Span[indices[0]].Normal);
		AssertNear(geometric, mesh.Vertices.Span[indices[1]].Normal);
		AssertNear(geometric, mesh.Vertices.Span[indices[2]].Normal);
	}

	[Fact]
	public void Build_EmptySource_ReturnsEmptyMesh()
	{
		var source = new MeshSource(
			MeshSourceKind.Model,
			Array.Empty<Vector3>(),
			Array.Empty<MeshSourceFeature>(),
			Array.Empty<MeshSourcePolygon>());

		var result = MeshBuilder.Build(source, "empty");

		Assert.Equal(MeshSourceKind.Model, result.Mesh.SourceKind);
		Assert.Equal(0, result.Mesh.VertexCount);
		Assert.Equal(0, result.Mesh.IndexCount);
		Assert.Empty(result.Mesh.SubMeshes);
		AssertBounds(new BoundingBox(Vector3.Zero, Vector3.Zero), result.Mesh.Bounds);
	}

	[Fact]
	public void Build_RejectsNullSourceAndInvalidName()
	{
		Assert.Throws<ArgumentNullException>(() => MeshBuilder.Build(null!, "mesh"));

		var source = new MeshSource(
			MeshSourceKind.World,
			Array.Empty<Vector3>(),
			Array.Empty<MeshSourceFeature>(),
			Array.Empty<MeshSourcePolygon>());

		Assert.Throws<ArgumentException>(() => MeshBuilder.Build(source, " "));
	}

	private static MeshSource Source(
		Vector3[] positions,
		MeshSourceFeature[] features,
		params MeshSourcePolygon[] polygons)
	{
		return new MeshSource(MeshSourceKind.World, positions, features, polygons);
	}

	private static MeshSourcePolygon Poly(
		int[] positions,
		int[] features,
		int material = 0,
		int lightMap = 0)
	{
		return new MeshSourcePolygon(material, lightMap, positions, features);
	}

	private static MeshSourceFeature[] Features(int count)
	{
		var result = new MeshSourceFeature[count];
		for (var index = 0; index < count; index++)
		{
			result[index] = new MeshSourceFeature(
				new Vector3(0, 0, 1),
				new Vector2(index, index + 1));
		}

		return result;
	}

	private static void AssertBounds(BoundingBox expected, BoundingBox actual)
	{
		AssertNear(expected.Min, actual.Min);
		AssertNear(expected.Max, actual.Max);
	}

	private static void AssertNear(Vector3 expected, Vector3 actual)
	{
		Assert.InRange(Vector3.Distance(expected, actual), 0f, 1e-4f);
	}
}
