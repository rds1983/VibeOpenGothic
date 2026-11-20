namespace VibeOpenGothic.Assets.Meshes;

public enum MeshSourceKind
{
    /// <summary>
    /// World or static MSH mesh. <see cref="MeshSubMesh.MaterialIndex"/> indexes
    /// the source mesh's material list.
    /// </summary>
    World,

    /// <summary>
    /// Model (MDM) mesh. <see cref="MeshSubMesh.MaterialIndex"/> is the
    /// multi-resolution submesh index; its material is the submesh's material.
    /// </summary>
    Model,
}
