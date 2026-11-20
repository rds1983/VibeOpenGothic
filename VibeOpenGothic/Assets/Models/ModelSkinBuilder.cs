using DigitalRiseModel;
using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Skins;

namespace VibeOpenGothic.Assets.Models;

/// <summary>
/// Compacts a baked mesh's weighted bones into a per-mesh <see cref="DrSkin"/>.
/// Meshes that reference more than <see cref="SkinnedEffectJointLimit"/> bones
/// keep the strongest 72 and renormalize the remaining weights.
/// </summary>
public static class ModelSkinBuilder
{
	public const int SkinnedEffectJointLimit = 72;

	public static ModelSkinBuildResult Build(
		SkinnedMeshData geometry,
		DrModel model,
		int skinIndex,
		string modelPath,
		ICollection<string> warnings)
	{
		ArgumentNullException.ThrowIfNull(geometry);
		ArgumentNullException.ThrowIfNull(model);
		ArgumentNullException.ThrowIfNull(warnings);

		var totals = new Dictionary<int, float>();
		foreach (var vertex in geometry.Vertices.Span)
		{
			AddBoneWeight(totals, vertex.BlendIndices.PackedValue, vertex.BlendWeights);
		}

		var jointCount = totals.Count;
		var exceedsLimit = jointCount > SkinnedEffectJointLimit;

		IReadOnlyList<int> selected;
		if (exceedsLimit)
		{
			selected = totals
				.OrderByDescending(pair => pair.Value)
				.ThenBy(pair => pair.Key)
				.Take(SkinnedEffectJointLimit)
				.Select(pair => pair.Key)
				.ToList();
			warnings.Add(
				$"Model '{modelPath}' mesh {skinIndex} uses {jointCount} bones; " +
				$"keeping the {SkinnedEffectJointLimit} with the largest total weight.");
		}
		else
		{
			selected = totals.Keys.OrderBy(index => index).ToList();
		}

		if (selected.Count == 0)
		{
			selected = new[] { 0 };
		}

		var jointMap = new Dictionary<int, byte>(selected.Count);
		for (var index = 0; index < selected.Count; index++)
		{
			jointMap[selected[index]] = (byte)index;
		}

		var remapped = new SkinnedVertex[geometry.Vertices.Length];
		for (var index = 0; index < remapped.Length; index++)
		{
			remapped[index] = RemapVertex(
				geometry.Vertices.Span[index],
				jointMap);
		}

		var bindInstance = new DrModelInstance(model);
		var joints = new DrSkinJoint[selected.Count];
		for (var index = 0; index < selected.Count; index++)
		{
			var boneIndex = selected[index];
			joints[index] = new DrSkinJoint(
				model.Bones[boneIndex],
				Matrix.Invert(bindInstance.GetBoneGlobalTransform(boneIndex)));
		}

		var skin = new DrSkin(skinIndex, joints)
		{
			Name = $"skin{skinIndex}",
		};

		var resultGeometry = new SkinnedMeshData(
			geometry.Name,
			remapped,
			geometry.Indices,
			geometry.SubMeshes,
			geometry.Bounds,
			geometry.Skeleton);

		return new ModelSkinBuildResult(
			resultGeometry,
			skin,
			jointCount,
			exceedsLimit);
	}

	private static void AddBoneWeight(
		Dictionary<int, float> totals,
		uint packed,
		Vector4 weights)
	{
		Add(packed & 0xFF, weights.X);
		Add((packed >> 8) & 0xFF, weights.Y);
		Add((packed >> 16) & 0xFF, weights.Z);
		Add((packed >> 24) & 0xFF, weights.W);

		void Add(uint index, float weight)
		{
			if (weight <= 0f)
			{
				return;
			}

			var key = (int)index;
			totals.TryGetValue(key, out var total);
			totals[key] = total + weight;
		}
	}

	private static SkinnedVertex RemapVertex(
		SkinnedVertex vertex,
		IReadOnlyDictionary<int, byte> jointMap)
	{
		var packed = vertex.BlendIndices.PackedValue;
		var weights = vertex.BlendWeights;
		var mappedWeights = new float[4];
		var mappedIndices = new byte[4];
		var sum = 0f;

		Map((int)(packed & 0xFF), weights.X, 0);
		Map((int)((packed >> 8) & 0xFF), weights.Y, 1);
		Map((int)((packed >> 16) & 0xFF), weights.Z, 2);
		Map((int)((packed >> 24) & 0xFF), weights.W, 3);

		if (sum <= 1e-6f)
		{
			mappedWeights[0] = 1f;
			mappedIndices[0] = 0;
		}
		else
		{
			for (var index = 0; index < 4; index++)
			{
				mappedWeights[index] /= sum;
			}
		}

		return new SkinnedVertex(
			vertex.Position,
			vertex.Normal,
			vertex.TexCoord,
			new Vector4(
				mappedWeights[0],
				mappedWeights[1],
				mappedWeights[2],
				mappedWeights[3]),
			new Microsoft.Xna.Framework.Graphics.PackedVector.Byte4(
				mappedIndices[0],
				mappedIndices[1],
				mappedIndices[2],
				mappedIndices[3]));

		void Map(int source, float weight, int slot)
		{
			if (weight <= 0f || !jointMap.TryGetValue(source, out var target))
			{
				mappedWeights[slot] = 0f;
				mappedIndices[slot] = 0;
				return;
			}

			mappedWeights[slot] = weight;
			mappedIndices[slot] = target;
			sum += weight;
		}
	}
}

public sealed record ModelSkinBuildResult(
	SkinnedMeshData Geometry,
	DrSkin Skin,
	int JointCount,
	bool ExceedsSkinnedEffectLimit);
