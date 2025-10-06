// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using SkiaSharp;

namespace RetroC64.Graphics;

public class C64Sprite : IDisposable
{
    private readonly SKBitmap _bitmap;
    private readonly SKCanvas _canvas;
    private readonly SKPaint _brush;

    public const int Width = 24;

    public const int Height = 21;
    
    public C64Sprite()
    {
        _bitmap = new SKBitmap(Width, Height, SKColorType.Alpha8, SKAlphaType.Opaque);
        _canvas = new SKCanvas(_bitmap);
        _brush = new SKPaint()
        {
            IsAntialias = false,
            Color = SKColors.White,
        };
    }

    public SKBitmap Bitmap => _bitmap;

    public SKCanvas Canvas => _canvas;

    public SKPaint Brush => _brush;

    public C64Sprite UseFill()
    {
        _brush.Style = SKPaintStyle.Fill;
        return this;
    }

    public C64Sprite UseStroke(float width = 1.0f)
    {
        _brush.Style = SKPaintStyle.Stroke;
        _brush.StrokeWidth = width;
        return this;
    }

    public Span<byte> ToBits()
    {
        var output = new byte[64];
        ToBits(output);
        return output;
    }

    public unsafe void FromBits(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < 64) throw new ArgumentException("Input buffer must be at least 64 bytes");

        _canvas.Clear(SKColors.Transparent);
        byte* data = (byte*)_bitmap.GetPixels();
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
    
    public unsafe void ToBits(Span<byte> output)
    {
        if (output.Length < 64) throw new ArgumentException("Output buffer must be at least 64 bytes");

        byte* data = (byte*)_bitmap.GetPixels();

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

    public void Dispose()
    {
        _brush.Dispose();
        _bitmap.Dispose();
        _canvas.Dispose();
    }
}