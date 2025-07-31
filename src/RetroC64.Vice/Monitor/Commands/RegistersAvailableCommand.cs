// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;
using RetroC64.Vice.Monitor.Shared;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to get available registers.
/// </summary>
public class RegistersAvailableCommand() : MonitorCommand(MonitorCommandType.RegistersAvailable)
{
    /// <summary>
    /// Gets or sets the memory space.
    /// </summary>
    public MemSpace Memspace { get; set; } = MemSpace.MainMemory;

    public override int BodyLength => sizeof(byte);

    public override void Serialize(Span<byte> buffer)
    {
        buffer[0] = (byte)Memspace;
    }
    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", Memspace: {Memspace}");
    }
}