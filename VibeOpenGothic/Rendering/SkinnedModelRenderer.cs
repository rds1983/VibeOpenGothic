using DigitalRiseModel;
using DigitalRiseModel.Animation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.Assets.Models;
using VibeOpenGothic.Assets.Skins;
using VibeOpenGothic.World;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// Uploads and draws skinned MDS VOBs with stock <see cref="SkinnedEffect"/>.
/// The runtime containers are DigitalRiseModel types: <see cref="DrModel"/>,
/// <see cref="DrModelInstance"/>, <see cref="DrMesh"/> and <see cref="DrMeshPart"/>.
/// Per-model, per-part and per-instance state lives in their <c>Tag</c> objects.
/// </summary>
public sealed class SkinnedModelRenderer : IDisposable
{
	private static readonly Color FogColor = new(92, 103, 112);

	private readonly GpuTextureCache _textures;
	private readonly SkinnedEffect _effect;
	private readonly BasicEffect _basic;
	private readonly AlphaTestEffect _alphaTest;
	private readonly Dictionary<string, DrModel> _models = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<DrModelInstance> _instances = new();
	private Matrix _view = Matrix.Identity;
	private Matrix _projection = Matrix.Identity;
	private bool _disposed;

	public SkinnedModelRenderer(
		GraphicsDevice device,
		GpuTextureCache textures,
		WorldAsset world,
		Action<string>? progress = null)
	{
		ArgumentNullException.ThrowIfNull(device);
		ArgumentNullException.ThrowIfNull(textures);
		ArgumentNullException.ThrowIfNull(world);
		progress ??= _ => { };

		_textures = textures;
		_effect = new SkinnedEffect(device)
		{
			WeightsPerVertex = 4,
			PreferPerPixelLighting = false,
			FogEnabled = true,
			FogColor = FogColor.ToVector3(),
			FogStart = 15000f,
			FogEnd = 140000f,
		};
		_effect.EnableDefaultLighting();
		_effect.AmbientLightColor = new Vector3(0.62f, 0.64f, 0.68f);

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

		foreach (var asset in world.Models.Values)
		{
			var model = CreateModel(device, textures, asset, progress);
			if (model is not null)
			{
				_models[asset.Key] = model;
			}
		}

		foreach (var vob in world.ModelVobs)
		{
			if (_models.TryGetValue(vob.ModelKey, out var model))
			{
				CreateInstance(model, vob.Transform);
			}
		}

		progress(
			$"Skinned models uploaded: {_models.Count} models, " +
			$"{_instances.Count} instances");
	}

	public int LastDrawCalls { get; private set; }

	public int LastDrawnPolygons { get; private set; }

	public void SetCamera(Matrix view, Matrix projection)
	{
		_view = view;
		_projection = projection;
	}

