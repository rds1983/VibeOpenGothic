using System.Buffers.Binary;

namespace VibeOpenGothic.Data.Installs;

public static class VdfTimestampReader
{
	// VDF header layout used by the original engine:
	//   256-byte comment, 16-byte signature, 4-byte entry count,
	//   4-byte version, 4-byte timestamp.
	private const int TimestampOffset = 280;
	private const int HeaderSize = TimestampOffset + sizeof(uint);

	public static uint Read(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		try
		{
			using var stream = File.OpenRead(path);
			if (stream.Length < HeaderSize)
			{
				return 0;
			}

			stream.Seek(TimestampOffset, SeekOrigin.Begin);

			Span<byte> buffer = stackalloc byte[sizeof(uint)];
			if (stream.Read(buffer) != buffer.Length)
			{
				return 0;
			}

			return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
		}
		catch (Exception exception) when (
			exception is IOException or UnauthorizedAccessException)
		{
			return 0;
		}
	}
}
