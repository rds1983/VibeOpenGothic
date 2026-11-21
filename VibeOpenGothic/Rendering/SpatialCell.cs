using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// One non-empty cell of the static VOB grid: merged world bounds plus the
/// per-mesh-part instance buckets inside it.
/// </summary>
public sealed class SpatialCell
{
	public SpatialCell(BoundingBox bounds, InstancedMeshPart[] parts)
	{
		ArgumentNullException.ThrowIfNull(parts);

		Bounds = bounds;
		Parts = parts;

		var instanceCount = 0;
		foreach (var part in parts)
		{
			instanceCount += part.Count;
		}

		InstanceCount = instanceCount;
	}

	public BoundingBox Bounds { get; }

	public InstancedMeshPart[] Parts { get; }

	public int PartCount => Parts.Length;

	public int InstanceCount { get; }
}
