using DigitalRiseModel;
using DigitalRiseModel.Primitives;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Textures;
using VibeOpenGothic.Core.Logging;
using VibeOpenGothic.Data.Vfs;

namespace VibeOpenGothic.Rendering;

/// <summary>
/// R0 test scene: grid, textured cube, a readable map/sign, an alpha-tested
/// two-sided sign and an unlit normal-colour debug cube. Geometry comes from
/// DigitalRiseModel primitives; <see cref="DrMaterial"/> carries texture and
/// colour, while <see cref="DrMaterial.Tag"/> keeps the richer
/// <see cref="MaterialDesc"/> that DigitalRiseModel does not model (alpha mode,
/// two-sided, vertex-colour debug).
/// </summary>
public sealed class TestScene : IDisposable
{
	private static readonly Color GroundColor = new(28, 32, 34);
	private static readonly Color GridColor = new(62, 68, 74);

	private readonly BasicEffect _basic;
	private readonly AlphaTestEffect _alphaTest;
	private readonly GpuTextureCache _textures;
	private readonly DrMaterial _mapMaterial;
	private readonly DrMaterial _signMaterial;
	private readonly DrMesh _groundMesh;
	private readonly DrMesh _cubeMesh;
	private readonly DrMesh _quadMesh;
	private readonly DrMesh _gridMesh;
	private readonly DrMesh _normalMesh;
	private readonly DrMeshPart _ground;
	private readonly DrMeshPart _cube;
	private readonly DrMeshPart _quad;
	private readonly DrMeshPart _grid;
	private readonly DrMeshPart _normalCube;
	private bool _disposed;

	public TestScene(GraphicsDevice device, IGameVfs vfs)
	{
		ArgumentNullException.ThrowIfNull(device);
		ArgumentNullException.ThrowIfNull(vfs);

		var textureCache = new TextureCache(
			new ZenKitTextureLoader(vfs),
			name => Log.Warning("Render", $"Test scene texture missing: '{name}'."));
		_textures = new GpuTextureCache(device);

		_basic = new BasicEffect(device)
		{
			PreferPerPixelLighting = false,
			TextureEnabled = false,
			LightingEnabled = false,
			VertexColorEnabled = false,
		};
		_basic.EnableDefaultLighting();
		_basic.AmbientLightColor = new Vector3(0.35f, 0.35f, 0.38f);

		_alphaTest = new AlphaTestEffect(device)
		{
			AlphaFunction = CompareFunction.Greater,
			ReferenceAlpha = 128,
		};

		_groundMesh = MeshPrimitives.CreatePlaneMesh(
			device,
			1200f,
			1200f,
			tessellationX: 1,
			tessellationY: 1,
			uScale: 1f,
			vScale: 1f,
			generateBackFace: false,
			toLeftHanded: false,
			NormalDirection.UpY);
		_ground = _groundMesh.MeshParts[0];
		_ground.Tag = PartKind.Unlit;
		_ground.Material = new DrMaterial
		{
			DiffuseColor = GroundColor,
			Tag = MaterialDesc.Default,
		};

		_cubeMesh = MeshPrimitives.CreateBoxMesh(device, new Vector3(90f));
		_cube = _cubeMesh.MeshParts[0];
		_cube.Tag = PartKind.LitTextured;
		_cube.Material = LoadMaterial(
			textureCache,
			"test-cube",
			"OBJ_CITY_SIGNSALL_01",
			MaterialAlphaFunction.Default);

		_mapMaterial = LoadMaterial(
			textureCache,
			"test-map",
			"MAP_NEWWORLD",
			MaterialAlphaFunction.Default);
		_signMaterial = LoadMaterial(
			textureCache,
			"test-sign",
			"NW_SEQ_EGG_SIGN_01",
			MaterialAlphaFunction.Default);

		_quadMesh = MeshPrimitives.CreatePlaneMesh(
			device,
			1f,
			1f,
			tessellationX: 1,
			tessellationY: 1,
			uScale: 1f,
			vScale: 1f,
			generateBackFace: false,
			toLeftHanded: false,
			NormalDirection.UpZ);
		_quad = _quadMesh.MeshParts[0];
		_quad.Tag = PartKind.AlphaTextured;
		_quad.Material = _mapMaterial;

		_gridMesh = new DrMesh(MeshPrimitives.CreatePlaneLinesMeshPart(device, 10));
		_grid = _gridMesh.MeshParts[0];
		_grid.Tag = PartKind.Unlit;
		_grid.Material = new DrMaterial
		{
			DiffuseColor = GridColor,
			Tag = MaterialDesc.Default,
		};

		var normalPart = CreateNormalCubePart(device);
		_normalMesh = new DrMesh(normalPart);
		_normalCube = normalPart;
		_normalCube.Tag = PartKind.VertexColor;
		_normalCube.Material = new DrMaterial
		{
			DiffuseColor = Color.White,
			Tag = MaterialDesc.Default,
		};
	}

