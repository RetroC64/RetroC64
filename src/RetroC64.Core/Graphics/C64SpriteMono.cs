// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using SkiaSharp;

namespace RetroC64.Graphics;

/// <summary>
/// Represents a Commodore 64 sprite editor surface backed by an alpha-only <see cref="SKBitmap"/> (24x21).
/// </summary>
/// <remarks>
/// - Pixel format: <see cref="SKColorType.Alpha8"/>. Any non-zero pixel is considered "set" (opaque), zero is "unset".
/// - Logical sprite size: 24 pixels wide by 21 pixels high (C64 standard).
/// - Bit layout for import/export: 1 bit per pixel, row-major, 3 bytes per row (24/8), 21 rows = 63 bytes. This API
///   uses a 64-byte buffer where the last byte is unused/reserved for convenience and alignment.
/// - Thread-safety: Instances are not thread-safe. Access from a single thread at a time.
/// </remarks>
public class C64SpriteMono : C64Sprite
{
    /// <summary>
    /// Gets the sprite width in pixels (24).
    /// </summary>
    public override int Width => 24;

    /// <summary>
    /// Gets the sprite height in pixels (21).
    /// </summary>
    public override int Height => 21;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="C64SpriteMono"/> class with an alpha-only backing bitmap and a configured paint brush.
    /// </summary>
    /// <remarks>
    /// The brush is initialized to white color with antialiasing disabled to match the crisp C64 sprite look.
    /// </remarks>
    public C64SpriteMono()
    {
    }

    /// <summary>
    /// Imports a 1-bit per pixel representation into the sprite bitmap.
    /// </summary>
    /// <param name="buffer">
    /// A 64-byte buffer in row-major format with 3 bytes per row for 21 rows (63 used bytes; last byte unused).
    /// A set bit (1) maps to an opaque pixel (255); an unset bit (0) maps to a transparent/zero pixel.
    /// </param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="buffer"/> is smaller than 64 bytes.</exception>
    /// <remarks>
    /// Clears the canvas before applying bits. Any non-zero bit sets the destination pixel to 255 in the alpha channel.
    /// </remarks>
    public unsafe void FromBits(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 64) throw new ArgumentException("Input buffer must be at least 64 bytes");

        Canvas.Clear(SKColors.Transparent);
        byte* data = (byte*)Bitmap.GetPixels();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int byteIndex = (y * Width / 8) + (x / 8);
                int bitIndex = 7 - (x % 8);
                if ((buffer[byteIndex] & (1 << bitIndex)) != 0)
                {
                    data[y * Width + x] = 255;
                }
                else
                {
                    data[y * Width + x] = 0;
                }
            }
        }
    }
    
    /// <summary>
    /// Exports the sprite into the provided buffer as a 1-bit per pixel representation.
    /// </summary>
    /// <param name="output">
    /// A span over a 64-byte buffer to receive the bits. Layout is row-major, 3 bytes per row, 21 rows (63 used bytes; last byte unused).
    /// </param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="output"/> is smaller than 64 bytes.</exception>
    /// <remarks>
    /// This method ORs bits into <paramref name="output"/> and does not clear it. For deterministic output,
    /// callers should clear the buffer (e.g., using <c>output.Clear()</c>) before calling.
    /// Any non-zero pixel is treated as a set bit (1).
    /// </remarks>
    public override unsafe void ToBits(Span<byte> output)
    {
        if (output.Length < 64) throw new ArgumentException("Output buffer must be at least 64 bytes");

        byte* data = (byte*)Bitmap.GetPixels();

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var pixel = data[y * Width + x];
                if (pixel != 0)
                {
                    int byteIndex = (y * Width / 8) + (x / 8);
                    int bitIndex = 7 - (x % 8);
                    output[byteIndex] |= (byte)(1 << bitIndex);
                }
            }
        }
    }
}