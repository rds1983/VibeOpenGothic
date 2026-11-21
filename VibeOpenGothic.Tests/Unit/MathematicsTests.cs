using Microsoft.Xna.Framework;
using VibeOpenGothic.Core;
using NumericsMatrix4x4 = System.Numerics.Matrix4x4;
using NumericsVector2 = System.Numerics.Vector2;
using NumericsVector3 = System.Numerics.Vector3;

namespace VibeOpenGothic.Tests.Unit;

public sealed class MathematicsTests
{
	private static readonly BoundingBox Box = new(
		new Vector3(-1f, -2f, -3f),
		new Vector3(1f, 2f, 3f));

	[Fact]
	public void Transform_Identity_ReturnsSameBounds()
	{
		var result = Box.Transform(Matrix.Identity);

		Assert.Equal(Box.Min, result.Min);
		Assert.Equal(Box.Max, result.Max);
	}

	[Fact]
	public void Transform_Translation_MovesBothCorners()
	{
		var result = Box.Transform(Matrix.CreateTranslation(10f, 20f, 30f));

		Assert.Equal(new Vector3(9f, 18f, 27f), result.Min);
		Assert.Equal(new Vector3(11f, 22f, 33f), result.Max);
	}

	[Fact]
	public void Transform_Scale_ScalesBothCorners()
	{
		var result = Box.Transform(Matrix.CreateScale(2f));

		Assert.Equal(new Vector3(-2f, -4f, -6f), result.Min);
		Assert.Equal(new Vector3(2f, 4f, 6f), result.Max);
	}

	[Fact]
	public void Transform_QuarterTurnY_SwapsXAndZExtents()
	{
		var result = Box.Transform(Matrix.CreateRotationY(MathHelper.PiOver2));

		Assert.Equal(-3f, result.Min.X, 4);
		Assert.Equal(-2f, result.Min.Y, 4);
		Assert.Equal(-1f, result.Min.Z, 4);
		Assert.Equal(3f, result.Max.X, 4);
		Assert.Equal(2f, result.Max.Y, 4);
		Assert.Equal(1f, result.Max.Z, 4);
	}

	[Fact]
	public void Expand_GrowsBothCorners()
	{
		var result = Box.Expand(5f);

		Assert.Equal(new Vector3(-6f, -7f, -8f), result.Min);
		Assert.Equal(new Vector3(6f, 7f, 8f), result.Max);
	}

	[Fact]
	public void Include_GrowsToContainPoint()
	{
		var result = Box.Include(new Vector3(5f, 0f, -10f));

		Assert.Equal(new Vector3(-1f, -2f, -10f), result.Min);
		Assert.Equal(new Vector3(5f, 2f, 3f), result.Max);
	}

	[Fact]
	public void IsFinite_RejectsNaNAndInfinity()
	{
		Assert.True(Vector3.One.IsFinite());
		Assert.False(new Vector3(float.NaN, 0f, 0f).IsFinite());
		Assert.False(new Quaternion(0f, float.PositiveInfinity, 0f, 1f).IsFinite());
		Assert.False(new Matrix(
			float.PositiveInfinity, 0f, 0f, 0f,
			0f, 1f, 0f, 0f,
			0f, 0f, 1f, 0f,
			0f, 0f, 0f, 1f).IsFinite());
	}

	[Fact]
	public void ToXna_Vector2AndVector3_CopyComponents()
	{
		Assert.Equal(new Vector2(1f, 2f), new NumericsVector2(1f, 2f).ToXna());
		Assert.Equal(
			new Vector3(1f, 2f, 3f),
			new NumericsVector3(1f, 2f, 3f).ToXna());
	}

	[Fact]
	public void ToXna_Matrix4x4_TransposesTranslationRow()
	{
		var source = NumericsMatrix4x4.Identity;
		source.M14 = 7f;
		source.M24 = 8f;
		source.M34 = 9f;

		var result = source.ToXna();

		Assert.Equal(7f, result.M41);
		Assert.Equal(8f, result.M42);
		Assert.Equal(9f, result.M43);
		Assert.Equal(1f, result.M44);
	}
}
