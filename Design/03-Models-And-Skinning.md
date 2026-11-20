# Models and skinning

## Gothic model files

| File | Meaning |
| --- | --- |
| `.MDS` | Model script: points to a hierarchy and mesh, defines animations, events, attachment points |
| `.MDH` | Model hierarchy: skeleton nodes with names, parent indices and local transforms |
| `.MDM` | Multi-resolution model mesh: positions, wedges (normal + UV), polygons, materials, LODs |
| `.MMB` | Morph mesh: base mesh plus per-vertex position deltas and morph animations |
| `.MSH` | Static/world mesh: positions, features (normal + UV), polygons, materials |

A skinned character is `MDS + MDH + MDM`. The renderer needs bind-pose geometry,
a bone hierarchy, inverse bind matrices, and animation clips.

### Compiled model resolution

An MDS points at source names (`*.ASC`); the loader resolves the compiled files
case-insensitively, mirroring `Resources::loadMesh` in OpenGothic:

1. Hierarchy: `<skeleton stem>.MDH`, then `<script stem>.MDH`.
2. If neither exists, a `.MDL` bundle named after either stem; it contains both
   hierarchy and mesh.
3. Meshes: `<mesh stem>.MDM` for the skeleton name and for every name in the MDS
   mesh list.

Examples: `HumanS.mds` (the same file is `HUMANS.MDS` case-insensitively) names
the skeleton `Hum_Body_Naked0.ASC`; no `Hum_Body_Naked0.MDH` exists, so the
script stem resolves to `HUMANS.MDH` and the mesh to `HUM_BODY_NAKED0.MDM`.
`Orc.mds` resolves to `ORC.MDH` + `ORC_BODY.MDM`. `ModelResourceResolver`
implements this and is unit tested with an in-memory VFS.

## Coordinate conversion (LHS to RHS)

Gothic uses a **left-handed, Y-up** system: +X right, +Y up, +Z forward into the
screen. MonoGame uses a **right-handed, Y-up** system: +Z points toward the
viewer. The conversion is a reflection across the XY plane:

```
M = diag(1, 1, -1)
```

Applying a reflection changes handedness but cannot be replaced by a rotation,
so it is done exactly once, at load, through `Core.CoordinateSystem`:

| Data | Conversion |
| --- | --- |
| Point, direction | `(x, y, -z)` |
| Normal | `(x, y, -z)`, then normalize |
| Triangle | Reverse winding: `(a, b, c)` becomes `(a, c, b)` |
| Matrix (linear part) | `R' = M * R * M` (M is symmetric and self-inverse) |
| Matrix translation | `t' = M * t` |
| Quaternion | Conjugate by M: axis `n` becomes `M*n`, angle negates; for `(x,y,z,w)` this is `(-x, -y, z, w)` up to sign |
| Scalar yaw | Negate (a rotation about Y maps to the opposite angle) |

Rules:

- Convert the mesh's local positions/normals **and** the skeleton's bind
  transforms with the same M before computing bind positions and inverse bind
  matrices. The skinning math is then entirely in engine space.
- Convert animation keyframe transforms (or quaternions) with the same helper.
- Convert VOB transforms, waypoint positions/directions, attachment transforms,
  collision meshes and camera target vectors. Everything the engine consumes is
  RHS.
- Morph deltas, particle emitter directions, decal orientations, light
  directions and sky vectors use the same conversion. A vector is a vector
  regardless of which subsystem owns it.
- Do not convert at draw time and do not add sign flips in shaders or gameplay
  code. Data is converted once and cached in engine space.
- UVs are not reflected by default. Verify with an asymmetric textured asset; if
  a specific material appears mirrored, fix it at the material level, not with a
  global texture flip.
- The conversion is its own inverse: applying it twice restores the input. Tests
  assert this, plus the winding/normal relationship below.

Normal/winding relationship: reflecting positions of a triangle flips the
cross-product orientation. Reversing the index order restores an outward normal
equal to the reflected normal. If a model looks inside-out, one of these two
steps is missing.

