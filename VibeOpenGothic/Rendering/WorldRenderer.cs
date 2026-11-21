using System.Diagnostics;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.Core;
using VibeOpenGothic.World;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// Uploads the converted world and its static VOB meshes, then draws them with
/// stock effects. World submeshes are merged by material; static VOBs are
/// partitioned into a deterministic spatial-cell grid with one
/// <see cref="InstancedMeshPart"/> per mesh part and cell. The cell layout is
/// ready for hardware instancing, but this renderer still submits each visible
/// instance through <see cref="ForwardRenderer.DrawMeshPart"/>.
/// </summary>
public sealed class WorldRenderer : IDisposable
{
	private const float CellSize = 2000f;

	private readonly WorldMeshGpu _worldMesh;
	private readonly Dictionary<string, DrMesh> _staticMeshes = new(StringComparer.OrdinalIgnoreCase);
	private readonly SpatialCell[] _cells;
	private bool _disposed;

	public WorldRenderer(
		GraphicsDevice device,
		GpuTextureCache textures,
		WorldAsset world,
		Action<string>? progress = null)
	{
		ArgumentNullException.ThrowIfNull(device);
		ArgumentNullException.ThrowIfNull(textures);
		ArgumentNullException.ThrowIfNull(world);
		progress ??= _ => { };

		progress("Uploading world mesh...");
		var timer = Stopwatch.StartNew();
		_worldMesh = new WorldMeshGpu(
			device,
			world.Mesh,
			textures.Resolve(world.Materials));
		progress(
			$"World mesh uploaded: {_worldMesh.Parts.Count} parts in " +
			$"{timer.Elapsed.TotalMilliseconds:0} ms");

		timer.Restart();
		var partOrder = CreateStaticMeshes(device, textures, world);
		_cells = BuildCells(world, partOrder, out var instanceCount, out var partCount);
		progress(
			$"Static meshes uploaded: {_staticMeshes.Count} meshes, " +
			$"{instanceCount} instances, {_cells.Length} cells, {partCount} parts in " +
			$"{timer.Elapsed.TotalMilliseconds:0} ms");
	}

