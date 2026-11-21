using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// Deterministic 2D XZ cell grid over a world-space bounding box. Static VOBs
/// are assigned by the centre of their transformed bounds; indices are clamped
/// so instances slightly outside the converted mesh bounds stay in the edge
/// cells.
/// </summary>
public sealed class SpatialGrid
{
	private readonly Vector3 _origin;
	private readonly float _cellSize;

	public SpatialGrid(BoundingBox bounds, float cellSize)
	{
		if (!(cellSize > 0f))
		{
			throw new ArgumentOutOfRangeException(
				nameof(cellSize),
				cellSize,
				"Cell size must be positive.");
		}

		_origin = bounds.Min;
		_cellSize = cellSize;
		Columns = Math.Max(
			1,
			(int)MathF.Ceiling((bounds.Max.X - bounds.Min.X) / cellSize));
		Rows = Math.Max(
			1,
			(int)MathF.Ceiling((bounds.Max.Z - bounds.Min.Z) / cellSize));
	}

	public int Columns { get; }

	public int Rows { get; }

	public int CellCount => Columns * Rows;

	public int GetCellIndex(Vector3 position)
	{
		var column = (int)MathF.Floor((position.X - _origin.X) / _cellSize);
		var row = (int)MathF.Floor((position.Z - _origin.Z) / _cellSize);
		column = Math.Clamp(column, 0, Columns - 1);
		row = Math.Clamp(row, 0, Rows - 1);
		return row * Columns + column;
	}

	public int GetCellIndex(BoundingBox instanceBounds)
	{
		return GetCellIndex((instanceBounds.Min + instanceBounds.Max) * 0.5f);
	}
}
