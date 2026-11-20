namespace VibeOpenGothic.Assets.Meshes;

public sealed record MeshConversionStats(
    int SourcePolygonCount,
    int SourceTriangleCount,
    int DegenerateTriangleCount,
    int InvalidPolygonCount,
    int VertexCount,
    int IndexCount,
    int SubMeshCount);

public sealed record MeshConversionResult(
    MeshData Mesh,
    MeshConversionStats Stats);
