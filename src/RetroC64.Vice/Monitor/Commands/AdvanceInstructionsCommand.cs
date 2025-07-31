// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to advance a number of instructions.
/// </summary>
public class AdvanceInstructionsCommand() : MonitorCommand(MonitorCommandType.AdvanceInstructions)
{
    /// <summary>
    /// Gets or sets a value indicating whether to step over subroutines.
    /// </summary>
    public bool StepOverSubroutines { get; set; }

    /// <summary>
    /// Gets or sets the instruction count.
    /// </summary>
    public ushort InstructionCount { get; set; }

    public override int BodyLength => sizeof(byte) + sizeof(ushort);

    public override void Serialize(Span<byte> buffer)
    {
        buffer[0] = AsByte(StepOverSubroutines);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(1, 2), InstructionCount);
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", StepOverSubroutines: {StepOverSubroutines}, InstructionCount: {InstructionCount}");
    }
}