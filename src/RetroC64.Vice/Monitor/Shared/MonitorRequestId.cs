// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor;

/// <summary>
/// Represents a unique request identifier for monitor commands and responses.
/// </summary>
public readonly record struct MonitorRequestId(uint Value)
{
    /// <summary>
    /// Gets a value indicating whether this request ID represents an event.
    /// </summary>
    public bool IsEvent => Value == 0xffffffff;

    /// <summary>
    /// Returns a string representation of the request ID.
    /// </summary>
    public override string ToString() => IsEvent ? "Event" : $"0x{Value:X8}";
}