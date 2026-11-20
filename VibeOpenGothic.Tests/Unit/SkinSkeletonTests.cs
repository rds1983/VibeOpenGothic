using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Skins;
using VibeOpenGothic.Core;

namespace VibeOpenGothic.Tests.Unit;

public sealed class SkinSkeletonTests
{
	private const float Epsilon = 1e-4f;

	[Fact]
	public void Build_AppliesReflectedRootTranslation()
	{
		var nodes = new[]
		{
			new SkinHierarchyNode("root", -1, Matrix.Identity),
		};

		var skeleton = SkinSkeletonBuilder.Build(nodes, new Vector3(0, 0, 5));

		Assert.Equal(1, skeleton.BoneCount);
		AssertNear(new Vector3(0, 0, -5), skeleton.Bones[0].Local.Translation);
		AssertNear(new Vector3(0, 0, -5), skeleton.Bones[0].BindGlobal.Translation);
		AssertNear(new Vector3(0, 0, 5), skeleton.Bones[0].InverseBindGlobal.Translation);
	}

	[Fact]
	public void Build_ComposesParentAndChild()
	{
		var nodes = new[]
		{
			new SkinHierarchyNode("root", -1, Matrix.Identity),
			new SkinHierarchyNode("child", 0, Matrix.CreateTranslation(0, 0, 5)),
		};

		var skeleton = SkinSkeletonBuilder.Build(nodes, Vector3.Zero);

		AssertNear(new Vector3(0, 0, -5), skeleton.Bones[1].BindGlobal.Translation);
	}

	[Fact]
	public void Build_ComposesChildBeforeParent()
	{
		var rootGothic = Matrix.CreateRotationY(MathHelper.PiOver2);
		var childGothic = Matrix.CreateTranslation(5, 0, 0);
		var nodes = new[]
		{
			new SkinHierarchyNode("root", -1, rootGothic),
			new SkinHierarchyNode("child", 0, childGothic),
		};

		var skeleton = SkinSkeletonBuilder.Build(nodes, Vector3.Zero);
		var expected = CoordinateSystem.ToEngineMatrix(childGothic * rootGothic);

		AssertMatrixNear(expected, skeleton.Bones[1].BindGlobal);
	}

	[Fact]
	public void Build_HandlesUnorderedHierarchy()
	{
		var nodes = new[]
		{
			new SkinHierarchyNode("child", 1, Matrix.CreateTranslation(0, 0, 5)),
			new SkinHierarchyNode("root", -1, Matrix.CreateTranslation(1, 0, 0)),
		};

		var skeleton = SkinSkeletonBuilder.Build(nodes, Vector3.Zero);

		AssertNear(new Vector3(1, 0, -5), skeleton.Bones[0].BindGlobal.Translation);
		AssertNear(new Vector3(1, 0, 0), skeleton.Bones[1].BindGlobal.Translation);
	}

	[Fact]
	public void Build_InverseBind_MultipliesBackToIdentity()
	{
		var nodes = new[]
		{
			new SkinHierarchyNode("root", -1, Matrix.Identity),
			new SkinHierarchyNode("child", 0, Matrix.CreateFromYawPitchRoll(0.3f, -0.2f, 0.7f) *
				Matrix.CreateTranslation(2, -3, 4)),
		};

		var skeleton = SkinSkeletonBuilder.Build(nodes, new Vector3(1, 2, 3));
		var product = skeleton.Bones[1].BindGlobal * skeleton.Bones[1].InverseBindGlobal;

		AssertMatrixNear(Matrix.Identity, product);
	}

	[Fact]
	public void Build_Cycle_ReportsWarningWithoutHanging()
	{
		var nodes = new[]
		{
			new SkinHierarchyNode("a", 1, Matrix.Identity),
			new SkinHierarchyNode("b", 0, Matrix.Identity),
		};

		var skeleton = SkinSkeletonBuilder.Build(nodes, Vector3.Zero);

		Assert.Equal(2, skeleton.BoneCount);
		Assert.NotEmpty(skeleton.Warnings);
	}

	[Fact]
	public void Build_MissingParent_ReportsWarningAndUsesLocalTransform()
	{
		var nodes = new[]
		{
			new SkinHierarchyNode("orphan", 99, Matrix.CreateTranslation(1, 2, 3)),
		};

		var skeleton = SkinSkeletonBuilder.Build(nodes, Vector3.Zero);

		Assert.NotEmpty(skeleton.Warnings);
		AssertNear(new Vector3(1, 2, -3), skeleton.Bones[0].BindGlobal.Translation);
	}

	[Fact]
	public void Build_EmptyHierarchy_Warns()
	{
		var skeleton = SkinSkeletonBuilder.Build(Array.Empty<SkinHierarchyNode>(), Vector3.Zero);

		Assert.Equal(0, skeleton.BoneCount);
		Assert.NotEmpty(skeleton.Warnings);
	}

	[Fact]
	public void ExceedsSkinnedEffectLimit_DetectsMoreThan72Bones()
	{
		var nodes = Enumerable.Range(0, 73)
			.Select(index => new SkinHierarchyNode($"bone{index}", -1, Matrix.Identity))
			.ToArray();

		var skeleton = SkinSkeletonBuilder.Build(nodes, Vector3.Zero);

		Assert.True(skeleton.ExceedsSkinnedEffectLimit);
	}

	[Fact]
	public void FindBone_ReturnsIndexOrMinusOne()
	{
		var nodes = new[]
		{
			new SkinHierarchyNode("root", -1, Matrix.Identity),
			new SkinHierarchyNode("BIP01 HEAD", 0, Matrix.Identity),
		};

		var skeleton = SkinSkeletonBuilder.Build(nodes, Vector3.Zero);

		Assert.Equal(1, skeleton.FindBone("BIP01 HEAD"));
		Assert.Equal(-1, skeleton.FindBone("missing"));
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