Implementation: `Core.CoordinateSystem` exposes `ToEnginePoint`,
`ToEngineDirection`, `ToEngineNormal`, `ToEngineYaw`, `ToEngineMatrix`,
`ToEngineQuaternion`, `ToEngineBounds` and the winding helpers. The matrix
conversion follows MonoGame's row-vector convention (`Reflection * matrix *
Reflection`, including the translation row). Unit tests cover the involution,
matrix/quaternion equivalence, yaw/direction equivalence, bounds and the
normal/winding relationship.

Because the conversion is its own inverse, accidentally converting twice leaves
data in Gothic space and is invisible in the values alone. Loaders must call the
helper exactly once; the involution tests pin this contract, and an asymmetric
textured asset catches visual regressions.

## The Gothic skinning scheme

In a `.MDM` skinned mesh, positions are not ordinary vertices. Each position
stores up to four influences:

```c
struct SoftSkinWeightEntry {
    ushort NodeIndex;  // bone
    float  Weight;     // normalized at load time
    Vec3   Position;   // position in that bone's local space
};
```

The original renderer computes the animated position as:

```
p = sum_i w_i * (G[node_i] * position_i)
```

where `G[node_i]` is the node's current global transform. This is not linear
blend skinning over one shared vertex position; every influence carries its own
local position. OpenGothic feeds all four positions to the vertex shader and
performs exactly the sum above.

This layout is non-standard. It forces a fat vertex (4 positions), it is not
supported by the standard skinning paths in `SkinnedEffect`, DigitalRiseModel or
glTF, and it duplicates data. We convert it once at load time.

## Conversion to standard LBS

Let `B_i` be the bind-pose global transform of bone `i`, composed from the MDH
hierarchy (root to leaf, local transforms as stored in the file) and converted
to engine space. For every Gothic vertex:

1. Read influences `(n_i, w_i, q_i)`, where `q_i` is the stored local position,
   converted to engine space.
2. Normalize weights so they sum to 1. If the sum is ~0, keep the first
   influence with weight 1 and log a warning.
3. Compute the bind-pose position:

   ```
   p_bind = sum_i w_i * (B[n_i] * q_i)
   ```

4. Store `p_bind` as the single vertex position, plus `n_i` and `w_i`.
5. Store `inverseBind[i] = inverse(B[i])` once per skeleton.

ZenKit exposes each MDH node transform as a column-vector matrix (translation in
`M14`/`M24`/`M34` of its `System.Numerics.Matrix4x4`). Transpose it into
MonoGame's row-vector layout before applying the reflection. Without that
transpose, bind positions are hundreds of metres off. The MDH root translation is
added to the root nodes' local translation before composition, matching
`Skeleton::mkSkeleton` in the original.

At runtime, use standard linear blend skinning:

```
p_animated = sum_i w_i * (G[n_i] * inverseBind[n_i]) * p_bind
```

### Why this is correct

If the four influences agree on the bind-pose world position (the normal case),
then `B[n_i] * q_i = p_common` for every `i`, so `p_bind = p_common` and

```
sum_i w_i * G[n_i] * inverseBind[n_i] * p_common
  = sum_i w_i * G[n_i] * q_i
