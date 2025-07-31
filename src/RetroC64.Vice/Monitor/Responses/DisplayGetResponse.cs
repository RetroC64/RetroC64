// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;
using RetroC64.Vice.Monitor.Shared;

namespace RetroC64.Vice.Monitor.Responses;

/// <summary>
/// Response containing display data.
/// </summary>
public class DisplayGetResponse() : MonitorResponse(MonitorResponseType.DisplayGet)
{
    /// <summary>
    /// Gets or sets the width of the display.
    /// </summary>
    public ushort Width { get; set; }

    /// <summary>
    /// Gets or sets the height of the display.
    /// </summary>
    public ushort Height { get; set; }

    /// <summary>
    /// Gets or sets the X coordinate of the inner display area.
    /// </summary>
    public ushort InnerX { get; set; }

    /// <summary>
    /// Gets or sets the Y coordinate of the inner display area.
    /// </summary>
    public ushort InnerY { get; set; }

    /// <summary>
    /// Gets or sets the width of the inner display area.
    /// </summary>
    public ushort InnerWidth { get; set; }

    /// <summary>
    /// Gets or sets the height of the inner display area.
    /// </summary>
    public ushort InnerHeight { get; set; }

    /// <summary>
    /// Gets or sets the number of bits per pixel.
    /// </summary>
    public byte BitsPerPixel { get; set; }

    /// <summary>
    /// Gets or sets the display data.
    /// </summary>
    public byte[] DisplayData { get; set; } = [];

    public override void Deserialize(ReadOnlySpan<byte> body)
    {
        var fieldLength = BinaryPrimitives.ReadUInt32LittleEndian(body);

        body = body.Slice(sizeof(uint));

        if (fieldLength < 13)
        {
            Error = MonitorErrorKind.InvalidLength;
            return;
        }

        Width = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(0, 2));
        Height = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(2, 2));
        InnerX = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(4, 2));
        InnerY = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(6, 2));
        InnerWidth = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(8, 2));
        InnerHeight = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(10, 2));
        BitsPerPixel = body[12];

        body = body.Slice((int)fieldLength);

        var bufferLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(0, 4)) - 4;

        body = body.Slice(sizeof(uint));
        DisplayData = body.Slice(0, (int)bufferLength).ToArray();
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.AppendLine($", Width: {Width}, Height: {Height}, InnerX: {InnerX}, InnerY: {InnerY}, " +
                           $"InnerWidth: {InnerWidth}, InnerHeight: {InnerHeight}, BitsPerPixel: {BitsPerPixel}, DisplayData Length: {DisplayData.Length}");
    }
}