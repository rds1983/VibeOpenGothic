using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Core;

/// <summary>
/// Converts Gothic's left-handed, Y-up data to MonoGame's right-handed, Y-up
/// space. The conversion is a reflection across the XY plane:
/// <c>M = diag(1, 1, -1)</c>.
///
/// Apply it exactly once, in the loader that reads the data. Every method here
/// is an involution (applying it twice restores the input), which is why
/// double-converting must be caught by tests rather than at runtime. Triangle
/// winding is the exception: it must be reversed exactly once.
/// </summary>
public static class CoordinateSystem
{
    private static readonly Matrix Reflection = Matrix.CreateScale(1f, 1f, -1f);

    /// <summary>Reflects a point: <c>(x, y, z) -&gt; (x, y, -z)</c>.</summary>
    public static Vector3 ToEnginePoint(Vector3 gothicPoint)
    {
        return new Vector3(gothicPoint.X, gothicPoint.Y, -gothicPoint.Z);
    }

    /// <summary>Reflects a direction vector. Same components as a point.</summary>
    public static Vector3 ToEngineDirection(Vector3 gothicDirection)
    {
        return new Vector3(gothicDirection.X, gothicDirection.Y, -gothicDirection.Z);
    }

    /// <summary>
    /// Reflects a normal and renormalizes it. Winding must be reversed at the
    /// same time so the reflected normal remains the outward normal.
    /// </summary>
    public static Vector3 ToEngineNormal(Vector3 gothicNormal)
    {
        var reflected = new Vector3(gothicNormal.X, gothicNormal.Y, -gothicNormal.Z);
        return reflected.LengthSquared() > 0
            ? Vector3.Normalize(reflected)
            : Vector3.Zero;
    }

    /// <summary>
    /// A heading in the XZ plane becomes its negated value: a Gothic direction
    /// <c>(cos a, 0, sin a)</c> reflects to <c>(cos -a, 0, sin -a)</c>.
    /// </summary>
    public static float ToEngineYaw(float gothicYawRadians)
    {
        return -gothicYawRadians;
    }

    /// <summary>
    /// Converts a row-vector transform (MonoGame convention):
    /// <c>R' = M R M</c>, <c>t' = t M</c>. Includes the translation row.
    /// </summary>
    public static Matrix ToEngineMatrix(Matrix gothicMatrix)
    {
        return Reflection * gothicMatrix * Reflection;
    }

    /// <summary>
    /// Conjugates a rotation quaternion by the reflection. For
    /// <c>(x, y, z, w)</c> this is <c>(-x, -y, z, w)</c>, up to sign.
    /// </summary>
    public static Quaternion ToEngineQuaternion(Quaternion gothicQuaternion)
    {
        return new Quaternion(
            -gothicQuaternion.X,
            -gothicQuaternion.Y,
            gothicQuaternion.Z,
            gothicQuaternion.W);
    }

    /// <summary>
    /// Converts an axis-aligned box. X/Y are unchanged; the Z range is negated
    /// and its minimum/maximum swap.
    /// </summary>
    public static BoundingBox ToEngineBounds(BoundingBox gothicBounds)
    {
        var min = ToEnginePoint(gothicBounds.Min);
        var max = ToEnginePoint(gothicBounds.Max);

        return new BoundingBox(
            new Vector3(
                MathF.Min(min.X, max.X),
                MathF.Min(min.Y, max.Y),
                MathF.Min(min.Z, max.Z)),
            new Vector3(
                MathF.Max(min.X, max.X),
                MathF.Max(min.Y, max.Y),
                MathF.Max(min.Z, max.Z)));
    }

    /// <summary>
    /// Reverses the winding of every triangle in a triangle list:
    /// <c>(a, b, c)</c> becomes <c>(a, c, b)</c>.
    /// </summary>
    public static void ReverseWindingInPlace(Span<int> indices)
    {
        for (var index = 0; index + 2 < indices.Length; index += 3)
        {
            (indices[index + 1], indices[index + 2]) =
                (indices[index + 2], indices[index + 1]);
        }
    }

    /// <summary>Returns a reversed-winding copy of the triangle list.</summary>
    public static int[] ReverseWinding(ReadOnlySpan<int> indices)
    {
        var copy = indices.ToArray();
        ReverseWindingInPlace(copy);
        return copy;
    }

    /// <summary>Reverses a single triangle's winding.</summary>
    public static (int A, int B, int C) ReverseTriangleWinding(int a, int b, int c)
    {
        return (a, c, b);
    }
}
