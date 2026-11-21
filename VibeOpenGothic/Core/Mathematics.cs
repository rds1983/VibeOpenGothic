using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Core;

internal static class Mathematics
{
	private static readonly Vector3[] _corners = new Vector3[8];

	public static BoundingBox Transform(this BoundingBox bounds, Matrix transform)
	{
		bounds.GetCorners(_corners);

		var min = new Vector3(float.PositiveInfinity);
		var max = new Vector3(float.NegativeInfinity);
		foreach (var corner in _corners)
		{
			var point = Vector3.Transform(corner, transform);
			min = Vector3.Min(min, point);
			max = Vector3.Max(max, point);
		}

		return new BoundingBox(min, max);
	}

	public static BoundingBox Expand(this BoundingBox bounds, float margin)
	{
		var offset = new Vector3(margin);
		return new BoundingBox(bounds.Min - offset, bounds.Max + offset);
	}

	public static BoundingBox Include(this BoundingBox bounds, Vector3 point)
	{
		return new BoundingBox(
			Vector3.Min(bounds.Min, point),
			Vector3.Max(bounds.Max, point));
	}

	public static bool IsFinite(this Vector3 value)
	{
		return float.IsFinite(value.X) &&
			float.IsFinite(value.Y) &&
			float.IsFinite(value.Z);
	}

	public static bool IsFinite(this Quaternion value)
	{
		return float.IsFinite(value.X) &&
			float.IsFinite(value.Y) &&
			float.IsFinite(value.Z) &&
			float.IsFinite(value.W);
	}

	public static bool IsFinite(this Matrix value)
	{
		return float.IsFinite(value.M11) && float.IsFinite(value.M12) &&
			float.IsFinite(value.M13) && float.IsFinite(value.M14) &&
			float.IsFinite(value.M21) && float.IsFinite(value.M22) &&
			float.IsFinite(value.M23) && float.IsFinite(value.M24) &&
			float.IsFinite(value.M31) && float.IsFinite(value.M32) &&
			float.IsFinite(value.M33) && float.IsFinite(value.M34) &&
			float.IsFinite(value.M41) && float.IsFinite(value.M42) &&
			float.IsFinite(value.M43) && float.IsFinite(value.M44);
	}

	public static Vector2 ToXna(this System.Numerics.Vector2 value)
	{
		return new Vector2(value.X, value.Y);
	}

	public static Vector3 ToXna(this System.Numerics.Vector3 value)
	{
		return new Vector3(value.X, value.Y, value.Z);
	}

	public static Matrix ToXna(this System.Numerics.Matrix4x4 value)
	{
		// ZenKit exposes the hierarchy transform as a column-vector matrix
		// (translation in M14/M24/M34). MonoGame matrices are row-vector, so
		// transpose while copying.
		return new Matrix(
			value.M11, value.M21, value.M31, value.M41,
			value.M12, value.M22, value.M32, value.M42,
			value.M13, value.M23, value.M33, value.M43,
			value.M14, value.M24, value.M34, value.M44);
	}
}
