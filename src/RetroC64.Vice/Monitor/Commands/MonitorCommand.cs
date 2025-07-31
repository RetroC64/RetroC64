// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;

namespace RetroC64.Vice.Monitor.Commands;

/// <summary>
/// Base class for all monitor commands.
/// </summary>
public abstract class MonitorCommand(MonitorCommandType commandType)
{
    /// <summary>
    /// Gets or sets the request identifier for this command.
    /// </summary>
    public uint RequestId { get; internal set; }

    /// <summary>
    /// Gets the command type.
    /// </summary>
    public MonitorCommandType CommandType { get; } = commandType;

    /// <summary>
    /// Gets the length of the command body in bytes.
    /// </summary>
    public abstract int BodyLength { get; }

    /// <summary>
    /// Serializes the command body into the specified buffer.
    /// </summary>
    /// <param name="buffer">The buffer to serialize into.</param>
    public abstract void Serialize(Span<byte> buffer);

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append($"Command: {CommandType}, RequestId: {RequestId}, BodyLength: {BodyLength}");
        AppendMembers(builder);
        return builder.ToString();
    }

    protected virtual void AppendMembers(StringBuilder builder)
    {
        // No-op for base
    }

    protected static int SizeOfString(string text)
    {
        return 1 + System.Text.Encoding.ASCII.GetByteCount(text);
    }

    protected static Span<byte> WriteString(string text, Span<byte> buffer)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(text);
        if (bytes.Length > 255) throw new ArgumentException("String too long", nameof(text));
        buffer[0] = (byte)bytes.Length;
        bytes.CopyTo(buffer.Slice(1));

        return buffer.Slice(1 + bytes.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static byte AsByte(bool value) => value ? (byte)1 : (byte)0;
}