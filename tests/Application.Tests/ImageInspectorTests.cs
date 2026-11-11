using Application.Assets;

namespace Application.Tests;

public class ImageInspectorTests
{
    private static byte[] Png(int width, int height)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(bytes, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
        return bytes;
    }

    /// <summary>SOI, an APP0 segment, then a baseline SOF0 frame header.</summary>
    private static byte[] Jpeg(ushort width, ushort height) =>
    [
        0xFF, 0xD8,
        0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00,
        0xFF, 0xC0, 0x00, 0x0B, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 0x01, 0x01, 0x11, 0x00,
        0xFF, 0xD9,
    ];

    [Fact]
    public void Reads_png_size()
    {
        Assert.Equal(new ImageInfo("image/png", 600, 240), ImageInspector.Inspect(Png(600, 240)));
    }

    [Fact]
    public void Reads_jpeg_size_skipping_other_segments()
    {
        Assert.Equal(new ImageInfo("image/jpeg", 1024, 768), ImageInspector.Inspect(Jpeg(1024, 768)));
    }

    [Fact]
    public void Reads_the_real_golden_sample_image()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Samples", "fluxo-exportacao.png"));

        Assert.Equal(new ImageInfo("image/png", 600, 240), ImageInspector.Inspect(bytes));
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>")]
    [InlineData("GIF89a......")]
    [InlineData("%PDF-1.7")]
    [InlineData("")]
    public void Rejects_other_formats(string content)
    {
        Assert.Null(ImageInspector.Inspect(System.Text.Encoding.ASCII.GetBytes(content)));
    }

    [Fact]
    public void Rejects_truncated_or_zero_sized_images()
    {
        Assert.Null(ImageInspector.Inspect(Png(600, 240)[..20]));
        Assert.Null(ImageInspector.Inspect(Png(0, 240)));
        Assert.Null(ImageInspector.Inspect(Jpeg(800, 600)[..12]));
    }
}