	public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
	{
		ArgumentNullException.ThrowIfNull(device);
		ObjectDisposedException.ThrowIf(_disposed, this);

		device.DepthStencilState = DepthStencilState.Default;
		device.BlendState = BlendState.Opaque;
		device.SamplerStates[0] = SamplerState.LinearClamp;
		device.RasterizerState = RasterizerState.CullCounterClockwise;

		_basic.View = view;
		_basic.Projection = projection;
		_alphaTest.View = view;
		_alphaTest.Projection = projection;

		DrawPart(
			device,
			_ground,
			Matrix.CreateTranslation(0f, -1f, 0f));
		DrawPart(
			device,
			_grid,
			Matrix.CreateScale(50f));
		DrawPart(
			device,
			_cube,
			Matrix.CreateScale(1f) *
			Matrix.CreateRotationY(-0.35f) *
			Matrix.CreateTranslation(0f, 66f, 0f));
		_quad.Material = _mapMaterial;
		DrawPart(
			device,
			_quad,
			Matrix.CreateScale(360f, 180f, 1f) *
			Matrix.CreateRotationY(0.10f) *
			Matrix.CreateTranslation(-230f, 160f, -210f));
		_quad.Material = _signMaterial;
		DrawPart(
			device,
			_quad,
			Matrix.CreateScale(260f, 130f, 1f) *
			Matrix.CreateRotationY(-0.45f) *
			Matrix.CreateTranslation(190f, 105f, 60f));
		DrawPart(
			device,
			_quad,
			Matrix.CreateScale(260f, 130f, 1f) *
			Matrix.CreateRotationY(MathHelper.Pi) *
			Matrix.CreateTranslation(190f, 250f, -180f));
		DrawPart(
			device,
			_normalCube,
			Matrix.CreateScale(70f) *
			Matrix.CreateRotationY(0.55f) *
			Matrix.CreateTranslation(-200f, 45f, 120f));

		device.RasterizerState = RasterizerState.CullCounterClockwise;
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
		_textures.Dispose();
		_groundMesh.Dispose();
		_cubeMesh.Dispose();
		_quadMesh.Dispose();
		_gridMesh.Dispose();
		_normalMesh.Dispose();
	}

	private void DrawPart(GraphicsDevice device, DrMeshPart part, Matrix world)
	{
		var material = part.Material;
		var desc = material?.Tag as MaterialDesc ?? MaterialDesc.Default;
		var kind = part.Tag is PartKind value ? value : PartKind.LitTextured;

		device.RasterizerState = RasterizerState.CullCounterClockwise;

		switch (kind)
		{
			case PartKind.Unlit:
				_basic.World = world;
				_basic.LightingEnabled = false;
				_basic.TextureEnabled = false;
				_basic.VertexColorEnabled = false;
				_basic.DiffuseColor = material?.DiffuseColor.ToVector3() ?? Vector3.One;
				_basic.Alpha = desc.AlphaWeight;
				_basic.CurrentTechnique.Passes[0].Apply();
				break;
			case PartKind.VertexColor:
				_basic.World = world;
				_basic.LightingEnabled = false;
				_basic.TextureEnabled = false;
				_basic.VertexColorEnabled = true;
				_basic.DiffuseColor = Vector3.One;
				_basic.Alpha = 1f;
				_basic.CurrentTechnique.Passes[0].Apply();
				break;
			case PartKind.AlphaTextured:
				device.RasterizerState = desc.IsTwoSided
					? RasterizerState.CullNone
					: RasterizerState.CullCounterClockwise;
				_alphaTest.World = world;
				_alphaTest.Texture = material?.DiffuseTexture;
				_alphaTest.DiffuseColor = material?.DiffuseColor.ToVector3() ?? Vector3.One;
				_alphaTest.Alpha = desc.AlphaWeight;
				_alphaTest.CurrentTechnique.Passes[0].Apply();
				break;
			default:
				_basic.World = world;
				_basic.LightingEnabled = true;
				_basic.TextureEnabled = true;
				_basic.VertexColorEnabled = false;
				_basic.Texture = material?.DiffuseTexture;
				_basic.DiffuseColor = material?.DiffuseColor.ToVector3() ?? Vector3.One;
				_basic.Alpha = desc.AlphaWeight;
				_basic.CurrentTechnique.Passes[0].Apply();
				break;
		}

		device.SetVertexBuffer(part.VertexBuffer);
		device.Indices = part.IndexBuffer;
		device.DrawIndexedPrimitives(
			part.PrimitiveType,
			part.VertexOffset,
			part.StartIndex,
			part.PrimitiveCount);
	}

