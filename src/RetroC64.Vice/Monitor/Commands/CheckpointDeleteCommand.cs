// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to delete a checkpoint.
/// </summary>
public class CheckpointDeleteCommand() : MonitorCommand(MonitorCommandType.CheckpointDelete)
{
    /// <summary>
    /// Gets or sets the checkpoint number.
    /// </summary>
    public uint CheckpointNumber { get; set; }

    public override int BodyLength => sizeof(uint);

    public override void Serialize(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, CheckpointNumber);
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", CheckpointNumber: {CheckpointNumber}");
    }
}