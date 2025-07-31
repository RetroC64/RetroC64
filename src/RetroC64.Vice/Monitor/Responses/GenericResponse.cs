// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace RetroC64.Vice.Monitor.Responses;

/// <summary>
/// Generic response for unhandled response types.
/// </summary>
public class GenericResponse(MonitorResponseType type) : MonitorResponse(type)
{
    /// <summary>
    /// Gets or sets the response body.
    /// </summary>
    public byte[] Body { get; set; } = [];

    public override void Deserialize(ReadOnlySpan<byte> body)
    {
        if (body.Length > 0)
        {
            Body = new byte[body.Length];
            body.CopyTo(Body);
        }
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", Body.Length: {Body.Length}");
    }
}