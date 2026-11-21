using DigitalRiseModel;
using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// Static instances of one <see cref="DrMeshPart"/> inside one spatial cell.
/// The transforms and per-instance bounds are built once and shared until the
/// world is unloaded; hardware instancing will later upload them directly.
/// </summary>
public sealed class InstancedMeshPart
{
	public InstancedMeshPart(
		DrMeshPart part,
		Matrix[] transforms,
		BoundingBox[] instanceBounds,
		BoundingBox bounds)
	{
		ArgumentNullException.ThrowIfNull(part);
		ArgumentNullException.ThrowIfNull(transforms);
		ArgumentNullException.ThrowIfNull(instanceBounds);
		if (transforms.Length != instanceBounds.Length)
		{
			throw new ArgumentException(
				"Transform and instance-bounds counts must match.",
				nameof(instanceBounds));
		}

		Part = part;
		Transforms = transforms;
		InstanceBounds = instanceBounds;
		Bounds = bounds;
	}

	public DrMeshPart Part { get; }

	public Matrix[] Transforms { get; }

	public BoundingBox[] InstanceBounds { get; }

	public BoundingBox Bounds { get; }

	public int Count => Transforms.Length;
}
