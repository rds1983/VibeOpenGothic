using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VibeOpenGothic.Assets.Meshes;

public readonly record struct MeshVertex(
	Vector3 Position,
	Vector3 Normal,
	Vector2 TexCoord) : IVertexType
{
	public static readonly VertexDeclaration Declaration = new(
		new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
		new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
		new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0));

	VertexDeclaration IVertexType.VertexDeclaration => Declaration;
}