	public void Draw(ForwardRenderer renderer)
	{
		ArgumentNullException.ThrowIfNull(renderer);
		ObjectDisposedException.ThrowIf(_disposed, this);

		foreach (var part in _worldMesh.Parts)
		{
			renderer.DrawMeshPart(part, Matrix.Identity);
		}

		foreach (var cell in _cells)
		{
			renderer.DrawInstanced(cell);
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_worldMesh.Dispose();

		foreach (var mesh in _staticMeshes.Values)
		{
			mesh.Dispose();
		}

		_staticMeshes.Clear();
	}

	private Dictionary<DrMeshPart, int> CreateStaticMeshes(
		GraphicsDevice device,
		GpuTextureCache textures,
		WorldAsset world)
	{
		var partOrder = new Dictionary<DrMeshPart, int>();
		foreach (var pair in world.StaticMeshes.OrderBy(
			pair => pair.Key,
			StringComparer.OrdinalIgnoreCase))
		{
			var mesh = CreateStaticMesh(
				device,
				pair.Key,
				pair.Value.Mesh,
				textures.Resolve(pair.Value.Materials));
			_staticMeshes[pair.Key] = mesh;

			foreach (var part in mesh.MeshParts)
			{
				partOrder[part] = partOrder.Count;
			}
		}

		return partOrder;
	}

	private SpatialCell[] BuildCells(
		WorldAsset world,
		IReadOnlyDictionary<DrMeshPart, int> partOrder,
		out int instanceCount,
		out int partCount)
	{
		var grid = new SpatialGrid(world.Bounds, CellSize);
		var cells = new Dictionary<int, CellBuilder>();
		instanceCount = 0;
		partCount = 0;

		foreach (var instance in world.StaticVobs)
		{
			if (!_staticMeshes.TryGetValue(instance.MeshKey, out var mesh))
			{
				continue;
			}

			var instanceBounds = mesh.BoundingBox.Transform(instance.Transform);
			var cellIndex = grid.GetCellIndex(instanceBounds);
			if (!cells.TryGetValue(cellIndex, out var cell))
			{
				cell = new CellBuilder();
				cells.Add(cellIndex, cell);
			}

			foreach (var part in mesh.MeshParts)
			{
				cell.Add(part, instance.Transform, instanceBounds);
			}

			instanceCount++;
		}

		var result = new List<SpatialCell>(cells.Count);
		foreach (var pair in cells.OrderBy(pair => pair.Key))
		{
			var builder = pair.Value;
			var parts = new InstancedMeshPart[builder.Parts.Count];
			var index = 0;
			foreach (var partPair in builder.Parts.OrderBy(
				pair => partOrder[pair.Key]))
			{
				var partBuilder = partPair.Value;
				parts[index++] = new InstancedMeshPart(
					partPair.Key,
					partBuilder.Transforms.ToArray(),
					partBuilder.InstanceBounds.ToArray(),
					partBuilder.Bounds);
				partCount++;
			}

			result.Add(new SpatialCell(builder.Bounds, parts));
		}

		return result.ToArray();
	}

	private static DrMesh CreateStaticMesh(
		GraphicsDevice device,
		string name,
		MeshData mesh,
		MaterialSet materials)
	{
		var result = new DrMesh
		{
			Name = name,
		};

		foreach (var subMesh in mesh.SubMeshes)
		{
			if (subMesh.IndexCount <= 0)
			{
				continue;
			}

			var vertexMap = new Dictionary<int, int>();
			var vertices = new List<MeshVertex>();
			var indices = new int[subMesh.IndexCount];
			for (var index = 0; index < indices.Length; index++)
			{
				var sourceIndex = mesh.Indices.Span[subMesh.IndexOffset + index];
				if (!vertexMap.TryGetValue(sourceIndex, out var localIndex))
				{
					localIndex = vertices.Count;
					vertexMap[sourceIndex] = localIndex;
					vertices.Add(mesh.Vertices.Span[sourceIndex]);
				}

				indices[index] = localIndex;
			}

			var vertexBuffer = new VertexBuffer(
				device,
				MeshVertex.Declaration,
				vertices.Count,
				BufferUsage.None);
			vertexBuffer.SetData(vertices.ToArray());

			var indexBuffer = new IndexBuffer(
				device,
				IndexElementSize.ThirtyTwoBits,
				indices.Length,
				BufferUsage.None);
			indexBuffer.SetData(indices);

			var material = materials.Get(subMesh.MaterialIndex);
			var part = new DrMeshPart(
				vertexBuffer,
				indexBuffer,
				subMesh.Bounds,
				PrimitiveType.TriangleList,
				vertices.Count,
				indices.Length / 3)
			{
				Tag = new DrMeshPartTag(material),
			};

			result.MeshParts.Add(part);
		}

		return result;
	}

	private sealed class CellBuilder
	{
		private bool _hasBounds;

		public Dictionary<DrMeshPart, PartBuilder> Parts { get; } = new();

		public BoundingBox Bounds { get; private set; }

		public void Add(
			DrMeshPart part,
			Matrix transform,
			BoundingBox instanceBounds)
		{
			Bounds = _hasBounds
				? BoundingBox.CreateMerged(Bounds, instanceBounds)
				: instanceBounds;
			_hasBounds = true;

			if (!Parts.TryGetValue(part, out var builder))
			{
				builder = new PartBuilder();
				Parts.Add(part, builder);
			}

			builder.Add(transform, instanceBounds);
		}
	}

	private sealed class PartBuilder
	{
		private bool _hasBounds;

		public List<Matrix> Transforms { get; } = new();

		public List<BoundingBox> InstanceBounds { get; } = new();

		public BoundingBox Bounds { get; private set; }

		public void Add(Matrix transform, BoundingBox instanceBounds)
		{
			Transforms.Add(transform);
			InstanceBounds.Add(instanceBounds);
			Bounds = _hasBounds
				? BoundingBox.CreateMerged(Bounds, instanceBounds)
				: instanceBounds;
			_hasBounds = true;
		}
	}

}
