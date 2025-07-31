// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace RetroC64.Vice.Monitor.Responses;

/// <summary>
/// Response indicating the emulator has resumed.
/// </summary>
public class ResumedResponse() : MonitorResponse(MonitorResponseType.Resumed)
{
    /// <summary>
    /// Gets or sets the program counter.
    /// </summary>
    public ushort ProgramCounter { get; set; }

    public override void Deserialize(ReadOnlySpan<byte> body)
    {
        ProgramCounter = BinaryPrimitives.ReadUInt16LittleEndian(body);
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", ProgramCounter: 0x{ProgramCounter:X4}");
    }
}