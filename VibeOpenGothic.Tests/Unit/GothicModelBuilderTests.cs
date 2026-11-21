using DigitalRiseModel;
using DigitalRiseModel.Animation;
using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Animation;
using VibeOpenGothic.Assets.Skins;

namespace VibeOpenGothic.Tests.Unit;

public sealed class GothicModelBuilderTests
{
	[Fact]
	public void Build_PreservesDepthFirstBoneOrder()
	{
		var skeleton = Skeleton(
			new SkinHierarchyNode("root", -1, Matrix.Identity),
			new SkinHierarchyNode("childA", 0, Matrix.CreateTranslation(1, 0, 0)),
			new SkinHierarchyNode("childB", 0, Matrix.CreateTranslation(0, 1, 0)),
			new SkinHierarchyNode("grandchild", 1, Matrix.CreateTranslation(0, 0, 5)));

		var result = GothicModelBuilder.Build("test", skeleton, EmptyAnimations());
		using var model = result.Model;

		Assert.Equal(new[] { 0, 1, 3, 2 }, result.SourceToDrBoneIndex);
		Assert.Equal(new[] { 0, 1, 3, 2 }, result.DrToSourceBoneIndex);
		Assert.Equal(3, model.FindBoneByName("childB")!.Index);
		Assert.Equal(3, result.SourceBones[2].Index);
		Assert.Equal("grandchild", model.Bones[2].Name);
		Assert.Contains(
			result.Warnings,
			warning => warning.Contains("not depth-first", StringComparison.Ordinal));
	}

	[Fact]
	public void Build_AttachesRootMeshToModel()
	{
		var skeleton = Skeleton(
			new SkinHierarchyNode("root", -1, Matrix.Identity),
			new SkinHierarchyNode("child", 0, Matrix.CreateTranslation(1, 0, 0)));
		var mesh = new DrMesh { Name = "body" };

		var result = GothicModelBuilder.Build(
			"test",
			skeleton,
			EmptyAnimations(),
			mesh);
		using var model = result.Model;

		Assert.Same(mesh, model.Root.Mesh);
		Assert.Contains(mesh, model.Meshes);
		Assert.Single(model.Meshes);
	}

	[Fact]
	public void Build_SetsDefaultPoseAndInverseBind()
	{
		var skeleton = Skeleton(
			new SkinHierarchyNode("root", -1, Matrix.Identity),
			new SkinHierarchyNode("child", 0, Matrix.CreateTranslation(0, 0, 5)));

		var result = GothicModelBuilder.Build("test", skeleton, EmptyAnimations());
		using var model = result.Model;

		AssertMatrixNear(
			skeleton.Bones[1].Local,
			result.SourceBones[1].DefaultPose.ToMatrix());

		var absolute = new Matrix[model.Bones.Length];
		model.CopyAbsoluteBoneTransformsTo(absolute);
		AssertMatrixNear(skeleton.Bones[1].InverseBindGlobal, Matrix.Invert(absolute[1]));
		Assert.Empty(result.Warnings);
	}

	[Fact]
	public void FindBone_ResolvesAttachmentNodeNames()
	{
		var skeleton = Skeleton(
			new SkinHierarchyNode("root", -1, Matrix.Identity),
			new SkinHierarchyNode("BIP01 R HAND", 0, Matrix.CreateTranslation(1, 0, 0)),
			new SkinHierarchyNode("ZS_RIGHTHAND", 1, Matrix.CreateTranslation(0, 0, 5)));

		var result = GothicModelBuilder.Build("attachments", skeleton, EmptyAnimations());
		using var model = result.Model;

		var hand = Assert.IsType<DrModelBone>(model.FindBoneByName("ZS_RIGHTHAND"));
		Assert.Equal(2, hand.Index);
		Assert.Equal(1, hand.Parent!.Index);
		Assert.Null(model.FindBoneByName("missing"));
	}

