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
- VOB transforms are composed root-to-leaf and applied once to shared static
  meshes.
- The `zCVobStartpoint` gives the first free-camera position and direction.
- BSP queries, sky, collision, spawn logic, NPCs and moving objects remain for
  T1.9/T2.x/T3.x.
- `--check-world` reports 23 roots / 23,288 VOBs, 11,546 static instances from
  312 unique meshes, 2,959 waypoints, 473k world triangles and a start point on
  G2 NotR. The frame submits about 2.58M triangles and 21k draw calls once all
  static instances are drawn. The CPU load takes about 1.8 s; GPU upload about
  0.1 s for the world and 0.2 s for static meshes on the development machine.
- Lightmaps are present in the world data but not applied yet: the current
  vertex format has no lightmap UV, so `DualTextureEffect` waits for a dedicated
  lightmap-projection task.

## Coordinate spaces

Gothic data is Y-up, centimetres and **left-handed**; headings live in the XZ
plane. The engine is Y-up, centimetres and **right-handed**. A VOB stores a
position plus a 3x3 rotation, which the loader converts with
`Core.CoordinateSystem` before the object is created. Engine rules:

- `WorldTransform` = parent transform * local transform, composed root to leaf,
  entirely in engine space.
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

### Character controller requirements

- Walk speed, run speed, sneak speed come from scripted attributes.
- Slope limit ~45 degrees, step height ~30 cm, capsule radius ~20 cm, height
  from the NPC's collision height (skeleton `bboxCol`).
- Ground snapping prevents stair descent from becoming a fall.
- Slide along walls instead of stopping; do not let the capsule enter geometry.
- The controller reports `OnGround`, `Landed(velocity)`, `HitWall`, and the
  current ground material for footsteps.
- Swimming, climbing and ladders are separate states added after walking works.

## Queries

`IPhysicsWorld` exposes:

```csharp
bool Raycast(Vector3 from, Vector3 to, out Hit hit, CollisionMask mask);
bool SphereCast(...);
bool OverlapBox(...);
```

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
2. Drop a capsule onto a triangle mesh; it rests at the expected height.
3. Walk up a 30-degree slope: moves; 60-degree slope: blocked.
4. Step over a 25 cm step: passes; 50 cm: blocked.
5. Wall slide at 45 degrees: tangential motion is preserved.
6. Camera ray passes through a doorway with the correct collision mask.
7. Moving kinematic platform carries the capsule upward.

World tests (require game data, skipped otherwise):

- Load `newworld.zen`; assert root object count, waypoint count, mesh triangle
  count within a tolerance, and that every VOB maps to a known engine type.
- Load each world reachable from the script and report failures; this is the
  acceptance evidence for T1.6.

## Risks

- Gothic world meshes are large and one Bepu `Mesh` per world may be expensive
  to build; measure before adding spatial decomposition.
- VOB collision and visual meshes do not always match; use the collision mesh
  (`VisualMesh`/`MultiResolutionMesh`) for physics, not the rendered LOD.
- Modded worlds often contain zero-area triangles and broken rotations. Sanitize
  (skip degenerate triangles, normalize rotations) and log counts.
