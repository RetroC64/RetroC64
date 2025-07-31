// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;
using RetroC64.Vice.Monitor.Shared;

namespace RetroC64.Vice.Monitor.Responses;

/// <summary>
/// Response containing a resource value.
/// </summary>
public class ResourceGetResponse() : MonitorResponse(MonitorResponseType.ResourceGet)
{
    /// <summary>
    /// Gets or sets the resource value.
    /// </summary>
    public ResourceValue ResourceValue { get; set; }

    public override void Deserialize(ReadOnlySpan<byte> body)
    {
        if (body.Length < 2)
        {
            return;
        }

        var type = (ResourceValueKind)body[0];
        var len = body[1];

        if (type == ResourceValueKind.String) // String type
        {
            ResourceValue = new(Encoding.ASCII.GetString(body.Slice(2, len)));
        }
        else if (type == ResourceValueKind.Integer) // Integer type
        {
            if (len != 4)
            {
                Error = MonitorErrorKind.InvalidLength; // Invalid length
            }
            else
            {
                ResourceValue = new(BinaryPrimitives.ReadInt32LittleEndian(body.Slice(2, 4)));
            }
        }
        else
        {
            Error = MonitorErrorKind.InvalidLength;
        }
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", ResourceValue: {ResourceValue}");
    }
}