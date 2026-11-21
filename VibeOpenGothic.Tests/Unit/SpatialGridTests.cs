using Microsoft.Xna.Framework;
using VibeOpenGothic.Rendering;

namespace VibeOpenGothic.Tests.Unit;

public sealed class SpatialGridTests
{
	private static readonly BoundingBox World = new(
		new Vector3(0f, 0f, 0f),
		new Vector3(100f, 50f, 100f));

	[Fact]
	public void GetCellIndex_UsesRowMajorXzCells()
	{
		var grid = new SpatialGrid(World, 10f);

		Assert.Equal(10, grid.Columns);
		Assert.Equal(10, grid.Rows);
		Assert.Equal(100, grid.CellCount);
		Assert.Equal(0, grid.GetCellIndex(new Vector3(5f, 0f, 5f)));
		Assert.Equal(2, grid.GetCellIndex(new Vector3(25f, 0f, 5f)));
		Assert.Equal(20, grid.GetCellIndex(new Vector3(5f, 0f, 25f)));
		Assert.Equal(99, grid.GetCellIndex(new Vector3(95f, 0f, 95f)));
	}

	[Fact]
	public void GetCellIndex_IgnoresHeight()
	{
		var grid = new SpatialGrid(World, 10f);

		Assert.Equal(
			grid.GetCellIndex(new Vector3(25f, 0f, 25f)),
			grid.GetCellIndex(new Vector3(25f, 9999f, 25f)));
	}

	[Fact]
	public void GetCellIndex_ClampsOutsideWorld()
	{
		var grid = new SpatialGrid(World, 10f);

		Assert.Equal(0, grid.GetCellIndex(new Vector3(-100f, 0f, -100f)));
		Assert.Equal(99, grid.GetCellIndex(new Vector3(1000f, 0f, 1000f)));
	}

	[Fact]
	public void GetCellIndex_UsesBoundsCentre()
	{
		var grid = new SpatialGrid(World, 10f);
		var bounds = new BoundingBox(
			new Vector3(20f, 0f, 20f),
			new Vector3(30f, 10f, 30f));

		Assert.Equal(22, grid.GetCellIndex(bounds));
	}

	[Fact]
	public void ZeroSizedWorld_HasOneCell()
	{
		var grid = new SpatialGrid(
			new BoundingBox(Vector3.Zero, Vector3.Zero),
			10f);

		Assert.Equal(1, grid.Columns);
		Assert.Equal(1, grid.Rows);
		Assert.Equal(0, grid.GetCellIndex(new Vector3(500f, 0f, -500f)));
	}

	[Theory]
	[InlineData(0f)]
	[InlineData(-1f)]
	public void NonPositiveCellSize_Throws(float cellSize)
	{
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new SpatialGrid(World, cellSize));
	}
}
