// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to autostart a file in the emulator.
/// </summary>
public class AutostartCommand() : MonitorCommand(MonitorCommandType.Autostart)
{
    /// <summary>
    /// Gets or sets a value indicating whether to run after loading.
    /// </summary>
    public bool RunAfterLoading { get; set; }

    /// <summary>
    /// Gets or sets the file index.
    /// </summary>
    public ushort FileIndex { get; set; }

    /// <summary>
    /// Gets or sets the filename.
    /// </summary>
    public required string Filename { get; set; }

    public override int BodyLength => sizeof(byte) + sizeof(ushort) + SizeOfString(Filename);

    public override void Serialize(Span<byte> buffer)
    {
        buffer[0] = AsByte(RunAfterLoading);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.Slice(1, 2), FileIndex);
        WriteString(Filename, buffer.Slice(3));
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", RunAfterLoading: {RunAfterLoading}, FileIndex: {FileIndex}, Filename: \"{Filename}\"");
    }
}