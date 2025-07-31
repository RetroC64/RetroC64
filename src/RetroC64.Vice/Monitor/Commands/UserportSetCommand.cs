// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to set a value on the user port.
/// </summary>
public class UserPortSetCommand() : MonitorCommand(MonitorCommandType.UserPortSet)
{
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public ushort Value { get; set; }

    public override int BodyLength => sizeof(ushort);

    public override void Serialize(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, Value);
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", Value: {Value}");
    }
}