using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Skins;
using VibeOpenGothic.Core;

namespace VibeOpenGothic.Tests.Unit;

public sealed class SkinBakeTests
{
	[Fact]
	public void Bake_SingleBone_TransformsLocalPosition()
	{
		var bind = new[] { Matrix.CreateTranslation(1, 0, 0) };
		var influences = new[]
		{
			new[] { new SkinInfluence(0, 1f, new Vector3(0, 0, 0)) },
		};

		var result = SkinBake.Bake(influences, bind);

		var vertex = Assert.Single(result.Vertices);
		AssertNear(new Vector3(1, 0, 0), vertex.Position);
		Assert.Equal(new Vector4(1, 0, 0, 0), vertex.Weights);
		Assert.Equal(0f, vertex.BoneIndices.ToVector4().X);
		Assert.Equal(0f, result.Diagnostics.MaxInconsistency);
	}

	[Fact]
	public void Bake_TwoBones_MatchesGothicFormulaWithinTolerance()
	{
		var pCommon = new Vector3(3, 4, 5);
		var bindGothic = new[]
		{
			Matrix.CreateTranslation(1, 0, 0),
			Matrix.CreateTranslation(0, 2, 0),
		};

		var influences = new[]
		{
			new[]
			{
				new SkinInfluence(
					0,
					0.5f,
					CoordinateSystem.ToEnginePoint(
						Vector3.Transform(pCommon, Matrix.Invert(bindGothic[0])))),
				new SkinInfluence(
					1,
					0.5f,
					CoordinateSystem.ToEnginePoint(
						Vector3.Transform(pCommon, Matrix.Invert(bindGothic[1])))),
			},
		};
		var bindEngine = bindGothic.Select(CoordinateSystem.ToEngineMatrix).ToArray();

		var result = SkinBake.Bake(influences, bindEngine);

		AssertNear(CoordinateSystem.ToEnginePoint(pCommon), result.Vertices[0].Position, 1e-5f);
		Assert.InRange(result.Diagnostics.MaxInconsistency, 0f, 1e-5f);
	}

	[Fact]
	public void Bake_RandomConsistentSkeletons_MatchesGothicFormula()
	{
		var random = new Random(12345);

		for (var test = 0; test < 200; test++)
		{
			var boneCount = random.Next(1, 5);
			var pCommon = new Vector3(
				(float)(random.NextDouble() * 20 - 10),
				(float)(random.NextDouble() * 20 - 10),
				(float)(random.NextDouble() * 20 - 10));

			var bindGothic = new Matrix[boneCount];
			for (var bone = 0; bone < boneCount; bone++)
			{
				bindGothic[bone] =
					Matrix.CreateFromYawPitchRoll(
						(float)(random.NextDouble() * 2 - 1),
						(float)(random.NextDouble() * 2 - 1),
						(float)(random.NextDouble() * 2 - 1)) *
					Matrix.CreateTranslation(
						(float)(random.NextDouble() * 20 - 10),
						(float)(random.NextDouble() * 20 - 10),
						(float)(random.NextDouble() * 20 - 10));
			}

			var rawWeights = Enumerable.Range(0, boneCount)
				.Select(_ => (float)(random.NextDouble() + 0.1))
				.ToArray();
			var sum = rawWeights.Sum();

			var influences = new SkinInfluence[boneCount];
			for (var bone = 0; bone < boneCount; bone++)
			{
				var local = Vector3.Transform(pCommon, Matrix.Invert(bindGothic[bone]));
				influences[bone] = new SkinInfluence(
					bone,
					rawWeights[bone] / sum,
					CoordinateSystem.ToEnginePoint(local));
			}

			var bindEngine = bindGothic.Select(CoordinateSystem.ToEngineMatrix).ToArray();
			var result = SkinBake.Bake(new[] { influences }, bindEngine);

			AssertNear(
				CoordinateSystem.ToEnginePoint(pCommon),
				result.Vertices[0].Position,
				1e-3f);
			Assert.InRange(result.Diagnostics.MaxInconsistency, 0f, 1e-3f);
		}
	}

