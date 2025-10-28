// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace RetroC64.Vice.Monitor.Responses;

/// <summary>
/// Response containing checkpoint information.
/// </summary>
public class CheckpointListResponse() : MonitorResponse(MonitorResponseType.CheckpointList)
{
    /// <summary>
    /// Gets or sets the number of checkpoint infos received before this response as <see cref="CheckpointResponse"/>.
    /// </summary>
    public uint Count { get; set; }

    public override void Deserialize(ReadOnlySpan<byte> body)
    {
        Count = BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(0, 4));
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", Count: {Count}");
    }
}