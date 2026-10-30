using System.Buffers.Binary;
using Imotek.Infrastructure.Images;
using Imotek.Tests.TestSupport;

namespace Imotek.Tests.Infrastructure;

public class ImageProcessorTests
{
    private readonly ImageProcessor _processor = new();

    [Fact]
    public void Jpeg_is_accepted_and_EXIF_and_comments_are_removed()
    {
        var input = TestImages.JpegWithExif();
        Assert.True(TestImages.Contains(input, TestImages.GpsMarker));

        var image = _processor.Sanitize(input);

        Assert.NotNull(image);
        Assert.Equal("image/jpeg", image.ContentType);
        Assert.Equal(".jpg", image.Extension);
        Assert.False(TestImages.Contains(image.Content, TestImages.GpsMarker));
        Assert.True(TestImages.Contains(image.Content, "JFIF")); // non-metadata segments are kept
        Assert.Equal([0xFF, 0xD9], image.Content[^2..]); // image data copied to the end
    }

    [Fact]
    public void Png_is_accepted_and_text_and_EXIF_chunks_are_removed()
    {
        var image = _processor.Sanitize(TestImages.PngWithText());

        Assert.NotNull(image);
        Assert.Equal("image/png", image.ContentType);
        Assert.False(TestImages.Contains(image.Content, TestImages.GpsMarker));
        Assert.True(TestImages.Contains(image.Content, "IDAT"));
        Assert.True(TestImages.Contains(image.Content, "IEND"));
    }

    [Fact]
    public void WebP_is_accepted_and_EXIF_and_XMP_are_removed_with_consistent_header()
    {
        var image = _processor.Sanitize(TestImages.WebPWithExif());

        Assert.NotNull(image);
        Assert.Equal("image/webp", image.ContentType);
        Assert.False(TestImages.Contains(image.Content, TestImages.GpsMarker));
        Assert.Equal((uint)image.Content.Length - 8, BinaryPrimitives.ReadUInt32LittleEndian(image.Content.AsSpan(4, 4)));

        var vp8x = image.Content.AsSpan().IndexOf("VP8X"u8);
        Assert.Equal(0, image.Content[vp8x + 8] & 0x0C); // EXIF and XMP flags cleared
    }

    public static TheoryData<string, byte[]> Rejected => new()
    {
        { "svg", TestImages.Svg() },
        { "html renamed as image", TestImages.Html() },
        { "truncated jpeg", TestImages.TruncatedJpeg() },
        { "png without IEND", TestImages.PngWithText()[..^12] },
        { "gif", "GIF89a\u0001\0\u0001\0"u8.ToArray() },
        { "empty", [] },
    };

    [Theory]
    [MemberData(nameof(Rejected))]
    public void Unsupported_or_malformed_files_are_rejected(string description, byte[] content)
    {
        Assert.Null(_processor.Sanitize(content));
        Assert.NotEmpty(description);
    }
}
