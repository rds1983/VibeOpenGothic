using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.Assets.Skins;

namespace VibeOpenGothic.Tests.Unit;

public sealed class SkinnedMeshBuilderTests
{
	[Fact]
	public void Build_SingleBone_ConvertsGeometryAndSkinData()
	{
		var source = Source();

		var result = SkinnedMeshBuilder.Build(source, "model");

		var mesh = result.Mesh;
		Assert.Equal(3, mesh.VertexCount);
		Assert.Equal(3, mesh.IndexCount);
		Assert.Equal(new[] { 0, 2, 1 }, mesh.Indices.ToArray());
		AssertNear(new Vector3(0, 0, 0), mesh.Vertices.Span[0].Position);
		AssertNear(new Vector3(1, 0, 0), mesh.Vertices.Span[1].Position);
		AssertNear(new Vector3(0, 1, 0), mesh.Vertices.Span[2].Position);
		AssertNear(new Vector3(0, 0, -1), mesh.Vertices.Span[0].Normal);
		Assert.Equal(new Vector2(0, 1), mesh.Vertices.Span[0].TexCoord);
		Assert.Equal(new Vector4(1, 0, 0, 0), mesh.Vertices.Span[0].BlendWeights);
		Assert.Equal(0f, mesh.Vertices.Span[0].BlendIndices.ToVector4().X);
		Assert.Equal(1, mesh.BoneCount);
		Assert.Single(mesh.SubMeshes);
		Assert.Equal(3, result.Stats.Skin.VertexCount);
		Assert.Equal(1, result.Stats.Skin.BoneCount);
	}

	[Fact]
	public void Build_TwoBones_BakesStandardLbs()
	{
		var pCommon = new[]
		{
			new Vector3(2, 3, 4),
			new Vector3(3, 3, 4),
			new Vector3(2, 4, 4),
		};
		var bindRoot = Matrix.Identity;
		var bindChild = Matrix.CreateTranslation(0, 0, 5);
		var hierarchy = new[]
		{
			new SkinHierarchyNode("root", -1, bindRoot),
			new SkinHierarchyNode("child", 0, bindChild),
		};
		var influences = pCommon
			.Select(point => (IReadOnlyList<SkinInfluence>)new[]
			{
				new SkinInfluence(0, 0.5f, point),
				new SkinInfluence(
					1,
					0.5f,
					Vector3.Transform(point, Matrix.Invert(bindChild))),
			})
			.ToArray();
		var source = Source(
			positions: pCommon,
			influences: influences,
			hierarchy: hierarchy);

		var result = SkinnedMeshBuilder.Build(source, "model");

		AssertNear(new Vector3(2, 3, -4), result.Mesh.Vertices.Span[0].Position, 1e-4f);
		AssertNear(new Vector3(3, 3, -4), result.Mesh.Vertices.Span[1].Position, 1e-4f);
		AssertNear(new Vector3(2, 4, -4), result.Mesh.Vertices.Span[2].Position, 1e-4f);
		Assert.InRange(result.Stats.Skin.MaxInconsistency, 0f, 1e-4f);
	}

	[Fact]
	public void Build_KeepsUvSeamsSeparate()
	{
		var features = Features(6);
		var source = Source(
			features: features,
			polygons: new[]
			{
				Poly(new[] { 0, 1, 2 }, new[] { 0, 1, 2 }),
				Poly(new[] { 0, 1, 2 }, new[] { 3, 4, 5 }),
			});

		var result = SkinnedMeshBuilder.Build(source, "seam");

		Assert.Equal(6, result.Mesh.VertexCount);
		Assert.Equal(6, result.Mesh.IndexCount);
	}

	[Fact]
	public void Build_GroupsByMaterialAndLightMap()
	{
		var source = Source(
			polygons: new[]
			{
				Poly(new[] { 0, 1, 2 }, new[] { 0, 1, 2 }, 1, 0),
				Poly(new[] { 0, 1, 2 }, new[] { 0, 1, 2 }, 0, 1),
				Poly(new[] { 0, 1, 2 }, new[] { 0, 1, 2 }, 0, 0),
			});

		var result = SkinnedMeshBuilder.Build(source, "groups");

		Assert.Equal(
			new[] { (0, 0), (0, 1), (1, 0) },
			result.Mesh.SubMeshes.Select(subMesh => (subMesh.MaterialIndex, subMesh.LightMapIndex)));
	}

	[Fact]
	public void Build_SkipsDegenerateAndInvalidPolygons()
	{
		var source = Source(
			positions: new[]
			{
				new Vector3(0, 0, 0),
				new Vector3(1, 0, 0),
				new Vector3(2, 0, 0),
			},
			polygons: new[]
			{
				Poly(new[] { 0, 1, 2 }, new[] { 0, 1, 2 }),
				Poly(new[] { 0, 1 }, new[] { 0, 1 }),
				Poly(new[] { 0, 1, 2 }, new[] { 0, 1 }),
			});

		var result = SkinnedMeshBuilder.Build(source, "degenerate");

		Assert.Equal(1, result.Stats.DegenerateTriangleCount);
		Assert.Equal(2, result.Stats.InvalidPolygonCount);
		Assert.Equal(0, result.Mesh.VertexCount);
	}

