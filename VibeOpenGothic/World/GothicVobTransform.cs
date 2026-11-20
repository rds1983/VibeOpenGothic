using Microsoft.Xna.Framework;
using VibeOpenGothic.Core;
using ZenKit.Util;
using ZenKit.Vobs;

namespace VibeOpenGothic.World;

/// <summary>
/// Converts Gothic VOB position and 3x3 rotation to an engine-space transform.
/// ZenKit stores the rotation columns; MonoGame is row-vector, so the columns
/// become rows before the handedness reflection is applied.
/// </summary>
public static class GothicVobTransform
{
	public static Matrix ToEngineTransform(IVirtualObject vob)
	{
		ArgumentNullException.ThrowIfNull(vob);
		return ToEngineTransform(vob.Position, vob.Rotation);
	}

	public static Matrix ToEngineTransform(
		System.Numerics.Vector3 position,
		Matrix3x3 rotation)
	{
		var gothic = new Matrix(
			rotation.M11, rotation.M21, rotation.M31, 0f,
			rotation.M12, rotation.M22, rotation.M32, 0f,
			rotation.M13, rotation.M23, rotation.M33, 0f,
			position.X, position.Y, position.Z, 1f);

		return CoordinateSystem.ToEngineMatrix(gothic);
	}

	/// <summary>
	/// Startpoint direction as stored by Gothic: the third rotation column,
	/// converted directly to engine space. This mirrors World::addStartPoint.
	/// </summary>
	public static Vector3 StartDirection(IVirtualObject vob)
	{
		ArgumentNullException.ThrowIfNull(vob);
		return StartDirection(vob.Rotation);
	}

	public static Vector3 StartDirection(Matrix3x3 rotation)
	{
		var gothic = new Vector3(
			rotation.M31,
			rotation.M32,
			rotation.M33);
		var engine = CoordinateSystem.ToEngineDirection(gothic);
		return engine.LengthSquared() > 0f ? Vector3.Normalize(engine) : -Vector3.UnitZ;
	}

	public static Matrix Compose(Matrix parent, Matrix local)
	{
		return local * parent;
	}
}
