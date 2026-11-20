using System.Diagnostics;
using DigitalRiseModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.World;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// Uploads the converted world and its static VOB meshes, then draws them with
/// stock effects. World submeshes are merged by material; static VOBs use
/// DigitalRiseModel <see cref="DrMesh"/> containers shared between instances.
/// </summary>
public sealed class WorldRenderer : IDisposable
{
	private readonly WorldMeshGpu _worldMesh;
	private readonly Dictionary<string, DrMesh> _staticMeshes = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<(DrMesh Mesh, Matrix Transform)> _instances = new();
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
		_worldMesh = new WorldMeshGpu(device, world.Mesh, world.Materials);
		progress(
			$"World mesh uploaded: {_worldMesh.DrawRangeCount} draw ranges in " +
			$"{timer.Elapsed.TotalMilliseconds:0} ms");

		timer.Restart();
		foreach (var pair in world.StaticMeshes)
		{
			_staticMeshes[pair.Key] = CreateStaticMesh(
				device,
				pair.Key,
				pair.Value.Mesh,
				pair.Value.Materials,
				textures);
		}

		foreach (var instance in world.StaticVobs)
		{
			if (_staticMeshes.TryGetValue(instance.MeshKey, out var mesh))
			{
				_instances.Add((mesh, instance.Transform));
			}
		}

		progress(
			$"Static meshes uploaded: {_staticMeshes.Count} meshes, " +
			$"{_instances.Count} instances in {timer.Elapsed.TotalMilliseconds:0} ms");
	}

	public void Draw(ForwardRenderer renderer)
	{
		ArgumentNullException.ThrowIfNull(renderer);
		ObjectDisposedException.ThrowIf(_disposed, this);

		foreach (var range in _worldMesh.Ranges)
		{
			renderer.DrawRange(
				_worldMesh.VertexBuffer,
				_worldMesh.IndexBuffer,
				range.StartIndex,
				range.PrimitiveCount,
				_worldMesh.Materials.Get(range.MaterialIndex),
				Matrix.Identity);
		}

		foreach (var (mesh, transform) in _instances)
		{
			foreach (var part in mesh.MeshParts)
			{
				renderer.DrawMeshPart(part, transform);
			}
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
		_instances.Clear();
	}

	private static DrMesh CreateStaticMesh(
		GraphicsDevice device,
		string name,
		MeshData mesh,
		MaterialSet materials,
		GpuTextureCache textures)
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

			var descriptor = materials.Get(subMesh.MaterialIndex);
			var material = new DrMaterial
			{
				Name = descriptor.Name,
				DiffuseColor = descriptor.Color,
				DiffuseTexture = textures.Get(descriptor.Texture),
				Tag = new DrMaterialTag(descriptor),
			};

			var part = new DrMeshPart(
				vertexBuffer,
				indexBuffer,
				subMesh.Bounds,
				PrimitiveType.TriangleList,
				vertices.Count,
				indices.Length / 3)
			{
				Material = material,
			};

			result.MeshParts.Add(part);
		}

		return result;
	}

}
