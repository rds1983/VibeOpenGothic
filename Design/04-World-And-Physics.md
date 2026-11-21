# World and physics

## World loading

A `.ZEN` file provides, via `ZenKit.World`:

- `Mesh`: the world geometry (terrain, buildings) as triangles with materials.
- `BspTree`: BSP nodes, sectors and portal polygons for visibility and sector
  lookup.
- `RootObjects`: the VOB tree (visual objects, items, NPCs, triggers, sounds).
- `Npcs`: NPC VOBs (ZenKit exposes them separately from the tree).
- `WayNet`: waypoints and edges for AI navigation.
- `SkyController`: time of day, fog, rain and sky rendering state.
- `SpawnLocations` / start point.

Loading is staged so progress can be reported and tests can load parts:

1. Parse the world and validate the version.
2. Convert the world mesh and materials; build static collision.
3. Build the BSP lookup structures (sector and leaf queries).
4. Convert the VOB tree into engine objects, preserving parent/child transforms.
5. Build the waynet graph.
6. Initialize sky and lighting state.
7. Spawn the player at the requested waypoint or the world's start point.

World changes unload the previous world's GPU and collision data first. The
session object survives; the player and script state are re-inserted into the new
world (see [06-Gameplay.md](06-Gameplay.md)).

### T1.6 static pass

The first pass implements stages 1, 2 (visual only) and 5:

- World mesh, static `.MRM`/`.MSH` VOB visuals and waypoints are converted to
  engine space; portal/ghost-occluder polygons are skipped for the visual pass.
- ZenKit exposes each VOB position/rotation as a world-space transform, including
  children. The loader converts it once and applies it directly to shared static
  meshes; parent composition is only used for runtime local transforms.
- The `zCVobStartpoint` gives the first free-camera position and direction.
- BSP queries, sky, collision, spawn logic, NPCs and moving objects remain for
  T1.9/T2.x/T3.x.
- `--check-world` reports 23 roots / 23,288 VOBs, 11,546 static instances from
  312 unique meshes, 2,959 waypoints, 473k world triangles and a start point on
  G2 NotR. Without culling the frame submits about 2.73M triangles and 23k draw
  calls once all static and model instances are drawn. T1.9 frustum/distance
  culling reduces the start view to about 537k triangles and 2.1k draw calls.
  The CPU load takes about 3.5 s; GPU upload about 0.1 s for the world, 0.2 s for
  static meshes and 0.3 s for models on the development machine.
- Lightmaps are present in the world data but not applied yet: the current
  vertex format has no lightmap UV, so `DualTextureEffect` waits for a dedicated
  lightmap-projection task.

### T1.7 model VOBs

Model-typed VOB visuals are loaded as MDS models (and `.ASC` visuals through
their compiled `.MDL` bundle). The first pass loads the model's default soft
mesh, builds per-mesh skins and animates model VOBs with their default idle
clip. Static MDM attachments are rendered by node name. On G2 NotR `newworld.zen`
this produces 883 model instances from 61 unique models after the preview option
is off; most are MOBSI props with `S_S0` and static `.MDL` furniture. NPC VOBs
are not placed in the ZEN; scripts spawn them later, so `--spawn-model <mds>` is
the developer preview for an NPC/creature against the real world.

## Coordinate spaces

Gothic data is Y-up, centimetres and **left-handed**; headings live in the XZ
plane. The engine is Y-up, centimetres and **right-handed**. A VOB stores a
position plus a 3x3 rotation, which the loader converts with
`Core.CoordinateSystem` before the object is created. Engine rules:

- ZenKit VOB positions/rotations are world-space (including nested VOBs); the
  loader converts them directly. For runtime movers, derive the local transform
  from `parent^-1 * world`; do not apply a parent a second time during loading.
- Visual meshes are converted to engine space at load; the VOB transform is
  applied once.
- Waypoint positions and directions are converted at load.
- Physics works in metres internally; convert at the Bepu boundary. Bepu uses
  `System.Numerics` types, so `VibeOpenGothic.Physics` owns that conversion too.
- Never apply a rotation or handedness flip in two places (e.g. VOB and mesh).
  A conversion test must catch double transforms.

## Engine object model

| Engine type | Created from | Notes |
| --- | --- | --- |
| `World` | `.ZEN` | Owns meshes, vobs, waynet, sky, physics, spatial index |
| `Vob` | Any `ZenKit.Vobs.IVirtualObject` | Base: transform, flags, parent/children |
| `StaticVob` | VOB with a mesh visual | Static geometry, batched |
| `ModelVob` | VOB with a model visual | MDS/MDM instance, animated or static |
| `Npc` | NPC VOB | Script instance, AI, inventory, animation |
| `Item` | Item VOB / inventory item | Can be world or inventory |
| `Interactive` | MOBSI VOB | Doors, chests, levers, ladders, beds, etc. |
| `Trigger` | Trigger VOBs | Script, change-level, world-start, zone triggers |
| `SoundVob`, `ParticleVob`, `LightVob` | Matching visuals | Presentation only |

