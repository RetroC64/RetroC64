// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to get a resource value.
/// </summary>
public class ResourceGetCommand() : MonitorCommand(MonitorCommandType.ResourceGet)
{
    /// <summary>
    /// Gets or sets the resource name.
    /// </summary>
    public required string ResourceName { get; set; }

    public override int BodyLength => SizeOfString(ResourceName);

    public override void Serialize(Span<byte> buffer)
    {
        WriteString(ResourceName, buffer);
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", ResourceName: \"{ResourceName}\"");
    }
}