```

which is exactly the Gothic formula.

If the influences disagree, the two formulas cannot both be represented by one
position. We use the weighted average (step 3), which minimizes the weighted
squared error at the bind pose. Two diagnostics are recorded:

- **raw bind inconsistency**: `max_vertex max_i |B[n_i] * q_i - p_bind|`;
- **weighted bind inconsistency**: `max_vertex sum_i w_i * |B[n_i] * q_i - p_bind|`.

The weighted value is the meaningful one: under rigid bone deltas it bounds the
difference between standard LBS and the Gothic four-position formula. The raw
value is dominated by negligible influences.

Real G2 NotR data (2026-11-20) shows that the default skeleton mesh is almost
always exact: vanilla bodies are `0`, and the worst of 64 primary meshes is
`2.713 cm` raw / `1.569 cm` weighted. Separate armor, head and variant meshes are
genuinely inconsistent: many are exported against an approximated bind pose, with
raw `4-17 cm` and weighted `2-9 cm`. The weighted average is still the best
single-position representation and matches precisely at bind pose.

Thresholds:

- `< 0.5 cm` weighted: silent.
- `0.5 - 2 cm` weighted: log once per model at debug level.
- `> 2 cm` weighted: log a warning with the mesh name and inconsistent vertex
  count; count it in `--check-models`. This is normal for vanilla armor meshes and
  is not an error.

Never fail a world load because of this metric.

### Edge cases

| Case | Handling |
| --- | --- |
| 1-3 influences | Pad with bone 0, weight 0 |
| Weights sum != 1 (common in mods) | Renormalize; log once per model |
| More than 4 influences | Not present in Gothic data; truncate to the 4 largest and warn |
| Duplicate bone indices | Merge weights when indices are equal |
| Unknown node index | Treat as bone 0, weight 0; log |
| Bone names duplicated | Use hierarchy indices, not names, for skinning |
| Bind pose from animation frame 0 differs from MDH | Use MDH. If visual evidence shows otherwise, add a per-model override, never a global heuristic |

### Vertex format

```csharp
public struct SkinnedVertex : IVertexType
{
    public Vector3 Position;      // bind pose, centimetres
    public Vector3 Normal;        // bind pose
    public Vector2 TexCoord;
    public Vector4 BlendWeights;  // normalized, sums to 1
    public Byte4   BlendIndices;  // indices into the skeleton
}
```

The vertex declaration is 52 bytes (`SkinnedVertex.Declaration`), matching
`VertexPositionNormalTextureSkinned`.

`SkinnedEffect` is configured with `WeightsPerVertex = 4`. It accepts at most
**72 bones**. Count nodes before creating the model:

- `<= 72`: use `SkinnedEffect`.
- `> 72`: log the model and node count. For the first pass, remap to the 72
  bones that actually have weights (Gothic skeletons often contain many
  non-deforming nodes). If a model still exceeds the limit, split mesh parts by
  dominant bone set. A custom effect is a last resort and belongs to T4.x.

### Normals

Apply the same weights and bone rotations to normals; MonoGame's `SkinnedEffect`
does this correctly for `VertexPositionNormalTexture`-style skinned vertices.
Do not bake normalized normals through the bind transform; keep them in bind
space and let the GPU skin them.

## Skeleton and animation

Build the runtime skeleton from `MDH` and the clips from `MDS`:

- One `DrModelBone` per hierarchy node, preserving order and parent links. The
  bone index must match the index used by the mesh weights and animation
  channels.
- `DrSkinJoint(bone, inverseBind)` for every deforming bone.
- One `AnimationClip` per MDS animation. Each channel targets a bone index and
  holds translation and rotation keyframes (scale is 1). Convert frame numbers
  to milliseconds using the animation's FPS; `TimeSpan` is used by
  DigitalRiseModel.
- Store the original frame numbers, event frame indices and the loop/transition
  flag alongside the clip. Script events (`MdsEventTag`, sound, particle,
  morph) belong to our animation layer, not to DigitalRiseModel.

`DrModelInstance.GetSkinTransforms(skinIndex)` produces the bone matrices for
`SkinnedEffect.SetBoneTransforms`.

### Animation layer

Implement in `Assets.Animation` + `Game.Animation`:

- Play, loop, stop, and crossfade between clips. Gothic transition animations
  (`TRANS_*`) are handled by the animation solver, not by a generic crossfade.
- Overlays (e.g. torch in hand on top of a walk cycle) using additive or
  per-bone replacement, matching the original `AnimationSolver`.
- Root motion: some clips carry a translation (`moveTr`). Consume it in the NPC
  controller; never let it move the rendered mesh twice.
- Events: fire at the correct frame, including hit windows, footsteps, sounds
  and particle triggers.
- "Fatness": the original offsets positions along normals per NPC. Implement as
  a vertex-shader parameter or a CPU bound, not by modifying shared meshes.

## Attachments and morphs

- Attachments are separate static meshes parented to a named node. Compose the
  node's global transform with the attachment's local transform.
- Morph meshes (`.MMB`) use per-vertex deltas and animation weights, not bones.
  For the first pass, render the base mesh with `BasicEffect` and implement
  morphing late (T4.7). Never mix morph and skinning in one vertex buffer.
- Multi-resolution meshes contain LODs. Load the highest LOD first; LOD
  selection is a performance task (T5.1).

## What we deliberately do not do

- Do not keep the four-position layout "just in case" in the main path. Keep a
  debug converter that can verify the conversion against it in tests.
- Do not bake animations into vertex buffers.
- Do not use `ModelProcessor`/content pipeline for Gothic data; it is runtime
  data, not build-time content.
- Do not scale meshes at load time to hide unit or axis bugs.

## Verification

Unit tests (no GPU required):

1. Coordinate conversion is an involution for points/directions/normals and is
   equivalent to the matrix/quaternion conversions; a reflected triangle with
   reversed winding has the reflected normal.
2. Two-bone synthetic skeleton with a shared bind position: converted LBS
   matches the Gothic formula within `1e-5` (after converting both sides).
3. Random skeleton (up to 8 bones), random weights that sum to 1, consistent
   local positions: same check, 1000 cases.
4. Inconsistent positions: the reported error matches the analytic deviation,
   and the baked position is the weighted average.
5. Weight normalization: sums 0.9 and 1.1 both produce sum 1 and a warning.
6. Bone index padding and duplicate merging.

Diagnostics:

- `--check-models` parses every `MDS` reachable from the mounted VFS, resolves
  MDH/MDL + MDM files, bakes every unique skinned mesh and reports: primary and
  variant bind inconsistency, bone counts, over-72 meshes and weight-sum
  anomalies. It does not crash on missing variants; they are reported as
  `MISSING`.
- Current G2 NotR result (2026-11-20, patch 5): 117 MDS scripts, 171 unique
  skinned meshes (64 primary, 107 variant), 103,421 triangles, 74,613 vertices,
  0 failures, 0 missing meshes, 1 duplicate reference, 0 weight anomalies. Max
  bones 79; 10 meshes exceed the 72-bone `SkinnedEffect` limit. Primary bind
  inconsistency is at most `2.713 cm` raw / `1.569 cm` weighted; all meshes
  (including armor/variants) reach `43.191 cm` raw / `21.816 cm` weighted.
  Bones above 72 are handled by the remap described above before rendering; the
  current task only converts and reports them.
- This command is the acceptance evidence for the data-conversion tasks.

Visual:

- Render an asymmetric textured model (a sign with readable text, or a model
  with a distinct left/right feature) and confirm the text is readable, the
  silhouette matches the original data, and nothing is inside-out.
- Screenshot an NPC in a T-pose/idle at a fixed camera, before and after
  conversion. The silhouettes must match within anti-aliasing tolerance.
