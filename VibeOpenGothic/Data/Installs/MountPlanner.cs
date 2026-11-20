namespace VibeOpenGothic.Data.Installs;

public static class MountPlanner
{
	/// <summary>
	/// Orders archives for mounting with <c>Older</c> overwrite semantics:
	/// mods first, then newer archives, then higher enumeration order. Mounting
	/// in this order means newer/mod archives win conflicts.
	/// </summary>
	public static IReadOnlyList<VdfArchive> Order(IEnumerable<VdfArchive> archives)
	{
		ArgumentNullException.ThrowIfNull(archives);

		return archives
			.OrderByDescending(archive => archive.IsMod)
			.ThenByDescending(archive => archive.Timestamp)
			.ThenByDescending(archive => archive.Order)
			.ToArray();
	}
}
