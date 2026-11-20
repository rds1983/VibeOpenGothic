using Microsoft.Xna.Framework;
using VibeOpenGothic.Core;

namespace VibeOpenGothic.Tests.Unit;

public sealed class CoordinateSystemTests
{
	private const float Epsilon = 1e-4f;

	[Fact]
	public void ToEnginePoint_ReflectsZ()
	{
		var result = CoordinateSystem.ToEnginePoint(new Vector3(1, 2, 3));

		AssertNear(new Vector3(1, 2, -3), result);
	}

	[Fact]
	public void ToEngineDirection_ReflectsZ()
	{
		var result = CoordinateSystem.ToEngineDirection(new Vector3(-1, 0.5f, 2));

		AssertNear(new Vector3(-1, 0.5f, -2), result);
	}

	[Fact]
	public void ToEngineNormal_ReflectsAndNormalizes()
	{
		var result = CoordinateSystem.ToEngineNormal(new Vector3(0, 0, 4));

		AssertNear(new Vector3(0, 0, -1), result);
		AssertNear(Vector3.Zero, CoordinateSystem.ToEngineNormal(Vector3.Zero));
	}

	[Fact]
	public void ToEngineYaw_NegatesAndMatchesConvertedDirection()
	{
		for (var step = 0; step < 16; step++)
		{
			var yaw = step * MathF.PI * 2 / 16;
			var gothicDirection = new Vector3(MathF.Cos(yaw), 0, MathF.Sin(yaw));
			var expected = CoordinateSystem.ToEngineDirection(gothicDirection);

			var convertedYaw = CoordinateSystem.ToEngineYaw(yaw);
			var actual = new Vector3(MathF.Cos(convertedYaw), 0, MathF.Sin(convertedYaw));

			AssertNear(expected, actual);
		}
	}

	[Fact]
	public void ToEngineMatrix_ConvertsTranslation()
	{
		var result = CoordinateSystem.ToEngineMatrix(Matrix.CreateTranslation(1, 2, 3));

		AssertNear(new Vector3(1, 2, -3), result.Translation);
	}

