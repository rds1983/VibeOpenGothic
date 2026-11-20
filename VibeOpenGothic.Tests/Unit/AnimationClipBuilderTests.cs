using DigitalRiseModel;
using DigitalRiseModel.Animation;
using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Animation;
using VibeOpenGothic.Assets.Skins;
using VibeOpenGothic.Core;

namespace VibeOpenGothic.Tests.Unit;

public sealed class AnimationClipBuilderTests
{
	private const float Epsilon = 1e-4f;

	[Fact]
	public void Build_TwoBoneClipAtKnownTimes()
	{
		var yaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
		var halfYaw = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4);
		var source = Source(
			fps: 10,
			frameCount: 2,
			next: "T_OTHER",
			channels: new[]
			{
				Channel(0, Sample(Vector3.Zero, Quaternion.Identity),
					Sample(new Vector3(10, 0, 0), yaw)),
				Channel(1, Sample(new Vector3(0, 0, 5), Quaternion.Identity),
					Sample(new Vector3(0, 0, 5), Quaternion.Identity)),
			});

		using var model = BuildModel(source);
		var clip = Assert.IsType<GothicAnimationClip>(model.Animations["S_TEST"]);

		Assert.True(clip.IsTransition);
		Assert.Equal(TimeSpan.FromMilliseconds(200), clip.Duration);
		Assert.Equal(2, clip.Channels.Length);
		Assert.All(clip.Channels, channel => Assert.Equal(2, channel.Keyframes.Length));

		var instance = new DrModelInstance(model);
		var controller = new AnimationController(instance);
		controller.StartClip(clip, clip.PlaybackFlags);
		controller.Time = TimeSpan.FromMilliseconds(50);

		var rootIndex = 0;
		var childIndex = 1;
		var expectedRoot = Matrix.CreateFromQuaternion(
			CoordinateSystem.ToEngineAnimationQuaternion(halfYaw)) *
			Matrix.CreateTranslation(5, 0, 0);
		var expectedChild = Matrix.CreateTranslation(0, 0, -5);

		AssertMatrixNear(expectedRoot, instance.GetBoneLocalTransform(rootIndex));
		AssertMatrixNear(expectedChild, instance.GetBoneLocalTransform(childIndex));

