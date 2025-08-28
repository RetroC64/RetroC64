// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor;

/// <summary>
/// Represents a value for a resource (string or integer).
/// </summary>
public readonly record struct ResourceValue
{
    /// <summary>
    /// Gets the kind of the resource value.
    /// </summary>
    public ResourceValueKind Kind { get; }

    /// <summary>
    /// Gets the string value, if applicable.
    /// </summary>
    public string? AsString { get; }

    /// <summary>
    /// Gets the integer value, if applicable.
    /// </summary>
    public int? AsInt { get; }

    public ResourceValue(string asString)
    {
        Kind = ResourceValueKind.String;
        AsString = asString;
        AsInt = null;
    }

    public ResourceValue(int? asInt)
    {
        Kind = ResourceValueKind.Integer;
        AsInt = asInt;
        AsString = null;
    }

    public int GetSizeInBytes()
    {
        return Kind switch
        {
            ResourceValueKind.String => 1 + System.Text.Encoding.UTF8.GetByteCount(AsString ?? string.Empty),
            ResourceValueKind.Integer => 1 + 4, // 1 byte for length + 4 bytes for integer
            _ => throw new InvalidOperationException("Unknown ResourceValueKind")
        };
    }

    public override string ToString()
    {
        if (Kind == ResourceValueKind.String)
        {
            return AsString ?? "";
        }

        return $"0x{AsInt:X8}";
    }
}