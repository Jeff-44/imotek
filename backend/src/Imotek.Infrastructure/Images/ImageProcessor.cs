using System.Buffers.Binary;
using Imotek.Application.Images;

namespace Imotek.Infrastructure.Images;

/// <summary>
/// Identifies JPEG, PNG and WebP files from their bytes and rewrites them without
/// metadata blocks (EXIF with GPS position, XMP, IPTC, comments, text chunks).
/// The pixel data is copied untouched, so no image library is required.
/// Anything else, including malformed structures, is rejected.
/// </summary>
public sealed class ImageProcessor : IImageProcessor
{
    public SanitizedImage? Sanitize(byte[] content)
    {
        if (IsJpeg(content))
        {
            return StripJpeg(content) is { } jpeg ? new SanitizedImage(jpeg, "image/jpeg", ".jpg") : null;
        }

        if (IsPng(content))
        {
            return StripPng(content) is { } png ? new SanitizedImage(png, "image/png", ".png") : null;
        }

        if (IsWebP(content))
        {
            return StripWebP(content) is { } webp ? new SanitizedImage(webp, "image/webp", ".webp") : null;
        }

        return null;
    }

    private static bool IsJpeg(byte[] b) => b.Length > 4 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static bool IsPng(byte[] b) => b.Length > 8 && b.AsSpan(0, 8).SequenceEqual(PngSignature);

    private static bool IsWebP(byte[] b) =>
        b.Length > 16 && b.AsSpan(0, 4).SequenceEqual("RIFF"u8) && b.AsSpan(8, 4).SequenceEqual("WEBP"u8);

    // JPEG: a sequence of marker segments until the start of scan (SOS), then entropy-coded data.
    private static byte[]? StripJpeg(byte[] data)
    {
        using var output = new MemoryStream(data.Length);
        output.Write(data, 0, 2);
        var pos = 2;

        while (pos + 4 <= data.Length)
        {
            if (data[pos] != 0xFF)
            {
                return null;
            }

            var marker = data[pos + 1];
            if (marker == 0xFF)
            {
                pos++; // fill byte
                continue;
            }

            if (marker == 0xDA)
            {
                // Start of scan: the remainder is image data.
                output.Write(data, pos, data.Length - pos);
                return output.ToArray();
            }

            if (marker is 0x01 or (>= 0xD0 and <= 0xD7))
            {
                output.Write(data, pos, 2); // markers without a length
                pos += 2;
                continue;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(pos + 2, 2));
            if (length < 2 || pos + 2 + length > data.Length)
            {
                return null;
            }

            // APP1 = EXIF/XMP, APP13 = IPTC/Photoshop, COM = comment. APP0 (JFIF) and APP2 (ICC colours) are kept.
            var isMetadata = marker is 0xE1 or 0xED or 0xFE;
            if (!isMetadata)
            {
                output.Write(data, pos, 2 + length);
            }

            pos += 2 + length;
        }

        return null; // no image data
    }

    private static readonly HashSet<string> PngMetadataChunks = ["eXIf", "tEXt", "zTXt", "iTXt", "tIME"];

    // PNG: signature, then chunks of [length][type][data][crc], starting with IHDR and ending with IEND.
    private static byte[]? StripPng(byte[] data)
    {
        using var output = new MemoryStream(data.Length);
        output.Write(data, 0, 8);
        var pos = 8;
        var first = true;

        while (pos + 12 <= data.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(pos, 4));
            if (length > int.MaxValue || pos + 12 + (long)length > data.Length)
            {
                return null;
            }

            var type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
            if (first && type != "IHDR")
            {
                return null;
            }

            first = false;
            var total = 12 + (int)length;
            if (!PngMetadataChunks.Contains(type))
            {
                output.Write(data, pos, total);
            }

            pos += total;
            if (type == "IEND")
            {
                return output.ToArray();
            }
        }

        return null; // no IEND
    }

    // WebP: RIFF container of chunks [fourcc][size LE][data][pad to even].
    private static byte[]? StripWebP(byte[] data)
    {
        var riffSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(4, 4));
        if (riffSize + 8L > data.Length || riffSize < 4)
        {
            return null;
        }

        var end = (int)riffSize + 8;
        using var output = new MemoryStream(end);
        output.Write(data, 0, 12);
        var pos = 12;
        var hasImage = false;

        while (pos + 8 <= end)
        {
            var fourCc = System.Text.Encoding.ASCII.GetString(data, pos, 4);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos + 4, 4));
            var padded = size + (size & 1);
            if (pos + 8L + padded > end)
            {
                return null;
            }

            var chunk = data.AsSpan(pos, 8 + (int)padded).ToArray();
            switch (fourCc)
            {
                case "EXIF" or "XMP ":
                    break;
                case "VP8X":
                    if (size < 1)
                    {
                        return null;
                    }

                    chunk[8] &= unchecked((byte)~0x0C); // clear the EXIF (0x08) and XMP (0x04) flags
                    output.Write(chunk);
                    break;
                default:
                    hasImage |= fourCc is "VP8 " or "VP8L";
                    output.Write(chunk);
                    break;
            }

            pos += 8 + (int)padded;
        }

        if (!hasImage)
        {
            return null;
        }

        var result = output.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4, 4), (uint)(result.Length - 8));
        return result;
    }
}
