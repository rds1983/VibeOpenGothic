using DigitalRiseModel;
using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Animation;
using VibeOpenGothic.Assets.Skins;

namespace VibeOpenGothic.Tests.Unit;

public sealed class RuntimeModelBuilderTests
{
	[Fact]
	public void Build_PreservesDepthFirstBoneOrder()
	{
		var skeleton = Skeleton(
			new SkinHierarchyNode("root", -1, Matrix.Identity),
			new SkinHierarchyNode("childA", 0, Matrix.CreateTranslation(1, 0, 0)),
			new SkinHierarchyNode("childB", 0, Matrix.CreateTranslation(0, 1, 0)),
			new SkinHierarchyNode("grandchild", 1, Matrix.CreateTranslation(0, 0, 5)));

		var result = RuntimeModelBuilder.Build("test", skeleton, EmptyAnimations());
		using var model = result.Model;

		Assert.Equal(new[] { 0, 1, 3, 2 }, model.SourceToDrBoneIndex);
		Assert.Equal(new[] { 0, 1, 3, 2 }, model.DrToSourceBoneIndex);
		Assert.Equal(3, model.FindBone("childB")!.Index);
		Assert.Equal(3, model.SourceBones[2].Index);
		Assert.Equal("grandchild", model.Model.Bones[2].Name);
		Assert.Contains(
			result.Warnings,
			warning => warning.Contains("not depth-first", StringComparison.Ordinal));
	}

	[Fact]
	public void Build_SetsDefaultPoseAndInverseBind()
	{
		var skeleton = Skeleton(
			new SkinHierarchyNode("root", -1, Matrix.Identity),
			new SkinHierarchyNode("child", 0, Matrix.CreateTranslation(0, 0, 5)));

		var result = RuntimeModelBuilder.Build("test", skeleton, EmptyAnimations());
		using var built = result.Model;

		Assert.Equal(2, built.Skin.Joints.Length);
		Assert.Equal(skeleton.Bones[1].InverseBindGlobal, built.Skin.Joints[1].InverseBindTransform);
		AssertMatrixNear(
			skeleton.Bones[1].Local,
			built.SourceBones[1].DefaultPose.ToMatrix());
		Assert.Empty(result.Warnings);
	}
	[Fact]
	public void FindBone_ResolvesAttachmentNodeNames()
	{
		var skeleton = Skeleton(
			new SkinHierarchyNode("root", -1, Matrix.Identity),
			new SkinHierarchyNode("BIP01 R HAND", 0, Matrix.CreateTranslation(1, 0, 0)),
			new SkinHierarchyNode("ZS_RIGHTHAND", 1, Matrix.CreateTranslation(0, 0, 5)));

		var result = RuntimeModelBuilder.Build("attachments", skeleton, EmptyAnimations());
		using var model = result.Model;

		var hand = Assert.IsType<DrModelBone>(model.FindBone("ZS_RIGHTHAND"));
		Assert.Equal(2, hand.Index);
		Assert.Equal(1, hand.Parent!.Index);
		Assert.Null(model.FindBone("missing"));
	}

	[Fact]
	public void Build_MultipleRoots_AddsSyntheticRoot()
	{
		var skeleton = Skeleton(
			new SkinHierarchyNode("rootA", -1, Matrix.Identity),
			new SkinHierarchyNode("rootB", -1, Matrix.CreateTranslation(0, 0, 5)));

		var result = RuntimeModelBuilder.Build("multi", skeleton, EmptyAnimations());
		using var model = result.Model;

		Assert.Equal(3, model.Model.Bones.Length);
		Assert.Equal("<ROOT>", model.Model.Root.Name);
		Assert.Equal(new[] { 1, 2 }, model.SourceToDrBoneIndex);
		Assert.Equal(-1, model.DrToSourceBoneIndex[0]);
		Assert.Contains(result.Warnings, warning => warning.Contains("synthetic root", StringComparison.Ordinal));
	}

	[Fact]
	public void Build_Cycle_DoesNotHang()
	{
		var skeleton = Skeleton(
			new SkinHierarchyNode("a", 1, Matrix.Identity),
			new SkinHierarchyNode("b", 0, Matrix.Identity));

		var result = RuntimeModelBuilder.Build("cycle", skeleton, EmptyAnimations());
		using var model = result.Model;

		Assert.Equal(2, model.Model.Bones.Length);
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

		var result = RuntimeModelBuilder.Build("alias", skeleton, animations);
		using var model = result.Model;

		Assert.NotNull(model.FindClip("s_base"));
		var alias = Assert.IsType<GothicAnimationClip>(model.FindClip("s_alias"));
		Assert.True(alias.IsLoop);
		Assert.Equal(2, alias.Layer);
		Assert.Contains("S_ALIAS", model.Model.Animations.Keys);
	}

	private static RuntimeModel Build(SkinSkeleton skeleton)
	{
		return RuntimeModelBuilder.Build("test", skeleton, EmptyAnimations()).Model;
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
		Assert.InRange(MathF.Abs(expected.M11 - actual.M11), 0f, epsilon);
		Assert.InRange(MathF.Abs(expected.M22 - actual.M22), 0f, epsilon);
		Assert.InRange(MathF.Abs(expected.M33 - actual.M33), 0f, epsilon);
		Assert.InRange(MathF.Abs(expected.M41 - actual.M41), 0f, epsilon);
		Assert.InRange(MathF.Abs(expected.M42 - actual.M42), 0f, epsilon);
		Assert.InRange(MathF.Abs(expected.M43 - actual.M43), 0f, epsilon);
	}
}
