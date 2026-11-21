using System;

namespace VibeOpenGothic.Physics;

[Flags]
public enum CollisionLayer : uint
{
	None = 0,
	World = 1u << 0,
	StaticProp = 1u << 1,
	Npc = 1u << 2,
	Player = 1u << 3,
	Item = 1u << 4,
	Projectile = 1u << 5,
	Camera = 1u << 6,
	Focus = 1u << 7,
	All = World | StaticProp | Npc | Player | Item | Projectile | Camera | Focus,
}

public readonly record struct CollisionFilter(
	CollisionLayer Layer,
	CollisionLayer Mask)
{
	public bool Allows(CollisionFilter other)
	{
		return (Mask & other.Layer) != 0 && (other.Mask & Layer) != 0;
	}
}

public static class CollisionMasks
{
	public const CollisionLayer World =
		CollisionLayer.StaticProp |
		CollisionLayer.Npc |
		CollisionLayer.Player |
		CollisionLayer.Item |
		CollisionLayer.Projectile;

	public const CollisionLayer StaticProp =
		CollisionLayer.World |
		CollisionLayer.Npc |
		CollisionLayer.Player |
		CollisionLayer.Item |
		CollisionLayer.Projectile;

	public const CollisionLayer Npc =
		CollisionLayer.World |
		CollisionLayer.StaticProp |
		CollisionLayer.Npc |
		CollisionLayer.Player |
		CollisionLayer.Item |
		CollisionLayer.Projectile;

	public const CollisionLayer Player =
		CollisionLayer.World |
		CollisionLayer.StaticProp |
		CollisionLayer.Npc |
		CollisionLayer.Player |
		CollisionLayer.Item |
		CollisionLayer.Projectile;

	public const CollisionLayer Item =
		CollisionLayer.World |
		CollisionLayer.StaticProp |
		CollisionLayer.Npc |
		CollisionLayer.Player |
		CollisionLayer.Item;

	public const CollisionLayer Projectile =
		CollisionLayer.World |
		CollisionLayer.StaticProp |
		CollisionLayer.Npc |
		CollisionLayer.Player;

	public const CollisionLayer Camera =
		CollisionLayer.World |
		CollisionLayer.StaticProp;

	public const CollisionLayer Focus =
		CollisionLayer.World |
		CollisionLayer.StaticProp |
		CollisionLayer.Npc |
		CollisionLayer.Item;

	public static CollisionLayer For(CollisionLayer layer)
	{
		return layer switch
		{
			CollisionLayer.World => World,
			CollisionLayer.StaticProp => StaticProp,
			CollisionLayer.Npc => Npc,
			CollisionLayer.Player => Player,
			CollisionLayer.Item => Item,
			CollisionLayer.Projectile => Projectile,
			CollisionLayer.Camera => Camera,
			CollisionLayer.Focus => Focus,
			_ => CollisionLayer.All,
		};
	}

	public static CollisionFilter Filter(CollisionLayer layer)
	{
		return new CollisionFilter(layer, For(layer));
	}
}
