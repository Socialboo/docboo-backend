using System.Buffers.Binary;

namespace Application.Assets;

/// <param name="ContentType">image/png or image/jpeg.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
public sealed record ImageInfo(string ContentType, int Width, int Height);

/// <summary>
/// Identifies PNG and JPEG images by their signature and reads the pixel size from the header,
/// without decoding the image. Anything else (SVG, GIF, renamed files, truncated files) is rejected.
/// </summary>
public static class ImageInspector
{
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static ImageInfo? Inspect(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 24 && data[..8].SequenceEqual(PngSignature) && data.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            var width = BinaryPrimitives.ReadInt32BigEndian(data.Slice(16, 4));
            var height = BinaryPrimitives.ReadInt32BigEndian(data.Slice(20, 4));
            return width > 0 && height > 0 ? new ImageInfo("image/png", width, height) : null;
        }

        if (data.Length >= 4 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return InspectJpeg(data);

        return null;
    }

    private static ImageInfo? InspectJpeg(ReadOnlySpan<byte> data)
    {
        var i = 2;
        while (i + 4 <= data.Length)
        {
            if (data[i] != 0xFF) return null;
            var marker = data[i + 1];
            if (marker == 0xFF) { i++; continue; }              // fill byte
            if (marker is 0xD8 or 0x01 or (>= 0xD0 and <= 0xD7)) { i += 2; continue; } // markers without length
            if (marker == 0xD9 || marker == 0xDA) return null;  // end/start of scan before a frame header

            var length = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(i + 2, 2));
            if (length < 2 || i + 2 + length > data.Length) return null;

            // SOF0..SOF15 except DHT (C4), JPG (C8) and DAC (CC) carry the frame size.
            if (marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
            {
                if (length < 7) return null;
                var height = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(i + 5, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(i + 7, 2));
                return width > 0 && height > 0 ? new ImageInfo("image/jpeg", width, height) : null;
            }
            i += 2 + length;
        }
        return null;
    }
}
