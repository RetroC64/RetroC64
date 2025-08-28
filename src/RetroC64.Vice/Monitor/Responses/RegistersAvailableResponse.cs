// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace RetroC64.Vice.Monitor.Responses;

/// <summary>
/// Response containing available registers.
/// </summary>
public class RegistersAvailableResponse() : MonitorResponse(MonitorResponseType.RegistersAvailable)
{
    /// <summary>
    /// Gets or sets the available registers.
    /// </summary>
    public RegisterName[] Registers { get; set; } = [];

    public override void Deserialize(ReadOnlySpan<byte> body)
    {
        var registerCount = BinaryPrimitives.ReadUInt16LittleEndian(body);
        body = body.Slice(2); // Skip the bank count

        Registers = new RegisterName[registerCount];
        for (int i = 0; i < registerCount; i++)
        {
            var originalBody = body;
            var itemSize = body[0];
            var registerId = body[1];
            var registerSize = body[2];
            var registerName = ReadString(body.Slice(3));
            Registers[i] = new(new(registerId), registerSize, registerName);

            // Skip the item
            body = originalBody.Slice(itemSize + 1);
        }
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", Registers: [{string.Join(", ", Registers)}]");
    }
}