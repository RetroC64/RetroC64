// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to reset the emulator.
/// </summary>
public class ResetCommand() : MonitorCommand(MonitorCommandType.Reset)
{
    /// <summary>
    /// Gets or sets what to reset.
    /// </summary>
    public ResetType WhatToReset { get; set; }

    public override int BodyLength => sizeof(byte);

    public override void Serialize(Span<byte> buffer)
    {
        buffer[0] = (byte)WhatToReset;
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", WhatToReset: {WhatToReset}");
    }
}