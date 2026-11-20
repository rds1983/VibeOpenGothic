using Microsoft.Xna.Framework;
using VibeOpenGothic.World;
using ZenKit.Util;

namespace VibeOpenGothic.Tests.Unit;

public sealed class GothicVobTransformTests
{
	private const float Epsilon = 1e-4f;

	[Fact]
	public void ToEngineTransform_ReflectsPosition()
	{
		var transform = GothicVobTransform.ToEngineTransform(
			new System.Numerics.Vector3(1, 2, 3),
			Identity());

		AssertNear(new Vector3(1, 2, -3), transform.Translation);
	}

	[Fact]
	public void StartDirection_UsesThirdRotationColumn()
	{
		var rotation = new Matrix3x3(
			0f, 0f, -1f,
			0f, 1f, 0f,
			1f, 0f, 0f);

		var direction = GothicVobTransform.StartDirection(rotation);

		AssertNear(Vector3.UnitX, direction);
	}

	[Fact]
	public void Compose_AppliesLocalBeforeParent()
	{
		var parent = Matrix.CreateTranslation(10, 0, 0);
		var local = Matrix.CreateTranslation(0, 0, 5);

		var result = GothicVobTransform.Compose(parent, local);

		AssertNear(new Vector3(10, 0, 5), result.Translation);
	}

	private static Matrix3x3 Identity()
	{
		return new Matrix3x3(
			1f, 0f, 0f,
			0f, 1f, 0f,
			0f, 0f, 1f);
	}

	private static void AssertNear(Vector3 expected, Vector3 actual)
	{
		Assert.InRange(Vector3.Distance(expected, actual), 0f, Epsilon);
	}
}