	[Fact]
	public void Build_MultipleRoots_AddsSyntheticRoot()
	{
		var skeleton = Skeleton(
			new SkinHierarchyNode("rootA", -1, Matrix.Identity),
			new SkinHierarchyNode("rootB", -1, Matrix.CreateTranslation(0, 0, 5)));

		var result = GothicModelBuilder.Build("multi", skeleton, EmptyAnimations());
		using var model = result.Model;

		Assert.Equal(3, model.Bones.Length);
		Assert.Equal("<ROOT>", model.Root.Name);
		Assert.Equal(new[] { 1, 2 }, result.SourceToDrBoneIndex);
		Assert.Equal(-1, result.DrToSourceBoneIndex[0]);
		Assert.Contains(result.Warnings, warning => warning.Contains("synthetic root", StringComparison.Ordinal));
	}

	[Fact]
	public void Build_Cycle_DoesNotHang()
	{
		var skeleton = Skeleton(
			new SkinHierarchyNode("a", 1, Matrix.Identity),
			new SkinHierarchyNode("b", 0, Matrix.Identity));

		var result = GothicModelBuilder.Build("cycle", skeleton, EmptyAnimations());
		using var model = result.Model;

		Assert.Equal(2, model.Bones.Length);
		Assert.Contains(result.Warnings, warning => warning.Contains("Cycle", StringComparison.Ordinal));
	}

	[Fact]
	public void Build_AddsAliasClips()
	{
		var skeleton = Skeleton(new SkinHierarchyNode("root", -1, Matrix.Identity));
		var animations = new AnimationLoadResult(
			new[]
			{
				new AnimationSource(
					"s_Base",
					"S_BASE",
					"s_Alias",
					Layer: 1,
					Fps: 10,
					FrameCount: 2,
					BlendInSeconds: 0,
					BlendOutSeconds: 0,
					GothicAnimationFlags.Move,
					Reverse: false,
					FirstFrame: 0,
					LastFrame: 1,
					HasComboWindow: false,
					RootMotionDelta: Vector3.Zero,
					HasRootMotion: false,
					new[]
					{
						new AnimationChannelSource(0, new[]
						{
							new AnimationSampleSource(Vector3.Zero, Quaternion.Identity),
							new AnimationSampleSource(Vector3.Zero, Quaternion.Identity),
						}),
					},
					Array.Empty<AnimationEventSource>()),
			},
			new[]
			{
				new AnimationAliasSource(
					"s_Alias",
					"s_Base",
					"s_Alias",
					Layer: 2,
					BlendInSeconds: 0.1f,
					BlendOutSeconds: 0.2f,
					GothicAnimationFlags.Rotate,
					Reverse: false),
			},
			Array.Empty<string>(),
			Array.Empty<string>());

		var result = GothicModelBuilder.Build("alias", skeleton, animations);
		using var model = result.Model;

		Assert.NotNull(model.Animations["s_base"]);
		var alias = Assert.IsType<AnimationClip>(model.Animations["s_alias"]);
		var tag = Assert.IsType<AnimationClipTag>(alias.Tag);
		Assert.True(tag.IsLoop);
		Assert.Equal(2, tag.Layer);
		Assert.Contains("S_ALIAS", model.Animations.Keys);
	}

	private static SkinSkeleton Skeleton(params SkinHierarchyNode[] nodes)
	{
		return SkinSkeletonBuilder.Build(nodes, Vector3.Zero);
	}

	private static AnimationLoadResult EmptyAnimations()
	{
		return new AnimationLoadResult(
			Array.Empty<AnimationSource>(),
			Array.Empty<AnimationAliasSource>(),
			Array.Empty<string>(),
			Array.Empty<string>());
	}

	private static void AssertMatrixNear(Matrix expected, Matrix actual)
	{
		const float epsilon = 1e-4f;
		var expectedValues = new[]
		{
			expected.M11, expected.M12, expected.M13, expected.M14,
			expected.M21, expected.M22, expected.M23, expected.M24,
			expected.M31, expected.M32, expected.M33, expected.M34,
			expected.M41, expected.M42, expected.M43, expected.M44,
		};
		var actualValues = new[]
		{
			actual.M11, actual.M12, actual.M13, actual.M14,
			actual.M21, actual.M22, actual.M23, actual.M24,
			actual.M31, actual.M32, actual.M33, actual.M34,
			actual.M41, actual.M42, actual.M43, actual.M44,
		};

		for (var index = 0; index < expectedValues.Length; index++)
		{
			Assert.InRange(MathF.Abs(expectedValues[index] - actualValues[index]), 0f, epsilon);
		}
	}
}
