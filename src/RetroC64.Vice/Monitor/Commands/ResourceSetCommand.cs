// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to set a resource value.
/// </summary>
public class ResourceSetCommand() : MonitorCommand(MonitorCommandType.ResourceSet)
{
    /// <summary>
    /// Gets or sets the resource name.
    /// </summary>
    public required string ResourceName { get; set; }

    /// <summary>
    /// Gets or sets the resource value.
    /// </summary>
    public ResourceValue ResourceValue { get; set; }

    public override int BodyLength =>
        sizeof(byte) + // resource kind
        SizeOfString(ResourceName) +
        ResourceValue.GetSizeInBytes();

    public override void Serialize(Span<byte> buffer)
    {
        buffer[0] = (byte)ResourceValue.Kind; // 0 for string, 1 for integer

        buffer = WriteString(ResourceName, buffer.Slice(1));

        if (ResourceValue.Kind == ResourceValueKind.Integer)
        {
            buffer[0] = 4; // Length for integer
            BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(1, 4), ResourceValue.AsInt ?? 0);
            return;
        }

        // For string type, we write the length and the string value
        WriteString(ResourceValue.AsString ?? string.Empty, buffer);
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", ResourceName: \"{ResourceName}\", ResourceValue: \"{ResourceValue}\"");
    }
}