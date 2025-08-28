// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace RetroC64.Vice.Monitor.Responses;

/// <summary>
/// Response containing VICE version information.
/// </summary>
public class ViceInfoResponse() : MonitorResponse(MonitorResponseType.ViceInfo)
{
    /// <summary>
    /// Gets or sets the VICE version.
    /// </summary>
    public Version? Version { get; set; }

    /// <summary>
    /// Gets or sets the SVN revision.
    /// </summary>
    public uint SvnRevision { get; set; }

    public override void Deserialize(ReadOnlySpan<byte> body)
    {
        if (body.Length < 10 || body[0] != 4 || body[5] != 4)
        {
            Error = MonitorErrorKind.InvalidLength;
            return;
        }
        var versionNumber = BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(1, 4));
        Version = new Version(
            (int)(versionNumber & 0xFF),
            (int)((versionNumber >> 8) & 0xFF),
            (int)((versionNumber >> 16) & 0xFF),
            (int)((versionNumber >> 24) & 0xFF)
        );
        SvnRevision = BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(5, 4));
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", Version: {Version}, SvnRevision: {SvnRevision}");
    }
}