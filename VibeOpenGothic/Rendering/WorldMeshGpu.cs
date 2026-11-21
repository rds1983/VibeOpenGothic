using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Meshes;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// One shared vertex/index buffer for the world mesh. Adjacent submeshes that
/// use the same material are merged into one <see cref="DrMeshPart"/>, so the
/// 13k source groups become one draw per material without duplicating vertices.
/// The part array is the whole drawable world; the class only owns the shared
/// buffers.
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

		Parts = CreateParts(mesh, materials, _vertices, _indices);
	}

	public IReadOnlyList<DrMeshPart> Parts { get; }

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

	private static DrMeshPart[] CreateParts(
		MeshData mesh,
		MaterialSet materials,
		VertexBuffer vertices,
		IndexBuffer indices)
	{
		var runs = MergeByMaterial(mesh.SubMeshes);
		var parts = new DrMeshPart[runs.Count];
		for (var index = 0; index < parts.Length; index++)
		{
			var run = runs[index];
			parts[index] = new DrMeshPart(
				vertices,
				indices,
				run.Bounds,
				PrimitiveType.TriangleList,
				mesh.VertexCount,
				run.PrimitiveCount,
				0,
				run.StartIndex)
			{
				Tag = new DrMeshPartTag(materials.Get(run.MaterialIndex)),
			};
		}

		return parts;
	}

	private static List<MaterialRun> MergeByMaterial(
		IReadOnlyList<MeshSubMesh> subMeshes)
	{
		var runs = new List<MaterialRun>(subMeshes.Count);
		foreach (var subMesh in subMeshes)
		{
			if (subMesh.IndexCount <= 0)
			{
				continue;
			}

			var primitiveCount = subMesh.IndexCount / 3;
			if (runs.Count > 0)
			{
				var previous = runs[^1];
				if (previous.MaterialIndex == subMesh.MaterialIndex &&
					previous.StartIndex + previous.PrimitiveCount * 3 == subMesh.IndexOffset)
				{
					runs[^1] = previous with
					{
						PrimitiveCount = previous.PrimitiveCount + primitiveCount,
						Bounds = BoundingBox.CreateMerged(previous.Bounds, subMesh.Bounds),
					};
					continue;
				}
			}

			runs.Add(new MaterialRun(
				subMesh.IndexOffset,
				primitiveCount,
				subMesh.MaterialIndex,
				subMesh.Bounds));
		}

		return runs;
	}

	private readonly record struct MaterialRun(
		int StartIndex,
		int PrimitiveCount,
		int MaterialIndex,
		BoundingBox Bounds);
}
