using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Meshes;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// One shared vertex/index buffer for the world mesh. Draw ranges merge adjacent
/// submeshes that use the same material, so the 13k source groups become one
/// draw per material without duplicating vertices.
/// </summary>
public sealed class WorldMeshGpu : IDisposable
{
	private readonly VertexBuffer _vertices;
	private readonly IndexBuffer _indices;
	private bool _disposed;

	public WorldMeshGpu(GraphicsDevice device, MeshData mesh, MaterialSet materials)
	{
		ArgumentNullException.ThrowIfNull(device);
		ArgumentNullException.ThrowIfNull(mesh);
		ArgumentNullException.ThrowIfNull(materials);

		var vertices = mesh.Vertices.ToArray();
		_vertices = new VertexBuffer(
			device,
			MeshVertex.Declaration,
			vertices.Length,
			BufferUsage.None);
		_vertices.SetData(vertices);

		var indices = mesh.Indices.ToArray();
		_indices = new IndexBuffer(
			device,
			IndexElementSize.ThirtyTwoBits,
			indices.Length,
			BufferUsage.None);
		_indices.SetData(indices);

		Ranges = MergeByMaterial(mesh.SubMeshes);
		Materials = materials;
		VertexCount = mesh.VertexCount;
		IndexCount = mesh.IndexCount;
		Bounds = mesh.Bounds;
	}

	public MaterialSet Materials { get; }

	public IReadOnlyList<DrawRange> Ranges { get; }

	public BoundingBox Bounds { get; }

	public int VertexCount { get; }

	public int IndexCount { get; }

	public int DrawRangeCount => Ranges.Count;

	public VertexBuffer VertexBuffer
	{
		get
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			return _vertices;
		}
	}

	public IndexBuffer IndexBuffer
	{
		get
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			return _indices;
		}
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_vertices.Dispose();
		_indices.Dispose();
	}

	private static DrawRange[] MergeByMaterial(IReadOnlyList<MeshSubMesh> subMeshes)
	{
		var ranges = new List<DrawRange>(subMeshes.Count);
		foreach (var subMesh in subMeshes)
		{
			if (subMesh.IndexCount <= 0)
			{
				continue;
			}

			var primitiveCount = subMesh.IndexCount / 3;
			if (ranges.Count > 0)
			{
				var previous = ranges[^1];
				if (previous.MaterialIndex == subMesh.MaterialIndex &&
					previous.StartIndex + previous.PrimitiveCount * 3 == subMesh.IndexOffset)
				{
					ranges[^1] = previous with
					{
						PrimitiveCount = previous.PrimitiveCount + primitiveCount,
						Bounds = BoundingBox.CreateMerged(previous.Bounds, subMesh.Bounds),
					};
					continue;
				}
			}

			ranges.Add(new DrawRange(
				subMesh.IndexOffset,
				primitiveCount,
				subMesh.MaterialIndex,
				subMesh.Bounds));
		}

		return ranges.ToArray();
	}

	public readonly record struct DrawRange(
		int StartIndex,
		int PrimitiveCount,
		int MaterialIndex,
		BoundingBox Bounds);
}
