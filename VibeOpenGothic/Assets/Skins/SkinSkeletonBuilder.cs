using Microsoft.Xna.Framework;
using VibeOpenGothic.Core;

namespace VibeOpenGothic.Assets.Skins;

public static class SkinSkeletonBuilder
{
	public static SkinSkeleton Build(
		IReadOnlyList<SkinHierarchyNode> nodes,
		Vector3 rootTranslation)
	{
		ArgumentNullException.ThrowIfNull(nodes);

		var warnings = new List<string>();
		if (nodes.Count == 0)
		{
			warnings.Add("Skeleton has no nodes.");
			return new SkinSkeleton(Array.Empty<SkinBone>(), warnings);
		}

		var locals = new Matrix[nodes.Count];
		var rootEngine = CoordinateSystem.ToEnginePoint(rootTranslation);

		for (var index = 0; index < nodes.Count; index++)
		{
			var local = CoordinateSystem.ToEngineMatrix(nodes[index].LocalTransform);

			if (nodes[index].ParentIndex < 0)
			{
				local.M41 += rootEngine.X;
				local.M42 += rootEngine.Y;
				local.M43 += rootEngine.Z;
			}

			locals[index] = local;
		}

		var globals = new Matrix[nodes.Count];
		var state = new byte[nodes.Count];

		for (var index = 0; index < nodes.Count; index++)
		{
			ComputeGlobal(index);
		}

		var bones = new SkinBone[nodes.Count];
		for (var index = 0; index < nodes.Count; index++)
		{
			var inverse = Matrix.Invert(globals[index]);
			if (!IsFinite(inverse))
			{
				warnings.Add($"Bone '{nodes[index].Name}' has a singular bind transform; using identity inverse.");
				inverse = Matrix.Identity;
			}

			bones[index] = new SkinBone(
				nodes[index].Name,
				nodes[index].ParentIndex,
				globals[index],
				inverse);
		}

		return new SkinSkeleton(bones, warnings);

		Matrix ComputeGlobal(int index)
		{
			if (state[index] == 2)
			{
				return globals[index];
			}

			if (state[index] == 1)
			{
				warnings.Add($"Cycle in skeleton hierarchy at node {index}; using local transform.");
				return locals[index];
			}

			state[index] = 1;

			var parent = nodes[index].ParentIndex;
			if (parent < 0)
			{
				globals[index] = locals[index];
			}
			else if (parent >= nodes.Count)
			{
				warnings.Add($"Bone '{nodes[index].Name}' references missing parent {parent}; treating it as a root.");
				globals[index] = locals[index];
			}
			else
			{
				globals[index] = locals[index] * ComputeGlobal(parent);
			}

			state[index] = 2;
			return globals[index];
		}
	}

	private static bool IsFinite(Matrix matrix)
	{
		return float.IsFinite(matrix.M11) && float.IsFinite(matrix.M12) &&
			   float.IsFinite(matrix.M13) && float.IsFinite(matrix.M14) &&
			   float.IsFinite(matrix.M21) && float.IsFinite(matrix.M22) &&
			   float.IsFinite(matrix.M23) && float.IsFinite(matrix.M24) &&
			   float.IsFinite(matrix.M31) && float.IsFinite(matrix.M32) &&
			   float.IsFinite(matrix.M33) && float.IsFinite(matrix.M34) &&
			   float.IsFinite(matrix.M41) && float.IsFinite(matrix.M42) &&
			   float.IsFinite(matrix.M43) && float.IsFinite(matrix.M44);
	}
}
