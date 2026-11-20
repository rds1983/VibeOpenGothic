using Microsoft.Xna.Framework.Graphics;
using VibeOpenGothic.Assets.Skins;

namespace VibeOpenGothic.Tests.Unit;

public sealed class SkinnedVertexTests
{
	[Fact]
	public void Declaration_MatchesSkinnedEffectLayout()
	{
		var declaration = ((IVertexType)default(SkinnedVertex)).VertexDeclaration;

		Assert.Equal(52, declaration.VertexStride);
		Assert.Collection(
			declaration.GetVertexElements(),
			element => AssertElement(element, 0, VertexElementFormat.Vector3, VertexElementUsage.Position),
			element => AssertElement(element, 12, VertexElementFormat.Vector3, VertexElementUsage.Normal),
			element => AssertElement(element, 24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate),
			element => AssertElement(element, 32, VertexElementFormat.Vector4, VertexElementUsage.BlendWeight),
			element => AssertElement(element, 48, VertexElementFormat.Byte4, VertexElementUsage.BlendIndices));
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