	public void Draw(GameTime gameTime)
	{
		ArgumentNullException.ThrowIfNull(gameTime);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var device = _effect.GraphicsDevice;
		device.DepthStencilState = DepthStencilState.Default;
		device.BlendState = BlendState.Opaque;
		device.SamplerStates[0] = SamplerState.LinearWrap;

		var drawCalls = 0;
		var polygons = 0;
		var elapsed = gameTime.ElapsedGameTime;

		foreach (var instance in _instances)
		{
			if (instance.Tag is not DrModelInstanceTag state ||
				instance.Model.Tag is not DrModelTag)
			{
				continue;
			}

			state.Controller?.Update(elapsed);

			var mesh = instance.Model.Root.Mesh;
			if (mesh is null)
			{
				continue;
			}

			foreach (var part in mesh.MeshParts)
			{
				if (part.Skin is not null)
				{
					var matrices = instance.GetSkinTransforms(part.Skin.SkinIndex);
					var descriptor = GetDescriptor(part);
					var texture = part.Material?.DiffuseTexture;

					_effect.World = state.Transform;
					_effect.View = _view;
					_effect.Projection = _projection;
					_effect.Texture = texture ?? _textures.White;
					_effect.DiffuseColor = descriptor.Color.ToVector3();
					_effect.SetBoneTransforms(matrices);

					device.RasterizerState = descriptor.IsTwoSided
						? RasterizerState.CullNone
						: RasterizerState.CullCounterClockwise;
					device.BlendState = descriptor.AlphaMode == MaterialAlphaMode.Solid
						? BlendState.Opaque
						: BlendState.AlphaBlend;

					_effect.CurrentTechnique.Passes[0].Apply();
					device.SetVertexBuffer(part.VertexBuffer);
					device.Indices = part.IndexBuffer;
					device.DrawIndexedPrimitives(
						part.PrimitiveType,
						part.VertexOffset,
						part.StartIndex,
						part.PrimitiveCount);

					drawCalls++;
					polygons += part.PrimitiveCount;
				}
				else if (part.Tag is DrMeshPartTag attachmentTag)
				{
					var nodeTransform = instance.GetBoneGlobalTransform(
						attachmentTag.BoneIndex);
					var world = nodeTransform * state.Transform;

					ApplyAttachmentMaterial(part, world);
					device.SetVertexBuffer(part.VertexBuffer);
					device.Indices = part.IndexBuffer;
					device.DrawIndexedPrimitives(
						part.PrimitiveType,
						part.VertexOffset,
						part.StartIndex,
						part.PrimitiveCount);

					drawCalls++;
					polygons += part.PrimitiveCount;
				}
			}
		}

		LastDrawCalls = drawCalls;
		LastDrawnPolygons = polygons;
		device.RasterizerState = RasterizerState.CullCounterClockwise;
		device.BlendState = BlendState.Opaque;
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_effect.Dispose();
		_basic.Dispose();
		_alphaTest.Dispose();

		foreach (var model in _models.Values)
		{
			if (model.Tag is not DrModelTag)
			{
				continue;
			}

			model.Root.Mesh?.Dispose();
			model.Tag = null;
		}

		_models.Clear();
		_instances.Clear();
	}

	private static DrModel? CreateModel(
		GraphicsDevice device,
		GpuTextureCache textures,
		GothicModelAsset asset,
		Action<string> progress)
	{
		if (asset.Meshes.Count == 0 && asset.Attachments.Count == 0)
		{
			progress($"Model '{asset.Key}' has no meshes and will not be drawn.");
			return null;
		}

		var mesh = asset.Model.Meshes.Length > 0
			? asset.Model.Meshes[0]
			: new DrMesh { Name = asset.Key };

		foreach (var sourceMesh in asset.Meshes)
		{
			var geometry = sourceMesh.Geometry;
			var vertices = geometry.Vertices.ToArray();
			var indices = geometry.Indices.ToArray();

			var vertexBuffer = new VertexBuffer(
				device,
				SkinnedVertex.Declaration,
				vertices.Length,
				BufferUsage.None);
			vertexBuffer.SetData(vertices);

			var indexBuffer = new IndexBuffer(
				device,
				IndexElementSize.ThirtyTwoBits,
				indices.Length,
				BufferUsage.None);
			indexBuffer.SetData(indices);

			foreach (var subMesh in geometry.SubMeshes)
			{
				var descriptor = sourceMesh.Materials.Get(subMesh.MaterialIndex);
				var texture = descriptor.Texture is null
					? null
					: textures.Get(descriptor.Texture);
				var material = new DrMaterial
				{
					Name = descriptor.Name,
					DiffuseColor = descriptor.Color,
					DiffuseTexture = texture,
					Tag = new DrMaterialTag(descriptor),
				};
				var part = new DrMeshPart(
					vertexBuffer,
					indexBuffer,
					subMesh.Bounds,
					PrimitiveType.TriangleList,
					vertices.Length,
					subMesh.IndexCount / 3,
					0,
					subMesh.IndexOffset)
				{
					Material = material,
					Skin = sourceMesh.Skin,
				};

				mesh.MeshParts.Add(part);
			}
		}

		foreach (var source in asset.Attachments)
		{
			var vertices = source.Mesh.Vertices.ToArray();
			var indices = source.Mesh.Indices.ToArray();

			var vertexBuffer = new VertexBuffer(
				device,
				MeshVertex.Declaration,
				vertices.Length,
				BufferUsage.None);
			vertexBuffer.SetData(vertices);

			var indexBuffer = new IndexBuffer(
				device,
				IndexElementSize.ThirtyTwoBits,
				indices.Length,
				BufferUsage.None);
			indexBuffer.SetData(indices);

			var bone = asset.Model.FindBoneByName(source.NodeName) ?? asset.Model.Root;
			foreach (var subMesh in source.Mesh.SubMeshes)
			{
				var descriptor = source.Materials.Get(subMesh.MaterialIndex);
				var texture = descriptor.Texture is null
					? null
					: textures.Get(descriptor.Texture);
				var material = new DrMaterial
				{
					Name = descriptor.Name,
					DiffuseColor = descriptor.Color,
					DiffuseTexture = texture,
					Tag = new DrMaterialTag(descriptor),
				};
				var part = new DrMeshPart(
					vertexBuffer,
					indexBuffer,
					subMesh.Bounds,
					PrimitiveType.TriangleList,
					vertices.Length,
					subMesh.IndexCount / 3,
					0,
					subMesh.IndexOffset)
				{
					Material = material,
					Tag = new DrMeshPartTag(bone.Index),
				};

				mesh.MeshParts.Add(part);
			}
		}

		asset.Model.Tag = new DrModelTag(asset);
		return asset.Model;
	}

