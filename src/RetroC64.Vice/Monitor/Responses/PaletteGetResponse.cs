// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace RetroC64.Vice.Monitor.Responses;

/// <summary>
/// Response containing palette data.
/// </summary>
public class PaletteGetResponse() : MonitorResponse(MonitorResponseType.PaletteGet)
{
    /// <summary>
    /// Gets or sets the palette data.
    /// </summary>
    public PaletteColor[] Palette { get; set; } = [];

    public override void Deserialize(ReadOnlySpan<byte> body)
    {
        var entryCount = BinaryPrimitives.ReadUInt16LittleEndian(body);

        var palette = new PaletteColor[entryCount];
        body = body.Slice(2); // Skip the number of entries
        for(int i = 0; i < entryCount; i++)
        {
            var itemSize = body[0];
            body = body.Slice(1); // Move past the item size

            byte r = 0, g = 0, b = 0;
            if (itemSize >= 1)
            {
                r = body[0]; // Red component
            }
            if (itemSize >= 2)
            {
                g = body[1]; // Green component
            }
            if (itemSize >= 3)
            {
                b = body[2]; // Blue component
            }

            palette[i] = new PaletteColor(r, g, b);

            body = body.Slice(itemSize); // Move to the next color
        }

        Palette = palette;
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", Palette.Length: {Palette.Length}");
    }
}

public readonly record struct PaletteColor(byte R, byte G, byte B);