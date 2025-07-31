// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace RetroC64.Vice.Monitor.Shared;

/// <summary>
/// Specifies the memory space for memory-related commands.
/// </summary>
public enum MemSpace : byte
{
    /// <summary>
    /// Main memory of the emulator.
    /// </summary>
    MainMemory = 0,
    /// <summary>
    /// Memory of drive 8.
    /// </summary>
    Drive8 = 1,
    /// <summary>
    /// Memory of drive 9.
    /// </summary>
    Drive9 = 2,
    /// <summary>
    /// Memory of drive 10.
    /// </summary>
    Drive10 = 3,
    /// <summary>
    /// Memory of drive 11.
    /// </summary>
    Drive11 = 4,
}