	private void CreateInstance(DrModel model, Matrix transform)
	{
		if (model.Tag is not DrModelTag info)
		{
			return;
		}

		var instance = new DrModelInstance(model);
		AnimationController? controller = null;
		if (info.Asset.IdleClip is not null)
		{
			controller = new AnimationController(instance);
			controller.StartClip(info.Asset.IdleClip, info.Asset.IdleClip.PlaybackFlags);
			controller.Update(TimeSpan.Zero);
		}

		instance.Tag = new DrModelInstanceTag(transform, controller);
		_instances.Add(instance);
	}

	private void ApplyAttachmentMaterial(DrMeshPart part, Matrix world)
	{
		var descriptor = GetDescriptor(part);
		var texture = part.Material?.DiffuseTexture;
		var device = part.VertexBuffer.GraphicsDevice;
		device.RasterizerState = descriptor.IsTwoSided
			? RasterizerState.CullNone
			: RasterizerState.CullCounterClockwise;
		device.BlendState = BlendState.Opaque;

		if (descriptor.AlphaMode == MaterialAlphaMode.Solid)
		{
			_basic.World = world;
			_basic.View = _view;
			_basic.Projection = _projection;
			_basic.LightingEnabled = true;
			_basic.TextureEnabled = texture is not null;
			_basic.Texture = texture;
			_basic.DiffuseColor = descriptor.Color.ToVector3();
			_basic.Alpha = descriptor.AlphaWeight;
			_basic.CurrentTechnique.Passes[0].Apply();
		}
		else
		{
			_alphaTest.World = world;
			_alphaTest.View = _view;
			_alphaTest.Projection = _projection;
			_alphaTest.Texture = texture ?? _textures.Placeholder;
			_alphaTest.DiffuseColor = descriptor.Color.ToVector3();
			_alphaTest.Alpha = descriptor.AlphaWeight;
			_alphaTest.CurrentTechnique.Passes[0].Apply();
		}
	}

	private static MaterialDesc GetDescriptor(DrMeshPart part)
	{
		return part.Material?.Tag is DrMaterialTag tag
			? tag.Descriptor
			: MaterialDesc.Default;
	}
}
