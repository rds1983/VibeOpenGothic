using Microsoft.Xna.Framework;

namespace VibeOpenGothic.World;

/// <summary>
/// One scene VOB whose visual is a skinned MDS model. The key indexes
/// <see cref="WorldAsset.Models"/>.
/// </summary>
public sealed record ModelVobInstance(
	string ModelKey,
	Matrix Transform);
