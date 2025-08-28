// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor;

/// <summary>
/// Specifies the kind of value a resource can have.
/// </summary>
public enum ResourceValueKind : byte
{
    /// <summary>
    /// The resource value is a string.
    /// </summary>
    String = 0,
    /// <summary>
    /// The resource value is an integer.
    /// </summary>
    Integer = 1,
}