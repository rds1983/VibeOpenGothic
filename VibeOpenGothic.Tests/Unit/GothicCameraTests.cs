using Microsoft.Xna.Framework;
using VibeOpenGothic.Rendering;

namespace VibeOpenGothic.Tests.Unit;

public sealed class GothicCameraTests
{
	private const float Epsilon = 1e-4f;

	[Fact]
	public void View_IsRightHanded()
	{
		var camera = new GothicCamera(new Vector3(0, 0, 10), Vector3.Zero);

		var origin = Vector3.Transform(Vector3.Zero, camera.View);

		AssertNear(new Vector3(0, 0, -10), origin);
	}

	[Fact]
	public void SetAspectRatio_UpdatesProjection()
	{
		var camera = new GothicCamera(new Vector3(0, 0, 10), Vector3.Zero)
		{
			FieldOfView = MathHelper.PiOver2,
		};

		camera.SetAspectRatio(2f);

		Assert.Equal(2f, camera.AspectRatio, 5);
		Assert.InRange(camera.Projection.M11, 0.5f - Epsilon, 0.5f + Epsilon);
	}

	[Fact]
	public void SetAspectRatio_IgnoresInvalidValues()
	{
		var camera = new GothicCamera(new Vector3(0, 0, 10), Vector3.Zero);
		camera.SetAspectRatio(16f / 9f);

		camera.SetAspectRatio(0f);
		camera.SetAspectRatio(float.NaN);

		Assert.Equal(16f / 9f, camera.AspectRatio, 5);
	}

	[Fact]
	public void LookAt_ReplacesPositionAndTarget()
	{
		var camera = new GothicCamera(Vector3.Zero, Vector3.UnitZ);

		camera.LookAt(new Vector3(1, 2, 3), new Vector3(4, 5, 6));

		Assert.Equal(new Vector3(1, 2, 3), camera.Position);
		Assert.Equal(new Vector3(4, 5, 6), camera.Target);
	}

	private static void AssertNear(Vector3 expected, Vector3 actual)
	{
		Assert.InRange(Vector3.Distance(expected, actual), 0f, Epsilon);
	}
}
