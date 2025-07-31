// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to toggle a checkpoint's enabled state.
/// </summary>
public class CheckpointToggleCommand() : MonitorCommand(MonitorCommandType.CheckpointToggle)
{
    /// <summary>
    /// Gets or sets the checkpoint number.
    /// </summary>
    public uint CheckpointNumber { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the checkpoint is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    public override int BodyLength => sizeof(uint) + sizeof(byte);

    public override void Serialize(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, CheckpointNumber);
        buffer[4] = AsByte(Enabled);
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", CheckpointNumber: {CheckpointNumber}, Enabled: {Enabled}");
    }
}