	[Fact]
	public void Build_RejectsPositionInfluenceMismatch()
	{
		var source = Source(
			positions: new[]
			{
				new Vector3(0, 0, 0),
				new Vector3(1, 0, 0),
				new Vector3(0, 1, 0),
			},
			influences: new IReadOnlyList<SkinInfluence>[]
			{
				new[] { new SkinInfluence(0, 1, Vector3.Zero) },
				new[] { new SkinInfluence(0, 1, Vector3.Zero) },
			});

		Assert.Throws<InvalidDataException>(() => SkinnedMeshBuilder.Build(source, "bad"));
	}

	[Fact]
	public void Build_RejectsNonModelGeometry()
	{
		var source = Source(kind: MeshSourceKind.World);

		Assert.Throws<ArgumentException>(() => SkinnedMeshBuilder.Build(source, "bad"));
	}

	[Fact]
	public void Build_ReportsBoneLimit()
	{
		var hierarchy = Enumerable.Range(0, 73)
			.Select(index => new SkinHierarchyNode($"bone{index}", -1, Matrix.Identity))
			.ToArray();
		var source = Source(hierarchy: hierarchy);

		var result = SkinnedMeshBuilder.Build(source, "many-bones");

		Assert.Equal(73, result.Mesh.BoneCount);
		Assert.True(result.Mesh.Skeleton.ExceedsSkinnedEffectLimit);
		Assert.Equal(73, result.Stats.Skin.BoneCount);
	}

	[Fact]
	public void Build_RemapsBoneIndicesThroughModelMap()
	{
		var hierarchy = new[]
		{
			new SkinHierarchyNode("root", -1, Matrix.Identity),
			new SkinHierarchyNode("child", 0, Matrix.Identity),
		};
		var influences = new[]
		{
			(IReadOnlyList<SkinInfluence>)new[] { new SkinInfluence(1, 1f, new Vector3(0, 0, 0)) },
			new[] { new SkinInfluence(1, 1f, new Vector3(1, 0, 0)) },
			new[] { new SkinInfluence(1, 1f, new Vector3(0, 1, 0)) },
		};
		var source = Source(influences: influences, hierarchy: hierarchy);

		var result = SkinnedMeshBuilder.Build(source, "remap", new[] { 1, 0 });

		Assert.Equal(0f, result.Mesh.Vertices.Span[0].BlendIndices.ToVector4().X);
		Assert.Equal(1f, result.Mesh.Vertices.Span[0].BlendWeights.X);
	}

	[Fact]
	public void Build_EmptyGeometry_ReturnsEmptyMeshWithSkeleton()
	{
		var source = Source(
			positions: Array.Empty<Vector3>(),
			features: Array.Empty<MeshSourceFeature>(),
			polygons: Array.Empty<MeshSourcePolygon>(),
			influences: Array.Empty<IReadOnlyList<SkinInfluence>>());

		var result = SkinnedMeshBuilder.Build(source, "empty");

		Assert.Equal(0, result.Mesh.VertexCount);
		Assert.Equal(0, result.Mesh.IndexCount);
		Assert.Empty(result.Mesh.SubMeshes);
		Assert.Equal(1, result.Mesh.BoneCount);
		AssertNear(Vector3.Zero, result.Mesh.Bounds.Min);
		AssertNear(Vector3.Zero, result.Mesh.Bounds.Max);
	}

	private static SkinnedMeshSource Source(
		MeshSourceKind kind = MeshSourceKind.Model,
		Vector3[]? positions = null,
		MeshSourceFeature[]? features = null,
		MeshSourcePolygon[]? polygons = null,
		IReadOnlyList<IReadOnlyList<SkinInfluence>>? influences = null,
		IReadOnlyList<SkinHierarchyNode>? hierarchy = null)
	{
		positions ??= new[]
		{
			new Vector3(0, 0, 0),
			new Vector3(1, 0, 0),
			new Vector3(0, 1, 0),
		};
		features ??= Features(3);
		polygons ??= new[] { Poly(new[] { 0, 1, 2 }, new[] { 0, 1, 2 }) };
		influences ??= positions
			.Select(position => (IReadOnlyList<SkinInfluence>)new[]
			{
				new SkinInfluence(0, 1f, position),
			})
			.ToArray();
		hierarchy ??= new[] { new SkinHierarchyNode("root", -1, Matrix.Identity) };

		var geometry = new MeshSource(kind, positions, features, polygons);
		return new SkinnedMeshSource(geometry, influences, hierarchy, Vector3.Zero);
	}

	private static MeshSourcePolygon Poly(
		int[] positions,
		int[] features,
		int material = 0,
		int lightMap = -1)
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

	private static void AssertNear(Vector3 expected, Vector3 actual, float epsilon = 1e-5f)
	{
		Assert.InRange(Vector3.Distance(expected, actual), 0f, epsilon);
	}
}
