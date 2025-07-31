// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;
using RetroC64.Vice.Monitor.Shared;

namespace RetroC64.Vice.Monitor.Responses;

/// <summary>
/// Response containing register values.
/// </summary>
public class RegisterResponse() : MonitorResponse(MonitorResponseType.RegisterInfo)
{
    /// <summary>
    /// Gets or sets the register values.
    /// </summary>
    public RegisterValue[] Items { get; set; } = [];

    public override void Deserialize(ReadOnlySpan<byte> body)
    {
        var count = BinaryPrimitives.ReadUInt16LittleEndian(body);
        body = body.Slice(2); // Skip count byte

        Items = new RegisterValue[count];
        for (int i = 0; i < count; i++)
        {
            byte size = body[0];
            byte regId = body[1];
            ushort value = 0;
            if (size == 2)
            {
                value = body[2];

            }
            else if (size == 3)
            {
                // size == 3
                value = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(2));
            }
            else
            {
                Error = MonitorErrorKind.InvalidLength;
                break;
            }

            Items[i] = new(new(regId), value);

            body = body.Slice(size + 1); // Move past the item
        }
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append(", Items: [");
        for (int i = 0; i < Items.Length; i++)
        {
            if (i > 0) builder.Append(", ");
            builder.Append(Items[i]);
        }
        builder.Append("]");
    }
}