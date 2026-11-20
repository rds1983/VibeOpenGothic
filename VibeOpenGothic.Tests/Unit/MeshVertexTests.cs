using Microsoft.Xna.Framework.Graphics;
using VibeOpenGothic.Assets.Meshes;

namespace VibeOpenGothic.Tests.Unit;

public sealed class MeshVertexTests
{
	[Fact]
	public void Declaration_MatchesVertexLayout()
	{
		var declaration = ((IVertexType)default(MeshVertex)).VertexDeclaration;

		Assert.Equal(32, declaration.VertexStride);
		Assert.Collection(
			declaration.GetVertexElements(),
			element => AssertElement(element, 0, VertexElementFormat.Vector3, VertexElementUsage.Position),
			element => AssertElement(element, 12, VertexElementFormat.Vector3, VertexElementUsage.Normal),
			element => AssertElement(element, 24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate));
	}

	private static void AssertElement(
		VertexElement element,
		int offset,
		VertexElementFormat format,
		VertexElementUsage usage)
	{
		Assert.Equal(offset, element.Offset);
		Assert.Equal(format, element.VertexElementFormat);
		Assert.Equal(usage, element.VertexElementUsage);
	}
}
