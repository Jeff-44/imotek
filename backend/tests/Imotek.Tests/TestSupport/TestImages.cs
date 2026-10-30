using System.Buffers.Binary;
using System.Text;

namespace Imotek.Tests.TestSupport;

/// <summary>Tiny but structurally valid images, with and without metadata blocks.</summary>
internal static class TestImages
{
    public const string GpsMarker = "GPS-48.8566N-2.3522E";

    /// <summary>SOI, APP0 (JFIF), APP1 (EXIF with a fake GPS string), COM, DQT, SOS + data, EOI.</summary>
    public static byte[] JpegWithExif()
    {
        var output = new MemoryStream();
        output.Write([0xFF, 0xD8]);
        WriteSegment(output, 0xE0, "JFIF\0\u0001\u0001\0\0\u0001\0\u0001\0\0"u8.ToArray());
        WriteSegment(output, 0xE1, [.. "Exif\0\0"u8.ToArray(), .. Encoding.ASCII.GetBytes(GpsMarker)]);
        WriteSegment(output, 0xFE, Encoding.ASCII.GetBytes("comment " + GpsMarker));
        WriteSegment(output, 0xDB, new byte[65]);
        output.Write([0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]);
        output.Write([0x12, 0x34, 0x56, 0x78]);
        output.Write([0xFF, 0xD9]);
        return output.ToArray();
    }

    public static byte[] PngWithText()
    {
        var output = new MemoryStream();
        output.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(output, "IHDR", [0, 0, 0, 1, 0, 0, 0, 1, 8, 2, 0, 0, 0]);
        WriteChunk(output, "tEXt", Encoding.ASCII.GetBytes("Comment\0" + GpsMarker));
        WriteChunk(output, "eXIf", Encoding.ASCII.GetBytes(GpsMarker));
        WriteChunk(output, "IDAT", [0x78, 0x9C, 0x63, 0x60, 0x00, 0x00, 0x00, 0x04, 0x00, 0x01]);
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    /// <summary>Extended WebP (VP8X with EXIF and XMP flags) carrying EXIF and XMP chunks.</summary>
    public static byte[] WebPWithExif()
    {
        var body = new MemoryStream();
        body.Write("WEBP"u8);
        WriteRiffChunk(body, "VP8X", [0x0C, 0, 0, 0, 0, 0, 0, 0, 0, 0]);
        WriteRiffChunk(body, "VP8 ", new byte[11]);
        WriteRiffChunk(body, "EXIF", Encoding.ASCII.GetBytes(GpsMarker));
        WriteRiffChunk(body, "XMP ", Encoding.ASCII.GetBytes("<x>" + GpsMarker + "</x>"));

        var output = new MemoryStream();
        output.Write("RIFF"u8);
        output.Write(LittleEndian((uint)body.Length));
        output.Write(body.ToArray());
        return output.ToArray();
    }

    public static byte[] Svg() => Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");

    public static byte[] Html() => Encoding.UTF8.GetBytes("<html><body><script>alert(1)</script></body></html>");

    /// <summary>A JPEG signature followed by garbage: no valid segment structure.</summary>
    public static byte[] TruncatedJpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0xFF, 0x00];

    public static bool Contains(byte[] data, string text) =>
        data.AsSpan().IndexOf(Encoding.ASCII.GetBytes(text)) >= 0;

    private static void WriteSegment(Stream output, byte marker, byte[] payload)
    {
        output.Write([0xFF, marker]);
        var length = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)(payload.Length + 2));
        output.Write(length);
        output.Write(payload);
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        output.Write(length);
        var typeAndData = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        output.Write(typeAndData);
        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(typeAndData));
        output.Write(crc);
    }

    // CRC-32 as used by PNG (polynomial 0xEDB88320).
    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        return ~crc;
    }

    private static void WriteRiffChunk(Stream output, string fourCc, byte[] data)
    {
        output.Write(Encoding.ASCII.GetBytes(fourCc));
        output.Write(LittleEndian((uint)data.Length));
        output.Write(data);
        if (data.Length % 2 == 1)
        {
            output.WriteByte(0);
        }
    }

    private static byte[] LittleEndian(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }
}
