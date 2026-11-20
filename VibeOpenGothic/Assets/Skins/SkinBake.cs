using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace VibeOpenGothic.Assets.Skins;

/// <summary>
/// Bakes Gothic's four local positions per vertex into one bind-pose position
/// for standard linear blend skinning. Inputs are engine-space (RHS).
/// </summary>
public static class SkinBake
{
	private const float WeightEpsilon = 1e-6f;
	private const float RenormalizeEpsilon = 1e-3f;

	public static SkinBakeResult Bake(
		IReadOnlyList<IReadOnlyList<SkinInfluence>> vertices,
		IReadOnlyList<Matrix> bindGlobals,
		float inconsistencyWarningThreshold = 2f)
	{
		ArgumentNullException.ThrowIfNull(vertices);
		ArgumentNullException.ThrowIfNull(bindGlobals);

		var boneCount = bindGlobals.Count;
		var baked = new SkinBakedVertex[vertices.Count];

		Span<Vector3> transformed = stackalloc Vector3[4];

		var maxInconsistency = 0f;
		var maxWeightedInconsistency = 0f;
		var inconsistentVertexCount = 0;
		var renormalizedWeightCount = 0;
		var zeroWeightCount = 0;
		var outOfRangeBoneCount = 0;
		var truncatedInfluenceCount = 0;

		for (var vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++)
		{
			transformed.Clear();
			var sourceInfluences = vertices[vertexIndex];
			var merged = new Dictionary<int, SkinInfluence>();

			if (sourceInfluences is not null)
			{
				foreach (var influence in sourceInfluences)
				{
					if (influence.BoneIndex < 0 ||
						influence.BoneIndex >= boneCount ||
						influence.BoneIndex > byte.MaxValue)
					{
						outOfRangeBoneCount++;
						continue;
					}

					var weight = float.IsFinite(influence.Weight)
						? Math.Max(0f, influence.Weight)
						: 0f;

					if (merged.TryGetValue(influence.BoneIndex, out var existing))
					{
						merged[influence.BoneIndex] = existing with
						{
							Weight = existing.Weight + weight,
						};
					}
					else
					{
						merged[influence.BoneIndex] = new SkinInfluence(
							influence.BoneIndex,
							weight,
							influence.LocalPosition);
					}
				}
			}

			var selected = merged.Values
				.OrderByDescending(influence => influence.Weight)
				.Take(4)
				.ToList();

			if (merged.Count > 4)
			{
				truncatedInfluenceCount += merged.Count - 4;
			}

			var sum = selected.Sum(influence => influence.Weight);
			if (sum <= WeightEpsilon)
			{
				zeroWeightCount++;

				if (selected.Count > 0)
				{
					selected = new List<SkinInfluence>
					{
						selected[0] with { Weight = 1f },
					};
				}
				else
				{
					selected = new List<SkinInfluence>
					{
						new(0, 1f, Vector3.Zero),
					};
				}

				sum = 1f;
			}
			else
			{
				if (MathF.Abs(sum - 1f) > RenormalizeEpsilon)
				{
					renormalizedWeightCount++;
				}

				for (var index = 0; index < selected.Count; index++)
				{
					selected[index] = selected[index] with
					{
						Weight = selected[index].Weight / sum,
					};
				}
			}

			var w0 = 0f;
			var w1 = 0f;
			var w2 = 0f;
			var w3 = 0f;
			byte b0 = 0, b1 = 0, b2 = 0, b3 = 0;
			var p = Vector3.Zero;

			for (var index = 0; index < 4; index++)
			{
				if (index < selected.Count)
				{
					var influence = selected[index];
					var weight = influence.Weight;
					var bone = (byte)influence.BoneIndex;

					switch (index)
					{
						case 0:
							w0 = weight;
							b0 = bone;
							break;
						case 1:
							w1 = weight;
							b1 = bone;
							break;
						case 2:
							w2 = weight;
							b2 = bone;
							break;
						default:
							w3 = weight;
							b3 = bone;
							break;
					}

					if (influence.BoneIndex < boneCount)
					{
						transformed[index] = Vector3.Transform(
							influence.LocalPosition,
							bindGlobals[influence.BoneIndex]);
					}

					p += transformed[index] * weight;
				}
			}

			var vertexInconsistency = 0f;
			var vertexWeightedInconsistency = 0f;
			for (var index = 0; index < selected.Count; index++)
			{
				if (selected[index].Weight <= 0)
				{
					continue;
				}

				var deviation = Vector3.Distance(transformed[index], p);
				vertexInconsistency = MathF.Max(vertexInconsistency, deviation);
				vertexWeightedInconsistency += deviation * selected[index].Weight;
			}

			maxInconsistency = MathF.Max(maxInconsistency, vertexInconsistency);
			maxWeightedInconsistency = MathF.Max(
				maxWeightedInconsistency,
				vertexWeightedInconsistency);
			if (vertexInconsistency > inconsistencyWarningThreshold)
			{
				inconsistentVertexCount++;
			}

			baked[vertexIndex] = new SkinBakedVertex(
				p,
				new Vector4(w0, w1, w2, w3),
				new Byte4(b0, b1, b2, b3));
		}

		var diagnostics = new SkinBakeDiagnostics(
			vertices.Count,
			boneCount,
			maxInconsistency,
			maxWeightedInconsistency,
			inconsistentVertexCount,
			renormalizedWeightCount,
			zeroWeightCount,
			outOfRangeBoneCount,
			truncatedInfluenceCount);

		return new SkinBakeResult(baked, diagnostics);
	}
}
