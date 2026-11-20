using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics.PackedVector;
using VibeOpenGothic.Assets.Animation;
using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.Assets.Models;
using VibeOpenGothic.Assets.Skins;

namespace VibeOpenGothic.Tests.Unit;

public sealed class ModelSkinBuilderTests
{
	[Fact]
	public void Build_Over72Bones_Keeps72AndReports()
	{
		var hierarchy = Enumerable.Range(0, 80)
			.Select(index => new SkinHierarchyNode(
				$"bone{index}",
				-1,
				Matrix.CreateTranslation(index, 0, 0)))
			.ToArray();
		var skeleton = SkinSkeletonBuilder.Build(hierarchy, Vector3.Zero);
		var build = GothicModelBuilder.Build(
			"synthetic",
			skeleton,
			new AnimationLoadResult(
				Array.Empty<AnimationSource>(),
				Array.Empty<AnimationAliasSource>(),
				Array.Empty<string>(),
				Array.Empty<string>()));
		using var model = build.Model;

		var vertices = new SkinnedVertex[80];
		for (var index = 0; index < vertices.Length; index++)
		{
			var bone = (byte)(index + 1);
			vertices[index] = new SkinnedVertex(
				new Vector3(index, 0, 0),
				Vector3.UnitY,
				Vector2.Zero,
				new Vector4(1, 0, 0, 0),
				new Byte4(bone, 0, 0, 0));
		}

		var bounds = new BoundingBox(Vector3.Zero, new Vector3(79, 1, 1));
		var geometry = new SkinnedMeshData(
			"synthetic",
			vertices,
			new[] { 0, 1, 2 },
			new[] { new MeshSubMesh(0, 3, 0, -1, bounds) },
			bounds,
			skeleton);
		var warnings = new List<string>();

		var result = ModelSkinBuilder.Build(geometry, model, 0, "synthetic", warnings);

		Assert.Equal(80, result.JointCount);
		Assert.True(result.ExceedsSkinnedEffectLimit);
		Assert.Equal(ModelSkinBuilder.SkinnedEffectJointLimit, result.Skin.Joints.Length);
		Assert.Contains(warnings, warning => warning.Contains("uses 80 bones"));

		foreach (var vertex in result.Geometry.Vertices.Span)
		{
			Assert.InRange(vertex.BlendIndices.ToVector4().X, 0f, 71f);
			Assert.Equal(1f, vertex.BlendWeights.X, 3);
		}
	}
}
