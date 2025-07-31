// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to get the palette.
/// </summary>
public class PaletteGetCommand() : MonitorCommand(MonitorCommandType.PaletteGet)
{
    /// <summary>
    /// Gets or sets whether to use the VIC-II palette.
    /// </summary>
    public bool UseVicII { get; set; }
    
    public override int BodyLength => sizeof(byte);

    public override void Serialize(Span<byte> buffer)
    {
        buffer[0] = AsByte(UseVicII);
    }
}