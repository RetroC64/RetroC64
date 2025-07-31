// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to set a condition on a checkpoint.
/// </summary>
public class ConditionSetCommand() : MonitorCommand(MonitorCommandType.ConditionSet)
{
    /// <summary>
    /// Gets or sets the checkpoint number.
    /// </summary>
    public uint CheckpointNumber { get; set; }

    /// <summary>
    /// Gets or sets the condition string.
    /// </summary>
    public string Condition { get; set; } = string.Empty;

    public override int BodyLength => sizeof(uint) + SizeOfString(Condition);

    public override void Serialize(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, CheckpointNumber);
        WriteString(Condition, buffer.Slice(4));
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", CheckpointNumber: {CheckpointNumber}, Condition: \"{Condition}\"");
    }
}