using Microsoft.Xna.Framework;

namespace VibeOpenGothic.Assets.Meshes;

public sealed class MeshData
{
    public MeshData(
        string name,
        MeshSourceKind sourceKind,
        ReadOnlyMemory<MeshVertex> vertices,
        ReadOnlyMemory<int> indices,
        IReadOnlyList<MeshSubMesh> subMeshes,
        BoundingBox bounds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        SourceKind = sourceKind;
        Vertices = vertices;
        Indices = indices;
        SubMeshes = subMeshes;
        Bounds = bounds;
    }

    public string Name { get; }

    /// <summary>Index space for <see cref="MeshSubMesh.MaterialIndex"/>.</summary>
    public MeshSourceKind SourceKind { get; }

    public ReadOnlyMemory<MeshVertex> Vertices { get; }

    public ReadOnlyMemory<int> Indices { get; }

    public IReadOnlyList<MeshSubMesh> SubMeshes { get; }

    public BoundingBox Bounds { get; }

    public int VertexCount => Vertices.Length;

    public int IndexCount => Indices.Length;

    public int TriangleCount => IndexCount / 3;
}
