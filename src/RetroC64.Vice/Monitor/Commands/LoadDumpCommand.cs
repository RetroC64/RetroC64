// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to load emulator state from a file.
/// </summary>
public class LoadDumpCommand() : MonitorCommand(MonitorCommandType.LoadDump)
{
    /// <summary>
    /// Gets or sets the filename.
    /// </summary>
    public required string Filename { get; set; }

    public override int BodyLength => SizeOfString(Filename);

    public override void Serialize(Span<byte> buffer)
    {
        WriteString(Filename, buffer);
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", Filename: \"{Filename}\"");
    }
}