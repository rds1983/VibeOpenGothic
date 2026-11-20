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
	private static readonly Color FogColor = new(92, 103, 112);

	private readonly GraphicsDevice _device;
	private readonly GpuTextureCache _textures;
	private readonly BasicEffect _basic;
	private readonly AlphaTestEffect _alphaTest;
	private readonly WorldMeshGpu _worldMesh;
	private readonly Dictionary<string, DrMesh> _staticMeshes = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<(DrMesh Mesh, Matrix Transform)> _instances = new();
	private Matrix _view = Matrix.Identity;
	private Matrix _projection = Matrix.Identity;
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

		_device = device;
		_textures = textures;
		_basic = new BasicEffect(device)
		{
			PreferPerPixelLighting = false,
			TextureEnabled = false,
			LightingEnabled = true,
			VertexColorEnabled = false,
			FogEnabled = true,
			FogColor = FogColor.ToVector3(),
			FogStart = 15000f,
			FogEnd = 140000f,
		};
		_basic.EnableDefaultLighting();
		_basic.AmbientLightColor = new Vector3(0.62f, 0.64f, 0.68f);

		_alphaTest = new AlphaTestEffect(device)
		{
			AlphaFunction = CompareFunction.Greater,
			ReferenceAlpha = 128,
			FogEnabled = true,
			FogColor = FogColor.ToVector3(),
			FogStart = 15000f,
			FogEnd = 140000f,
		};

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

	public void SetCamera(Matrix view, Matrix projection)
	{
		_view = view;
		_projection = projection;
	}

	public int LastDrawCalls { get; private set; }

	public int LastDrawnPolygons { get; private set; }

	public void Draw(GameTime gameTime)
	{
		ArgumentNullException.ThrowIfNull(gameTime);
		ObjectDisposedException.ThrowIf(_disposed, this);

		_device.DepthStencilState = DepthStencilState.Default;
		_device.BlendState = BlendState.Opaque;
		_device.SamplerStates[0] = SamplerState.LinearWrap;

		_basic.View = _view;
		_basic.Projection = _projection;
		_alphaTest.View = _view;
		_alphaTest.Projection = _projection;

		var drawCalls = 0;
		var polygons = 0;

		_device.SetVertexBuffer(_worldMesh.VertexBuffer);
		_device.Indices = _worldMesh.IndexBuffer;
		foreach (var range in _worldMesh.Ranges)
		{
			ApplyMaterial(_worldMesh.Materials.Get(range.MaterialIndex), null, Matrix.Identity);
			_device.DrawIndexedPrimitives(
				PrimitiveType.TriangleList,
				0,
				range.StartIndex,
				range.PrimitiveCount);
			drawCalls++;
			polygons += range.PrimitiveCount;
		}

		foreach (var (mesh, transform) in _instances)
		{
			foreach (var part in mesh.MeshParts)
			{
				var descriptor = part.Material?.Tag is DrMaterialTag tag
					? tag.Descriptor
					: MaterialDesc.Default;
				ApplyMaterial(descriptor, part.Material, transform);
				_device.SetVertexBuffer(part.VertexBuffer);
				_device.Indices = part.IndexBuffer;
				_device.DrawIndexedPrimitives(
					part.PrimitiveType,
					part.VertexOffset,
					part.StartIndex,
					part.PrimitiveCount);
				drawCalls++;
				polygons += part.PrimitiveCount;
			}
		}

		LastDrawCalls = drawCalls;
		LastDrawnPolygons = polygons;
		_device.RasterizerState = RasterizerState.CullCounterClockwise;
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_basic.Dispose();
		_alphaTest.Dispose();
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

	private void ApplyMaterial(MaterialDesc descriptor, DrMaterial? material, Matrix world)
	{
		_device.RasterizerState = descriptor.IsTwoSided
			? RasterizerState.CullNone
			: RasterizerState.CullCounterClockwise;

		var texture = material?.DiffuseTexture ??
			(descriptor.Texture is null ? null : _textures.Get(descriptor.Texture));
		var color = material?.DiffuseColor.ToVector3() ?? descriptor.Color.ToVector3();

		if (descriptor.AlphaMode == MaterialAlphaMode.Solid)
		{
			_basic.World = world;
			_basic.LightingEnabled = true;
			_basic.TextureEnabled = texture is not null;
			_basic.Texture = texture;
			_basic.DiffuseColor = color;
			_basic.Alpha = descriptor.AlphaWeight;
			_basic.CurrentTechnique.Passes[0].Apply();
		}
		else
		{
			_alphaTest.World = world;
			_alphaTest.Texture = texture ?? _textures.Placeholder;
			_alphaTest.DiffuseColor = color;
			_alphaTest.Alpha = descriptor.AlphaWeight;
			_alphaTest.CurrentTechnique.Passes[0].Apply();
		}
	}
}
