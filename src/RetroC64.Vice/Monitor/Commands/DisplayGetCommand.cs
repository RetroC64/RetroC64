// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to get display data.
/// </summary>
public class DisplayGetCommand() : MonitorCommand(MonitorCommandType.DisplayGet)
{
    /// <summary>
    /// Gets or sets the VIC-II usage flag.
    /// </summary>
    public bool UseVicII { get; set; }

    /// <summary>
    /// Gets or sets the display format.
    /// </summary>
    public DisplayGetMode Format { get; set; }

    public override int BodyLength => sizeof(byte) + sizeof(byte);

    public override void Serialize(Span<byte> buffer)
    {
        buffer[0] = AsByte(UseVicII);
        buffer[1] = (byte)Format;
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", UseVicII: {UseVicII}, Format: {Format}");
    }
}