	private DrMaterial LoadMaterial(
		TextureCache textureCache,
		string name,
		string textureName,
		MaterialAlphaFunction alphaFunction)
	{
		var source = new MaterialSource(
			name,
			MaterialGroup.Stone,
			Color.White,
			0f,
			textureName,
			Vector2.One,
			0f,
			TextureAnimationMapping.None,
			Vector2.Zero,
			false,
			false,
			false,
			string.Empty,
			0f,
			false,
			false,
			0f,
			WaveMode.None,
			WaveSpeed.None,
			0f,
			0f,
			false,
			alphaFunction,
			Vector2.One);

		var desc = MaterialBuilder.FromSource(source, textureCache);
		return new DrMaterial
		{
			Name = name,
			DiffuseColor = desc.Color,
			DiffuseTexture = _textures.Get(desc.Texture),
			Tag = desc,
		};
	}

	private static DrMeshPart CreateNormalCubePart(GraphicsDevice device)
	{
		var vertices = new List<VertexPositionColor>(24);
		var indices = new List<int>(36);

		AddFace(new Vector3(-0.5f, -0.5f, 0.5f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
		AddFace(new Vector3(0.5f, -0.5f, -0.5f), -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitZ);
		AddFace(new Vector3(0.5f, -0.5f, 0.5f), -Vector3.UnitZ, Vector3.UnitY, Vector3.UnitX);
		AddFace(new Vector3(-0.5f, -0.5f, -0.5f), Vector3.UnitZ, Vector3.UnitY, -Vector3.UnitX);
		AddFace(new Vector3(-0.5f, 0.5f, -0.5f), Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY);
		AddFace(new Vector3(-0.5f, -0.5f, -0.5f), Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitY);

		var vertexBuffer = new VertexBuffer(
			device,
			VertexPositionColor.VertexDeclaration,
			vertices.Count,
			BufferUsage.None);
		vertexBuffer.SetData(vertices.ToArray());

		var indexBuffer = new IndexBuffer(
			device,
			IndexElementSize.ThirtyTwoBits,
			indices.Count,
			BufferUsage.None);
		indexBuffer.SetData(indices.ToArray());

		return new DrMeshPart(
			vertexBuffer,
			indexBuffer,
			new BoundingBox(new Vector3(-0.5f), new Vector3(0.5f)),
			PrimitiveType.TriangleList,
			vertices.Count,
			indices.Count / 3);

		void AddFace(Vector3 origin, Vector3 right, Vector3 up, Vector3 normal)
		{
			var color = new Color(
				(normal.X * 0.5f) + 0.5f,
				(normal.Y * 0.5f) + 0.5f,
				(normal.Z * 0.5f) + 0.5f);
			var start = vertices.Count;
			vertices.Add(new VertexPositionColor(origin, color));
			vertices.Add(new VertexPositionColor(origin + right, color));
			vertices.Add(new VertexPositionColor(origin + right + up, color));
			vertices.Add(new VertexPositionColor(origin + up, color));
			indices.Add(start);
			indices.Add(start + 1);
			indices.Add(start + 2);
			indices.Add(start);
			indices.Add(start + 2);
			indices.Add(start + 3);
		}
	}

	private enum PartKind
	{
		Unlit,
		LitTextured,
		AlphaTextured,
		VertexColor,
	}
}