	[Fact]
	public void Bake_InconsistentInfluences_UsesWeightedAverage()
	{
		var bind = new[] { Matrix.Identity, Matrix.Identity };
		var influences = new[]
		{
			new[]
			{
				new SkinInfluence(0, 0.5f, new Vector3(0, 0, 0)),
				new SkinInfluence(1, 0.5f, new Vector3(10, 0, 0)),
			},
		};

		var result = SkinBake.Bake(influences, bind);

		AssertNear(new Vector3(5, 0, 0), result.Vertices[0].Position, 1e-5f);
		Assert.InRange(result.Diagnostics.MaxInconsistency, 5f - 1e-5f, 5f + 1e-5f);
		Assert.InRange(result.Diagnostics.MaxWeightedInconsistency, 5f - 1e-5f, 5f + 1e-5f);
		Assert.Equal(1, result.Diagnostics.InconsistentVertexCount);
	}

	[Fact]
	public void Bake_TinyOutlierWeight_KeepsWeightedInconsistencySmall()
	{
		var bind = new[] { Matrix.Identity, Matrix.Identity };
		var influences = new[]
		{
			new[]
			{
				new SkinInfluence(0, 0.999f, Vector3.Zero),
				new SkinInfluence(1, 0.001f, new Vector3(100, 0, 0)),
			},
		};

		var result = SkinBake.Bake(influences, bind);

		Assert.InRange(result.Diagnostics.MaxInconsistency, 99.9f - 1e-3f, 99.9f + 1e-3f);
		Assert.InRange(result.Diagnostics.MaxWeightedInconsistency, 0.1998f - 1e-4f, 0.1998f + 1e-4f);
	}

	[Fact]
	public void Bake_RenormalizesWeights()
	{
		var bind = new[] { Matrix.Identity, Matrix.Identity };
		var influences = new[]
		{
			new[]
			{
				new SkinInfluence(0, 0.5f, new Vector3(0, 0, 0)),
				new SkinInfluence(1, 0.3f, new Vector3(10, 0, 0)),
			},
		};

		var result = SkinBake.Bake(influences, bind);

		Assert.Equal(1, result.Diagnostics.RenormalizedWeightCount);
		Assert.InRange(result.Vertices[0].Weights.X, 0.625f - 1e-5f, 0.625f + 1e-5f);
		Assert.InRange(result.Vertices[0].Weights.Y, 0.375f - 1e-5f, 0.375f + 1e-5f);
		AssertNear(new Vector3(3.75f, 0, 0), result.Vertices[0].Position, 1e-5f);
	}

	[Fact]
	public void Bake_MergesDuplicateBoneInfluences()
	{
		var bind = new[] { Matrix.Identity, Matrix.Identity };
		var influences = new[]
		{
			new[]
			{
				new SkinInfluence(0, 0.25f, new Vector3(1, 0, 0)),
				new SkinInfluence(0, 0.5f, new Vector3(1, 0, 0)),
				new SkinInfluence(1, 0.25f, new Vector3(5, 0, 0)),
			},
		};

		var result = SkinBake.Bake(influences, bind);

		var vertex = result.Vertices[0];
		Assert.Equal(0f, vertex.BoneIndices.ToVector4().X);
		Assert.Equal(1f, vertex.BoneIndices.ToVector4().Y);
		Assert.InRange(vertex.Weights.X, 0.75f - 1e-5f, 0.75f + 1e-5f);
		AssertNear(new Vector3(2, 0, 0), vertex.Position, 1e-5f);
	}

