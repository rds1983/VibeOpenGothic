using DigitalRiseModel;
using VibeOpenGothic.Assets.Animation;
using VibeOpenGothic.Assets.Materials;
using VibeOpenGothic.Assets.Meshes;
using VibeOpenGothic.Assets.Skins;

namespace VibeOpenGothic.Assets.Models;

/// <summary>
/// CPU-side skinned model: the DigitalRiseModel skeleton/clips, the baked
/// standard-LBS meshes and per-mesh skins. GPU buffers are created by the
/// rendering layer.
/// </summary>
public sealed class GothicModelAsset
{
	public GothicModelAsset(
		string key,
		string sourcePath,
		DrModel model,
		IReadOnlyList<GothicModelMesh> meshes,
		IReadOnlyList<GothicModelAttachment> attachments,
		GothicAnimationClip? idleClip,
		IReadOnlyList<string> warnings)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
		ArgumentNullException.ThrowIfNull(model);
		ArgumentNullException.ThrowIfNull(meshes);
		ArgumentNullException.ThrowIfNull(attachments);
		ArgumentNullException.ThrowIfNull(warnings);

		Key = key;
		SourcePath = sourcePath;
		Model = model;
		Meshes = meshes;
		Attachments = attachments;
		IdleClip = idleClip;
		Warnings = warnings;

		foreach (var mesh in meshes)
		{
			TriangleCount += mesh.Geometry.TriangleCount;
			VertexCount += mesh.Geometry.VertexCount;
		}

		foreach (var attachment in attachments)
		{
			TriangleCount += attachment.Mesh.TriangleCount;
			VertexCount += attachment.Mesh.VertexCount;
		}
	}

	public string Key { get; }

	public string SourcePath { get; }

	public DrModel Model { get; }

	public IReadOnlyList<GothicModelMesh> Meshes { get; }

	public IReadOnlyList<GothicModelAttachment> Attachments { get; }

	public GothicAnimationClip? IdleClip { get; }

	public IReadOnlyList<string> Warnings { get; }

	public int TriangleCount { get; private set; }

	public int VertexCount { get; private set; }
}

public sealed record GothicModelMesh(
	SkinnedMeshData Geometry,
	MaterialSet Materials,
	DrSkin Skin,
	int JointCount,
	bool ExceedsSkinnedEffectLimit);

public sealed record GothicModelAttachment(
	string NodeName,
	MeshData Mesh,
	MaterialSet Materials);
