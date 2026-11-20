using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// Right-handed camera. All source data is already converted to engine space, so
/// this uses MonoGame's CreateLookAt/CreatePerspectiveFieldOfView directly.
/// </summary>
public sealed class GothicCamera
{
	public const float DefaultFieldOfView = 67.5f * MathF.PI / 180f;
	public const float DefaultNearPlane = 1f;
	public const float DefaultFarPlane = 20000f;

	private float _aspectRatio = 1f;

	public GothicCamera(Vector3 position, Vector3 target)
	{
		Position = position;
		Target = target;
	}

	public Vector3 Position { get; set; }

	public Vector3 Target { get; set; }

	public Vector3 Up { get; set; } = Vector3.Up;

	public float FieldOfView { get; set; } = DefaultFieldOfView;

	public float NearPlane { get; set; } = DefaultNearPlane;

	public float FarPlane { get; set; } = DefaultFarPlane;

	public float AspectRatio => _aspectRatio;

	public Matrix View => Matrix.CreateLookAt(Position, Target, Up);

	public Matrix Projection =>
		Matrix.CreatePerspectiveFieldOfView(FieldOfView, _aspectRatio, NearPlane, FarPlane);

	public void SetAspectRatio(float aspectRatio)
	{
		if (float.IsFinite(aspectRatio) && aspectRatio > 0f)
		{
			_aspectRatio = aspectRatio;
		}
	}

	public void LookAt(Vector3 position, Vector3 target)
	{
		Position = position;
		Target = target;
	}
}
