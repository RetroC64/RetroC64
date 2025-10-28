// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor;

/// <summary>
/// Specifies the memory space for memory-related commands.
/// </summary>
public enum MemSpace : byte
{
    /// <summary>
    /// Default space (equal to <see cref="Computer"/>).
    /// </summary>
    Default = 0,
    /// <summary>
    /// Main memory of the computer.
    /// </summary>
    Computer,
    /// <summary>
    /// Memory of drive 8.
    /// </summary>
    Drive8,
    /// <summary>
    /// Memory of drive 9.
    /// </summary>
    Drive9,
    /// <summary>
    /// Memory of drive 10.
    /// </summary>
    Drive10,
    /// <summary>
    /// Memory of drive 11.
    /// </summary>
    Drive11,
    /// <summary>
    /// Represents an invalid space.
    /// </summary>
    Invalid
}