// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to dump emulator state to a file.
/// </summary>
public class DumpCommand() : MonitorCommand(MonitorCommandType.Dump)
{
    /// <summary>
    /// Gets or sets a value indicating whether to save ROMs.
    /// </summary>
    public bool SaveROMs { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether to save disks.
    /// </summary>
    public bool SaveDisks { get; set; }

    /// <summary>
    /// Gets or sets the filename.
    /// </summary>
    public required string Filename { get; set; }

    public override int BodyLength => 2 + 1 + System.Text.Encoding.UTF8.GetByteCount(Filename);

    public override void Serialize(Span<byte> buffer)
    {
        buffer[0] = AsByte(SaveROMs);
        buffer[1] = AsByte(SaveDisks);
        WriteString(Filename, buffer.Slice(2));
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", SaveROMs: {SaveROMs}, SaveDisks: {SaveDisks}, Filename: \"{Filename}\"");
    }
}