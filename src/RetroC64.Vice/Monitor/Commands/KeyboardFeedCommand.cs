// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Command to feed keyboard input.
/// </summary>
public class KeyboardFeedCommand() : MonitorCommand(MonitorCommandType.KeyboardFeed)
{
    /// <summary>
    /// Gets or sets the text to feed.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    public override int BodyLength => SizeOfString(Text);

    public override void Serialize(Span<byte> buffer)
    {
        WriteString(Text, buffer);
    }

    protected override void AppendMembers(StringBuilder builder)
    {
        builder.Append($", Text: \"{Text}\"");
    }
}