VOB mapping is data-driven: the type, the visual type and the script instance
decide the runtime class. Unknown types become `Vob` with a log entry; they must
not crash loading.

Flags that must be honored:

- `ShowVisual`: skip rendering but keep logic (used by scripted objects).
- `CdStatic` / `CdDynamic`: whether the VOB contributes collision.
- `PhysicsEnabled`: whether it participates in rigid-body simulation.
- `Static`: never move; allows batching and static colliders.
- `SleepMode`, `NextOnTimer`: deferred initialization for distant objects.
- `Ambient`, `AnimationType`, `AnimationStrength`: ambient animation.

## Spatial index and visibility

- Build a uniform grid or BVH over VOB bounds for focus, AI and trigger queries.
- The BSP is used for sector identification and, later, portal-based culling.
  The first renderer may draw the whole world; correctness first, culling in
  T1.9.
- Distance culling must not despawn scripted objects. "Sleeping" is logic state,
  not data unloading.

## Collision with BepuPhysics2

One `BepuPhysics.Simulation` per world, updated at a fixed 60 Hz.

- **World geometry:** a static `BepuPhysics.Collidables.Mesh` built from the
  ZEN mesh triangles. Use per-triangle materials to mark water, ladders and
  non-collidable surfaces. A world mesh is large; build it once and never
  rebuild per frame.
- **Static props:** static meshes from each collidable VOB. Combine child meshes
  into a compound where possible to reduce collider count.
- **Dynamic props:** simple convex shapes (boxes, spheres, capsules) for items
  that need to fall or be pushed. Most items can stay static until picked up.
- **NPCs and the player:** a custom capsule character controller, not a dynamic
  rigid body. It resolves contacts against the simulation using shape casts and
  a small set of contact planes, with step-up, slope limit, ground snapping and
  wall sliding. This is the standard Bepu character-controller approach; do not
  reimplement full rigid-body character physics.
- **Sensors:** triggers and zones are not Bepu colliders. Test them with box or
  sphere overlap in `World` at a low frequency (e.g. 10 Hz) and dispatch events.

Collision filters (layers): `World`, `StaticProp`, `Npc`, `Player`, `Item`,
`Projectile`, `Camera`, `Focus`. The camera and focus rays must ignore the player
and focus-target visuals appropriately.

### T2.1 static collision pass

- `VibeOpenGothic.Physics` owns the Bepu simulation. The public API uses engine
  centimetres; positions and mesh vertices convert to metres at the Bepu
  boundary (`0.01`), and gravity is `(0, -9.81, 0)` m/s².
- `WorldPhysicsBuilder` converts the converted world mesh and every static VOB
  with `CdStatic` or `CdDynamic` into `PhysicsWorldDescription`. Static meshes
  are shared as Bepu `Mesh` shapes; each instance adds one static collider with
  its converted pose. On `newworld.zen` that is 259 mesh shapes, 535,475
  triangles and 5,887 static colliders; loading takes about 4.2 s on the
  development machine.
- Degenerate, non-finite and out-of-range triangles are skipped and counted.
  `CollisionFilter` uses `CollisionLayer` flags and symmetric masks; the
  `Camera` and `Focus` masks are query-side layers. Two non-unit-scale static
  instances are currently skipped and reported.
- `PhysicsWorld.Step` accumulates elapsed time in a fixed 60 Hz loop with at
  most five substeps per caller step. `GothicGame` drives it from the existing
  `FixedStepRunner`; future dynamic bodies and the player capsule use the same
  simulation.
- `Raycast` uses Bepu's ray tests and a layer mask through `IRayHitHandler`;
  positions, distances and normals are returned in engine space. `OverlapBox`
  is a conservative broad-phase AABB query and does not yet run contact
  generation. `SphereCast` remains for T2.2.
- `--check-world` builds the collision world after the visual load, prints the
  stats and casts a downward probe from the start point.
- `GothicGame` builds the same collision world during `LoadContent` and steps it
  from the fixed logic loop. The app has no loading screen yet, so the extra
  ~4 s collision build currently leaves the window blank until the first frame.
- Per-triangle material flags (water, ladders, non-collidable surfaces) and
  non-unit-scale collision instances are deferred; they are counted in the
  build stats rather than silently ignored.
- Static VOB collision currently uses the converted visual mesh. Worlds whose
  collision mesh differs from the rendered mesh need the dedicated collision
  source in a later pass.

### Character controller requirements

- Walk speed, run speed, sneak speed come from scripted attributes.
- Slope limit ~45 degrees, step height ~30 cm, capsule radius ~20 cm, height
  from the NPC's collision height (skeleton `bboxCol`).