	[Fact]
	public void ToEngineMatrix_ConjugatesRotation()
	{
		var rotation = Matrix.CreateRotationY(MathF.PI / 2);
		var converted = CoordinateSystem.ToEngineMatrix(rotation);

		foreach (var axis in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ })
		{
			var expected = CoordinateSystem.ToEngineDirection(Vector3.Transform(axis, rotation));
			var actual = Vector3.Transform(CoordinateSystem.ToEngineDirection(axis), converted);

			AssertNear(expected, actual);
		}
	}

	[Fact]
	public void ToEngineMatrix_WithRotationAndTranslation_MatchesPointConversion()
	{
		var gothic = Matrix.CreateFromYawPitchRoll(0.4f, -0.7f, 0.2f) *
					 Matrix.CreateTranslation(10, -20, 30);
		var point = new Vector3(3, 4, 5);

		var expected = CoordinateSystem.ToEnginePoint(Vector3.Transform(point, gothic));
		var actual = Vector3.Transform(
			CoordinateSystem.ToEnginePoint(point),
			CoordinateSystem.ToEngineMatrix(gothic));

		AssertNear(expected, actual);
	}

	[Fact]
	public void AppliedTwice_RestoresOriginal_DoubleConversionIsDetectable()
	{
		// The conversion is its own inverse. A loader that converts twice leaves
		// the data in Gothic space, which these checks catch.
		var point = new Vector3(1, 2, 3);
		AssertNear(point, CoordinateSystem.ToEnginePoint(CoordinateSystem.ToEnginePoint(point)));

		var matrix = Matrix.CreateFromYawPitchRoll(0.3f, 0.2f, -0.1f) *
					 Matrix.CreateTranslation(4, 5, 6);
		AssertMatrixNear(matrix, CoordinateSystem.ToEngineMatrix(CoordinateSystem.ToEngineMatrix(matrix)));

		var quaternion = Quaternion.CreateFromYawPitchRoll(0.3f, 0.2f, -0.1f);
		var twice = CoordinateSystem.ToEngineQuaternion(CoordinateSystem.ToEngineQuaternion(quaternion));
		Assert.True(MathF.Abs(Quaternion.Dot(quaternion, twice)) > 0.99999f);
	}

	[Fact]
	public void ToEngineQuaternion_MatchesMatrixConversion()
	{
		var quaternion = Quaternion.Normalize(
			Quaternion.CreateFromYawPitchRoll(0.77f, -0.31f, 0.12f));

		var direct = Matrix.CreateFromQuaternion(CoordinateSystem.ToEngineQuaternion(quaternion));
		var viaMatrix = CoordinateSystem.ToEngineMatrix(Matrix.CreateFromQuaternion(quaternion));

		AssertMatrixNear(viaMatrix, direct);
	}

	[Fact]
	public void ToEngineQuaternion_PreservesRotationLength()
	{
		var quaternion = Quaternion.Normalize(
			Quaternion.CreateFromYawPitchRoll(0.2f, 0.5f, -0.8f));

		var converted = CoordinateSystem.ToEngineQuaternion(quaternion);

		Assert.InRange(MathF.Abs(converted.Length() - 1f), 0f, Epsilon);
	}

	[Fact]
	public void ToEngineBounds_ReflectsAndSwapsZRange()
	{
		var bounds = new BoundingBox(new Vector3(1, 2, 3), new Vector3(4, 5, 6));

		var converted = CoordinateSystem.ToEngineBounds(bounds);

		AssertNear(new Vector3(1, 2, -6), converted.Min);
		AssertNear(new Vector3(4, 5, -3), converted.Max);
	}

	[Fact]
	public void ReverseTriangleWinding_SwapsSecondAndThirdIndices()
	{
		Assert.Equal((0, 2, 1), CoordinateSystem.ReverseTriangleWinding(0, 1, 2));
	}

	[Fact]
	public void ReverseWindingInPlace_ReversesEveryTriangle()
	{
		var indices = new[] { 0, 1, 2, 3, 4, 5 };

		CoordinateSystem.ReverseWindingInPlace(indices);

		Assert.Equal(new[] { 0, 2, 1, 3, 5, 4 }, indices);
	}

	[Fact]
	public void ReverseWinding_ReturnsCopyAndLeavesSourceUnchanged()
	{
		var source = new[] { 7, 8, 9 };

		var result = CoordinateSystem.ReverseWinding(source);

		Assert.Equal(new[] { 7, 9, 8 }, result);
		Assert.Equal(new[] { 7, 8, 9 }, source);
	}

	[Fact]
	public void ReflectedNormalAndReversedWinding_AreConsistent()
	{
		var a = new Vector3(0, 0, 0);
		var b = new Vector3(1, 0, 0);
		var c = new Vector3(0, 1, 0);
		var gothicNormal = Vector3.Normalize(Vector3.Cross(b - a, c - a));

		var convertedNormal = CoordinateSystem.ToEngineNormal(gothicNormal);
		var positions = new[]
		{
			CoordinateSystem.ToEnginePoint(a),
			CoordinateSystem.ToEnginePoint(b),
			CoordinateSystem.ToEnginePoint(c),
		};
		var (i0, i1, i2) = CoordinateSystem.ReverseTriangleWinding(0, 1, 2);
		var windingNormal = Vector3.Normalize(
			Vector3.Cross(positions[i1] - positions[i0], positions[i2] - positions[i0]));

		AssertNear(convertedNormal, windingNormal);
	}

	private static void AssertNear(Vector3 expected, Vector3 actual, float epsilon = Epsilon)
	{
		Assert.InRange(Vector3.Distance(expected, actual), 0f, epsilon);
	}

	private static void AssertMatrixNear(Matrix expected, Matrix actual, float epsilon = Epsilon)
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
			Assert.InRange(
				MathF.Abs(expectedValues[index] - actualValues[index]),
				0f,
				epsilon);
		}
	}
}
