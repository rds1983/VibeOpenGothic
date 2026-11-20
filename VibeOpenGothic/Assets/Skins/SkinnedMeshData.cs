using Microsoft.Xna.Framework;
using VibeOpenGothic.Assets.Meshes;

namespace VibeOpenGothic.Assets.Skins;

public sealed class SkinnedMeshData
{
	public SkinnedMeshData(
		string name,
		ReadOnlyMemory<SkinnedVertex> vertices,
		ReadOnlyMemory<int> indices,
		IReadOnlyList<MeshSubMesh> subMeshes,
		BoundingBox bounds,
		SkinSkeleton skeleton)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(subMeshes);
		ArgumentNullException.ThrowIfNull(skeleton);

		Name = name;
		Vertices = vertices;
		Indices = indices;
		SubMeshes = subMeshes;
		Bounds = bounds;
		Skeleton = skeleton;
	}

	public string Name { get; }

	public ReadOnlyMemory<SkinnedVertex> Vertices { get; }

	public ReadOnlyMemory<int> Indices { get; }

	public IReadOnlyList<MeshSubMesh> SubMeshes { get; }

	public BoundingBox Bounds { get; }

	public SkinSkeleton Skeleton { get; }

	public int VertexCount => Vertices.Length;

	public int IndexCount => Indices.Length;

	public int TriangleCount => IndexCount / 3;

	public int BoneCount => Skeleton.BoneCount;
}