- Ground snapping prevents stair descent from becoming a fall.
- Slide along walls instead of stopping; do not let the capsule enter geometry.
- The controller reports `OnGround`, `Landed(velocity)`, `HitWall`, and the
  current ground material for footsteps.
- Swimming, climbing and ladders are separate states added after walking works.

### T2.2 player pass

- `Game.PlayerController` is a kinematic capsule (`Radius = 25 cm`,
  `Height = 180 cm`) moved by `PhysicsWorld.CapsuleCast`; it has no Bepu body.
  It resolves horizontal wall slides, 30 cm step-ups, gravity and a 20 cm ground
  snap. A 50 cm step is blocked by the controller tests.
- `PhysicsWorld.CapsuleCast` sweeps a capsule against the static world with the
  same `CollisionFilter` masks as raycasts. The player mask includes `World`,
  `StaticProp`, `Npc` and `Item`.
- `Game.PlayerInput` carries WASD, run/sneak, free-camera up/down and mouse look.
  `GothicGame` reads the keyboard/mouse, runs the controller at the fixed 60 Hz
  logic step and drives `PlayerCamera`.
- `Game.PlayerCamera` implements first-person, third-person and debug free
  camera modes. Third-person camera collision uses a `Camera`-mask raycast;
  `F4` cycles the modes. The fixed `--screenshot` camera is deliberately kept
  separate so deterministic world screenshots do not depend on input or player
  position.
- The camera uses the right-handed basis: screen-right is
  `cross(forward, Up)`, so mouse X decreases yaw and strafe-right moves toward
  that screen-right direction.
- Tests cover spawn/snap, walking, wall sliding, 25 cm step-up, 50 cm step
  blocking and mouse look. The interactive start view was checked with the Orc
  showcase; a multi-minute Khorinis walk remains a manual acceptance check.

## Queries

`IPhysicsWorld` exposes:

```csharp
bool Raycast(Vector3 from, Vector3 to, out PhysicsHit hit, CollisionLayer mask);
bool CapsuleCast(Vector3 center, float radius, float height,
    Vector3 direction, float distance, out PhysicsHit hit, CollisionLayer mask);
int OverlapBox(Vector3 center, Vector3 halfExtents, CollisionLayer mask,
    List<PhysicsOverlap>? results = null);
```

`OverlapBox` is a conservative broad-phase AABB query; exact shape overlap and
the remaining character queries follow with T2.2/T2.3.

Raycasts are used for focus/interaction, camera collision, line of sight,
footstep surface and bullet traces. Batch results are cached per frame; do not
cast the same ray multiple times in one frame.

## Movers and triggers

- Movers (`Mover`, `MoverController`, `TriggerChangeLevel`, doors, elevators)
  are kinematic bodies updated from their animation or lerp state. Carry the
  player by resolving against the moving collider each step.
- Trigger VOBs (`TriggerScript`, `TriggerWorldStart`, `TriggerUntouch`,
  `TriggerList`) execute Daedalus functions. They are thin adapters over
  `Game.Scripts`.
- Zone VOBs (`ZoneFog`, `ZoneMusic`, `ZoneFarPlane`, `ZoneTrigger`) change
  presentation/state when the player enters; they must not allocate per frame.

## Testing

Headless physics tests with a fixed timestep:

1. A Gothic-space point/transform converted to engine space and back is
   unchanged; a VOB parent/child chain produces the same world transform as the
   reference calculation.
2. Drop a capsule or sphere onto a triangle mesh; it rests at the expected
   height. T2.1 covers the sphere/mesh case; the capsule controller follows in
   T2.2.
3. Walk up a 30-degree slope: moves; 60-degree slope: blocked.
4. Step over a 25 cm step: passes; 50 cm: blocked.
5. Wall slide at 45 degrees: tangential motion is preserved.
6. Camera ray passes through a doorway with the correct collision mask.
7. Moving kinematic platform carries the capsule upward.

World tests (require game data, skipped otherwise):

- Load `newworld.zen`; assert root object count, waypoint count, mesh triangle
  count within a tolerance, and that every VOB maps to a known engine type.
- Build the static collision world for `newworld.zen`; assert the mesh/instance
  counts and that a downward ray from the start point hits the world. T2.1
  currently reports 259 shapes, 535,475 triangles and 5,887 statics.
- Load each world reachable from the script and report failures; this is the
  acceptance evidence for T1.6.

## Risks

- Gothic world meshes are large and one Bepu `Mesh` per world may be expensive
  to build; measure before adding spatial decomposition.
- VOB collision and visual meshes do not always match; use the collision mesh
  (`VisualMesh`/`MultiResolutionMesh`) for physics, not the rendered LOD.
- Modded worlds often contain zero-area triangles and broken rotations. Sanitize
  (skip degenerate triangles, normalize rotations) and log counts.
