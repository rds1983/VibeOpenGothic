using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Graphics.PackedVector;

namespace VibeOpenGothic.Assets.Skins;

public readonly record struct SkinnedVertex(
	Vector3 Position,
	Vector3 Normal,
	Vector2 TexCoord,
	Vector4 BlendWeights,
	Byte4 BlendIndices) : IVertexType
{
	public static readonly VertexDeclaration Declaration = new(
		new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
		new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
		new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
		new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.BlendWeight, 0),
		new VertexElement(48, VertexElementFormat.Byte4, VertexElementUsage.BlendIndices, 0));

	VertexDeclaration IVertexType.VertexDeclaration => Declaration;
}