		controller.Time = TimeSpan.Zero;
		AssertMatrixNear(Matrix.Identity, instance.GetBoneLocalTransform(rootIndex));
	}

	[Fact]
	public void Build_LoopAppendsClosingKeyAndWraps()
	{
		var source = Source(
			fps: 10,
			frameCount: 2,
			next: "s_Test",
			channels: new[]
			{
				Channel(0, Sample(Vector3.Zero, Quaternion.Identity),
					Sample(new Vector3(10, 0, 0), Quaternion.Identity)),
			});

		using var model = BuildModel(source);
		var clip = Assert.IsType<GothicAnimationClip>(model.Animations["S_TEST"]);

		Assert.True(clip.IsLoop);
		Assert.True((clip.PlaybackFlags & DigitalRiseModel.Animation.AnimationFlags.Looped) != 0);
		Assert.Equal(3, clip.Channels[0].Keyframes.Length);
		Assert.Equal(TimeSpan.FromMilliseconds(200), clip.Channels[0].Keyframes[2].Time);
		AssertNear(Vector3.Zero, clip.Channels[0].Keyframes[2].Pose.Translation);

		var instance = new DrModelInstance(model);
		var controller = new AnimationController(instance);
		controller.StartClip(clip, clip.PlaybackFlags);
		controller.Time = TimeSpan.FromMilliseconds(150);

		AssertNear(
			new Vector3(5, 0, 0),
			instance.GetBoneLocalTransform(0).Translation);
	}

	[Fact]
	public void Build_ComboWindowForcesTransition()
	{
		var source = Source(
			fps: 10,
			frameCount: 2,
			next: "s_Test",
			hasComboWindow: true,
			channels: new[]
			{
				Channel(0, Sample(Vector3.Zero, Quaternion.Identity),
					Sample(Vector3.Zero, Quaternion.Identity)),
			});

		using var model = BuildModel(source);
		var clip = Assert.IsType<GothicAnimationClip>(model.Animations["S_TEST"]);

		Assert.False(clip.IsLoop);
		Assert.True(clip.IsTransition);
		Assert.Equal(2, clip.Channels[0].Keyframes.Length);
	}

	[Fact]
	public void Build_ConvertsEventFramesToTime()
	{
		var source = Source(
			fps: 10,
			frameCount: 2,
			next: "T_OTHER",
			events: new[]
			{
				new AnimationEventSource(GothicAnimationEventKind.Tag, 1, "HitEnd", new[] { 1 }),
			},
			channels: new[]
			{
				Channel(0, Sample(Vector3.Zero, Quaternion.Identity),
					Sample(Vector3.Zero, Quaternion.Identity)),
			});

		using var model = BuildModel(source);
		var clip = Assert.IsType<GothicAnimationClip>(model.Animations["S_TEST"]);

		var evt = Assert.Single(clip.Events);
		Assert.Equal(GothicAnimationEventKind.Tag, evt.Kind);
		Assert.Equal(TimeSpan.FromMilliseconds(100), evt.Time);
		Assert.Equal("HitEnd", evt.Name);
	}

	[Fact]
	public void Build_InvalidFps_IsSkipped()
	{
		var warnings = new List<string>();
		var source = Source(
			fps: 0,
			frameCount: 2,
			channels: new[]
			{
				Channel(0, Sample(Vector3.Zero, Quaternion.Identity),
					Sample(Vector3.Zero, Quaternion.Identity)),
			});

		var clip = AnimationClipBuilder.Build(source, new[] { 0 }, warnings);

		Assert.Null(clip);
		Assert.NotEmpty(warnings);
	}

	[Fact]
	public void Build_OutOfRangeBone_IsSkipped()
	{
		var warnings = new List<string>();
		var source = Source(
			fps: 10,
			frameCount: 2,
			channels: new[]
			{
				Channel(5, Sample(Vector3.Zero, Quaternion.Identity),
					Sample(Vector3.Zero, Quaternion.Identity)),
			});

		var clip = AnimationClipBuilder.Build(source, new[] { 0 }, warnings);

		Assert.Null(clip);
		Assert.NotEmpty(warnings);
	}

	[Fact]
	public void BuildAlias_ReusesChannelsAndMetadata()
	{
		var baseSource = Source(
			fps: 10,
			frameCount: 2,
			next: "T_OTHER",
			channels: new[]
			{
				Channel(0, Sample(Vector3.Zero, Quaternion.Identity),
					Sample(new Vector3(5, 0, 0), Quaternion.Identity)),
			});
		var warnings = new List<string>();
		var baseClip = Assert.IsType<GothicAnimationClip>(
			AnimationClipBuilder.Build(baseSource, new[] { 0 }, warnings));
		var alias = new AnimationAliasSource(
			"s_Alias",
			"s_Test",
			"s_Alias",
			Layer: 3,
			BlendInSeconds: 0.2f,
			BlendOutSeconds: 0.3f,
			GothicAnimationFlags.Rotate,
			Reverse: false);

		var aliasClip = Assert.IsType<GothicAnimationClip>(
			AnimationClipBuilder.BuildAlias(alias, baseClip, warnings));

		Assert.Equal("S_ALIAS", aliasClip.Name);
		Assert.True(aliasClip.IsLoop);
		Assert.Equal(3, aliasClip.Layer);
		Assert.Equal(TimeSpan.FromMilliseconds(200), aliasClip.BlendIn);
		Assert.Equal(TimeSpan.FromMilliseconds(300), aliasClip.BlendOut);
		Assert.Same(baseClip.Channels, aliasClip.Channels);
		Assert.Empty(warnings);
	}

	private static DrModel BuildModel(AnimationSource source)
	{
		var skeleton = SkinSkeletonBuilder.Build(
			new[]
			{
				new SkinHierarchyNode("root", -1, Matrix.Identity),
				new SkinHierarchyNode("child", 0, Matrix.CreateTranslation(0, 0, 5)),
			},
			Vector3.Zero);
		var animations = new AnimationLoadResult(
			new[] { source },
			Array.Empty<AnimationAliasSource>(),
			Array.Empty<string>(),
			Array.Empty<string>());
		return GothicModelBuilder.Build("test", skeleton, animations).Model;
	}

	private static AnimationSource Source(
		float fps,
		int frameCount,
		IReadOnlyList<AnimationChannelSource> channels,
		string? next = null,
		bool hasComboWindow = false,
		IReadOnlyList<AnimationEventSource>? events = null)
	{
		return new AnimationSource(
			"s_Test",
			"S_TEST",
			next,
			Layer: 1,
			fps,
			frameCount,
			BlendInSeconds: 0.1f,
			BlendOutSeconds: 0.2f,
			GothicAnimationFlags.Move,
			Reverse: false,
			FirstFrame: 0,
			LastFrame: frameCount - 1,
			hasComboWindow,
			RootMotionDelta: Vector3.Zero,
			HasRootMotion: false,
			channels,
			events ?? Array.Empty<AnimationEventSource>());
	}

	private static AnimationChannelSource Channel(int bone, params AnimationSampleSource[] samples)
	{
		return new AnimationChannelSource(bone, samples);
	}

	private static AnimationSampleSource Sample(Vector3 translation, Quaternion rotation)
	{
		return new AnimationSampleSource(translation, rotation);
	}

	private static void AssertNear(Vector3 expected, Vector3 actual)
	{
		Assert.InRange(Vector3.Distance(expected, actual), 0f, Epsilon);
	}

	private static void AssertMatrixNear(Matrix expected, Matrix actual)
	{
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
			Assert.InRange(MathF.Abs(expectedValues[index] - actualValues[index]), 0f, Epsilon);
		}
	}
}
