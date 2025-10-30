// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using SkiaSharp;

namespace RetroC64.Graphics;

/// <summary>
/// Represents an abstract base class for a Commodore 64-style sprite, providing drawing surfaces and export
/// functionality.
/// </summary>
public abstract class C64Sprite : IDisposable
{
    private readonly SKBitmap _bitmap;
    private readonly SKCanvas _canvas;
    private readonly SKPaint _brush;
    
    protected C64Sprite()
    {
        // ReSharper disable VirtualMemberCallInConstructor
        _bitmap = new SKBitmap(Width, Height, SKColorType.Alpha8, SKAlphaType.Opaque);
        // ReSharper restore VirtualMemberCallInConstructor
        _canvas = new SKCanvas(_bitmap);
        _brush = new SKPaint()
        {
            IsAntialias = false,
            Color = SKColors.White,
        };
    }

    /// <summary>
    /// Gets the width of the element, in pixels.
    /// </summary>
    public abstract int Width { get; }
    
    /// <summary>
    /// Gets the vertical dimension of the object, typically measured in pixels.
    /// </summary>
    public abstract int Height { get; }

    /// <summary>
    /// Gets the underlying alpha-only bitmap for the sprite (owned by this instance).
    /// </summary>
    /// <remarks>
    /// Do not dispose this bitmap directly; it is disposed by <see cref="Dispose()"/>.
    /// </remarks>
    public SKBitmap Bitmap => _bitmap;

    /// <summary>
    /// Gets a canvas targeting the sprite bitmap (owned by this instance).
    /// </summary>
    /// <remarks>
    /// Use this canvas with <see cref="Brush"/> to draw into the sprite. Do not dispose this canvas directly.
    /// </remarks>
    public SKCanvas Canvas => _canvas;

    /// <summary>
    /// Gets a reusable paint brush configured for drawing into the sprite.
    /// </summary>
    /// <remarks>
    /// The brush is owned by this instance. Do not dispose it directly. Use <see cref="UseFill"/> and <see cref="UseStroke(float)"/> to configure it.
    /// </remarks>
    public SKPaint Brush => _brush;

    /// <summary>
    /// Sets the shared <see cref="Brush"/> style to <see cref="SKPaintStyle.Fill"/>.
    /// </summary>
    /// <returns>The current <see cref="C64SpriteMono"/> to allow method chaining.</returns>
    public C64Sprite UseFill()
    {
        _brush.Style = SKPaintStyle.Fill;
        return this;
    }

    /// <summary>
    /// Sets the shared <see cref="Brush"/> style to <see cref="SKPaintStyle.Stroke"/> with the specified width.
    /// </summary>
    /// <param name="width">Stroke width in device pixels. Defaults to 1.0.</param>
    /// <returns>The current <see cref="C64SpriteMono"/> to allow method chaining.</returns>
    public C64Sprite UseStroke(float width = 1.0f)
    {
        _brush.Style = SKPaintStyle.Stroke;
        _brush.StrokeWidth = width;
        return this;
    }
    
    /// <summary>
    /// Exports the sprite to a buffer of 64 bytes.
    /// </summary>
    /// <returns>A buffer of 64 bytes containing sprite data</returns>
    public Span<byte> ToBits()
    {
        var output = new byte[64];
        ToBits(output);
        return output;
    }

    /// <summary>
    /// Exports the sprite into the provided buffer.
    /// </summary>
    /// <param name="output">A span over a 64-byte buffer to receive the bits. </param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="output"/> is smaller than 64 bytes.</exception>
    public abstract void ToBits(Span<byte> output);

    /// <summary>
    /// Releases resources used by this instance.
    /// </summary>
    /// <remarks>
    /// Disposes the shared brush, backing bitmap, and canvas. After calling this method, the instance should not be used.
    /// </remarks>
    public void Dispose()
    {
        _brush.Dispose();
        _bitmap.Dispose();
        _canvas.Dispose();
    }
}