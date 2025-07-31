// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to set a value on a joystick port.
/// </summary>
public class JoyPortSetCommand() : MonitorCommand(MonitorCommandType.JoyPortSet)
{
    /// <summary>
    /// Gets or sets the port number.
    /// </summary>
    public ushort Port { get; set; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public ushort Value { get; set; }

    public override int BodyLength => sizeof(ushort) + sizeof(ushort);

    public override void Serialize(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(0, 2), Port);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(2, 2), Value);
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", Port: {Port}, Value: {Value}");
    }
}