	[Fact]
	public void Bake_TruncatesInfluencesBeyondFour()
	{
		var bind = Enumerable.Repeat(Matrix.Identity, 5).ToArray();
		var influences = new[]
		{
			Enumerable.Range(0, 5)
				.Select(index => new SkinInfluence(index, 0.2f, new Vector3(index, 0, 0)))
				.ToArray(),
		};

		var result = SkinBake.Bake(influences, bind);

		Assert.Equal(1, result.Diagnostics.TruncatedInfluenceCount);
		Assert.Equal(3f, result.Vertices[0].BoneIndices.ToVector4().W);
		Assert.InRange(result.Vertices[0].Weights.X, 0.25f - 1e-5f, 0.25f + 1e-5f);
	}

	[Fact]
	public void Bake_DropsOutOfRangeBones()
	{
		var bind = new[] { Matrix.Identity };
		var influences = new[]
		{
			new[]
			{
				new SkinInfluence(0, 0.5f, new Vector3(2, 0, 0)),
				new SkinInfluence(9, 0.5f, new Vector3(100, 0, 0)),
			},
		};

		var result = SkinBake.Bake(influences, bind);

		Assert.Equal(1, result.Diagnostics.OutOfRangeBoneCount);
		AssertNear(new Vector3(2, 0, 0), result.Vertices[0].Position, 1e-5f);
		Assert.Equal(new Vector4(1, 0, 0, 0), result.Vertices[0].Weights);
	}

	[Fact]
	public void Bake_ZeroWeights_UsesFirstInfluence()
	{
		var bind = new[] { Matrix.Identity };
		var influences = new[]
		{
			new[] { new SkinInfluence(0, 0f, new Vector3(2, 3, 4)) },
		};

		var result = SkinBake.Bake(influences, bind);

		Assert.Equal(1, result.Diagnostics.ZeroWeightCount);
		AssertNear(new Vector3(2, 3, 4), result.Vertices[0].Position, 1e-5f);
		Assert.Equal(new Vector4(1, 0, 0, 0), result.Vertices[0].Weights);
	}

	[Fact]
	public void Bake_NoInfluences_UsesBoneZero()
	{
		var bind = new[] { Matrix.Identity };
		var influences = new[] { Array.Empty<SkinInfluence>() };

		var result = SkinBake.Bake(influences, bind);

		Assert.Equal(1, result.Diagnostics.ZeroWeightCount);
		AssertNear(Vector3.Zero, result.Vertices[0].Position, 1e-5f);
		Assert.Equal(new Vector4(1, 0, 0, 0), result.Vertices[0].Weights);
	}

	[Fact]
	public void Bake_PadsWeightsAndIndicesToFour()
	{
		var bind = new[] { Matrix.Identity };
		var influences = new[]
		{
			new[] { new SkinInfluence(0, 1f, new Vector3(1, 2, 3)) },
		};

		var result = SkinBake.Bake(influences, bind);

		Assert.Equal(new Vector4(1, 0, 0, 0), result.Vertices[0].Weights);
		Assert.Equal(0f, result.Vertices[0].BoneIndices.ToVector4().Y);
		Assert.Equal(0f, result.Vertices[0].BoneIndices.ToVector4().Z);
		Assert.Equal(0f, result.Vertices[0].BoneIndices.ToVector4().W);
	}

	[Fact]
	public void Bake_EmptyInput_ReturnsEmptyResult()
	{
		var result = SkinBake.Bake(Array.Empty<IReadOnlyList<SkinInfluence>>(), new[] { Matrix.Identity });

		Assert.Empty(result.Vertices);
		Assert.Equal(0, result.Diagnostics.VertexCount);
	}

	[Fact]
	public void Bake_RejectsNullInputs()
	{
		Assert.Throws<ArgumentNullException>(
			() => SkinBake.Bake(null!, new[] { Matrix.Identity }));
		Assert.Throws<ArgumentNullException>(
			() => SkinBake.Bake(Array.Empty<IReadOnlyList<SkinInfluence>>(), null!));
	}

	private static void AssertNear(Vector3 expected, Vector3 actual, float epsilon = 1e-5f)
	{
		Assert.InRange(Vector3.Distance(expected, actual), 0f, epsilon);
	}
}
